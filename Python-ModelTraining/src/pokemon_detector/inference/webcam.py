"""Testable real-time webcam application."""

from __future__ import annotations

import sys
from collections.abc import Callable
from dataclasses import dataclass
from datetime import UTC, datetime
from math import isfinite
from pathlib import Path
from time import monotonic
from typing import Any

import cv2
import numpy as np
import yaml

from pokemon_detector.inference.detector import Detection, UltralyticsDetector
from pokemon_detector.inference.fps import RollingFPS
from pokemon_detector.inference.frame_source import CameraUnavailableError, OpenCVFrameSource
from pokemon_detector.project_paths import resolve_project_path

_REVIEW_CONFIDENCE = 0.65
_CLASS_COLORS = {
    0: (0, 255, 255),
    1: (0, 128, 255),
    2: (255, 128, 0),
}


@dataclass(frozen=True, slots=True)
class WebcamConfig:
    checkpoint: Path
    camera_index: int
    confidence: float
    image_size: int
    device: str
    save_low_confidence: bool
    low_confidence_directory: Path

    def __post_init__(self) -> None:
        if isinstance(self.camera_index, bool) or not isinstance(self.camera_index, int):
            raise TypeError("camera_index must be an integer")
        if self.camera_index < 0:
            raise ValueError("camera_index must be non-negative")
        if not isfinite(self.confidence) or not 0.0 < self.confidence <= 1.0:
            raise ValueError("confidence must be greater than 0 and at most 1")
        if isinstance(self.image_size, bool) or not isinstance(self.image_size, int):
            raise TypeError("image_size must be an integer")
        if self.image_size <= 0:
            raise ValueError("image_size must be positive")
        if self.device not in {"cpu", "mps"}:
            raise ValueError("device must be one of: cpu, mps")
        if not isinstance(self.save_low_confidence, bool):
            raise TypeError("save_low_confidence must be a boolean")

    @classmethod
    def load(cls, path: Path) -> WebcamConfig:
        config_path = path.resolve()
        try:
            payload = yaml.safe_load(config_path.read_text(encoding="utf-8"))
        except FileNotFoundError as error:
            raise ValueError(f"config file does not exist: {path}") from error
        except (UnicodeDecodeError, yaml.YAMLError) as error:
            raise ValueError(f"unable to read webcam config: {error}") from error
        if not isinstance(payload, dict):
            raise TypeError("config file must contain a YAML mapping")
        required = {
            "checkpoint",
            "camera_index",
            "confidence",
            "image_size",
            "device",
            "save_low_confidence",
            "low_confidence_directory",
        }
        missing = sorted(required - payload.keys())
        if missing:
            raise ValueError(f"config file is missing required field: {missing[0]}")
        def resolve_path(value: object) -> Path:
            if not isinstance(value, str):
                raise TypeError("webcam path values must be strings")
            return resolve_project_path(value, start=config_path)

        camera_index = payload["camera_index"]
        image_size = payload["image_size"]
        confidence = payload["confidence"]
        if isinstance(confidence, bool) or not isinstance(confidence, (int, float)):
            raise TypeError("confidence must be numeric")
        return cls(
            checkpoint=resolve_path(payload["checkpoint"]),
            camera_index=camera_index,  # type: ignore[arg-type]
            confidence=float(confidence),
            image_size=image_size,  # type: ignore[arg-type]
            device=payload["device"],  # type: ignore[arg-type]
            save_low_confidence=payload["save_low_confidence"],  # type: ignore[arg-type]
            low_confidence_directory=resolve_path(payload["low_confidence_directory"]),
        )


class OpenCVDisplay:
    """Render the live preview and translate keyboard input."""

    def __init__(self, window_name: str = "Pokemon Detector", *, backend: Any = cv2) -> None:
        self._window_name = window_name
        self._backend = backend
        self._shown = False

    def show(self, frame: np.ndarray) -> int:
        self._backend.imshow(self._window_name, frame)
        self._shown = True
        return self._backend.waitKey(1) & 0xFF

    def close(self) -> None:
        if self._shown:
            self._backend.destroyWindow(self._window_name)
            self._shown = False


def annotate_frame(
    frame: np.ndarray,
    detections: list[Detection],
    *,
    fps: float,
    inference_ms: float,
) -> np.ndarray:
    annotated = frame.copy()
    for detection in detections:
        left, top, right, bottom = detection.xyxy
        color = _CLASS_COLORS[detection.class_id]
        cv2.rectangle(annotated, (left, top), (right, bottom), color, 2)
        label = f"{detection.class_name} {detection.confidence:.2f}"
        cv2.putText(
            annotated,
            label,
            (left, max(15, top - 6)),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.5,
            color,
            1,
            cv2.LINE_AA,
        )
    cv2.putText(
        annotated,
        f"FPS {fps:.1f} | inference {inference_ms:.1f} ms",
        (10, 22),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.55,
        (255, 255, 255),
        1,
        cv2.LINE_AA,
    )
    return annotated


def _default_timestamp() -> str:
    return datetime.now(UTC).strftime("%Y%m%dT%H%M%S%fZ")


def _save_low_confidence_frame(
    frame: np.ndarray,
    detections: list[Detection],
    output_dir: Path,
    timestamp_factory: Callable[[], str],
) -> None:
    if not detections:
        return
    weakest = min(detections, key=lambda detection: detection.confidence)
    if weakest.confidence >= _REVIEW_CONFIDENCE:
        return
    output_dir.mkdir(parents=True, exist_ok=True)
    filename = f"{timestamp_factory()}_{weakest.class_name}_{weakest.confidence:.3f}.jpg"
    target = output_dir / filename
    if not cv2.imwrite(str(target), frame):
        raise ValueError(f"unable to save low-confidence frame: {target}")


def run_webcam(
    config: WebcamConfig,
    source_factory: Callable[[int, int], Any] | None = None,
    detector_factory: Callable[[Path, float, str, int], Any] | None = None,
    *,
    display_factory: Callable[[], Any] | None = None,
    clock: Callable[[], float] = monotonic,
    timestamp_factory: Callable[[], str] = _default_timestamp,
) -> int:
    """Run until end-of-stream, q, or Escape and return a stable exit code."""
    if not config.checkpoint.is_file():
        print(f"checkpoint does not exist: {config.checkpoint}", file=sys.stderr)
        return 2
    if source_factory is None:
        source_factory = lambda camera_index, image_size: OpenCVFrameSource(
            camera_index, requested_size=image_size
        )
    if detector_factory is None:
        detector_factory = lambda checkpoint, confidence, device, image_size: UltralyticsDetector(
            checkpoint, confidence, device, image_size
        )
    if display_factory is None:
        display_factory = OpenCVDisplay

    source: Any | None = None
    display: Any | None = None
    exit_code = 0
    try:
        source = source_factory(config.camera_index, config.image_size)
        detector = detector_factory(
            config.checkpoint,
            config.confidence,
            config.device,
            config.image_size,
        )
        display = display_factory()
        fps_counter = RollingFPS(clock=clock)
        while True:
            frame = source.read()
            if frame is None:
                break
            started_at = clock()
            detections = detector.predict(frame)
            inference_ms = max(0.0, (clock() - started_at) * 1000.0)
            annotated = annotate_frame(
                frame,
                detections,
                fps=fps_counter.update(),
                inference_ms=inference_ms,
            )
            if config.save_low_confidence:
                _save_low_confidence_frame(
                    annotated,
                    detections,
                    config.low_confidence_directory,
                    timestamp_factory,
                )
            if display.show(annotated) in {ord("q"), 27}:
                break
    except CameraUnavailableError as error:
        print(error, file=sys.stderr)
        exit_code = 3
    except Exception as error:  # noqa: BLE001 - translate live backend failures to exit 4
        print(error, file=sys.stderr)
        exit_code = 4
    finally:
        if source is not None:
            try:
                source.close()
            except Exception as error:  # noqa: BLE001 - cleanup must not escape the boundary
                print(f"unable to close camera: {error}", file=sys.stderr)
                exit_code = 4
        if display is not None:
            try:
                display.close()
            except Exception as error:  # noqa: BLE001 - cleanup must not escape the boundary
                print(f"unable to close display: {error}", file=sys.stderr)
                exit_code = 4
    return exit_code
