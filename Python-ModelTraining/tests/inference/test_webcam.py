from __future__ import annotations

from pathlib import Path
from typing import ClassVar

import cv2
import numpy as np
import pytest

import pokemon_detector.inference.webcam as webcam_module
from pokemon_detector.cli.webcam import main as webcam_main
from pokemon_detector.inference.detector import Detection, UltralyticsDetector
from pokemon_detector.inference.frame_source import CameraUnavailableError, OpenCVFrameSource
from pokemon_detector.inference.webcam import (
    OpenCVDisplay,
    WebcamConfig,
    annotate_frame,
    run_webcam,
)


class StepClock:
    def __init__(self, step: float = 0.01) -> None:
        self._value = -step
        self._step = step

    def __call__(self) -> float:
        self._value += self._step
        return self._value


class FakeSource:
    def __init__(self, frames: list[np.ndarray]) -> None:
        self._frames = iter(frames)
        self.close_count = 0

    def read(self) -> np.ndarray | None:
        return next(self._frames, None)

    def close(self) -> None:
        self.close_count += 1


class FakeDetector:
    def __init__(self, detections: list[Detection], *, error: Exception | None = None) -> None:
        self.detections = detections
        self.error = error
        self.prediction_count = 0

    def predict(self, frame: np.ndarray) -> list[Detection]:
        self.prediction_count += 1
        if self.error is not None:
            raise self.error
        return self.detections


class FakeDisplay:
    def __init__(self, keys: list[int] | None = None) -> None:
        self._keys = iter(keys or [])
        self.frames: list[np.ndarray] = []
        self.close_count = 0

    def show(self, frame: np.ndarray) -> int:
        self.frames.append(frame.copy())
        return next(self._keys, -1)

    def close(self) -> None:
        self.close_count += 1


class WindowTrackingBackend:
    def __init__(self) -> None:
        self.window_created = False
        self.destroy_count = 0

    def imshow(self, window_name: str, frame: np.ndarray) -> None:
        self.window_created = True

    def waitKey(self, delay: int) -> int:
        return -1

    def destroyWindow(self, window_name: str) -> None:
        if not self.window_created:
            raise RuntimeError("window was never created")
        self.destroy_count += 1


def make_config(checkpoint: Path, **overrides: object) -> WebcamConfig:
    values: dict[str, object] = {
        "checkpoint": checkpoint,
        "camera_index": 0,
        "confidence": 0.5,
        "image_size": 640,
        "device": "cpu",
        "save_low_confidence": False,
        "low_confidence_directory": checkpoint.parent / "review",
    }
    values.update(overrides)
    return WebcamConfig(**values)  # type: ignore[arg-type]


def test_webcam_config_loads_project_relative_paths_and_planned_defaults(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.chdir(tmp_path)
    checkpoint = tmp_path / "runs" / "baseline" / "weights" / "best.pt"
    checkpoint.parent.mkdir(parents=True)
    checkpoint.touch()
    config_path = tmp_path / "configs" / "webcam.yaml"
    config_path.parent.mkdir()
    (tmp_path / "pyproject.toml").touch()
    config_path.write_text(
        "checkpoint: runs/baseline/weights/best.pt\n"
        "camera_index: 0\n"
        "confidence: 0.50\n"
        "image_size: 640\n"
        "device: mps\n"
        "save_low_confidence: false\n"
        "low_confidence_directory: runs/webcam-review\n",
        encoding="utf-8",
    )

    config = WebcamConfig.load(config_path)

    assert config.checkpoint == checkpoint
    assert config.camera_index == 0
    assert config.confidence == 0.5
    assert config.image_size == 640
    assert config.device == "mps"
    assert config.save_low_confidence is False
    assert config.low_confidence_directory == tmp_path / "runs" / "webcam-review"


def test_webcam_config_paths_are_independent_of_caller_working_directory(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    project_root = tmp_path / "project"
    checkpoint = project_root / "runs" / "baseline" / "weights" / "best.pt"
    checkpoint.parent.mkdir(parents=True)
    checkpoint.touch()
    config_path = project_root / "configs" / "webcam.yaml"
    config_path.parent.mkdir()
    (project_root / "pyproject.toml").touch()
    config_path.write_text(
        "checkpoint: runs/baseline/weights/best.pt\n"
        "camera_index: 0\nconfidence: 0.5\nimage_size: 640\ndevice: cpu\n"
        "save_low_confidence: false\n"
        "low_confidence_directory: runs/webcam-review\n",
        encoding="utf-8",
    )
    elsewhere = tmp_path / "elsewhere"
    elsewhere.mkdir()
    monkeypatch.chdir(elsewhere)

    config = WebcamConfig.load(config_path)

    assert config.checkpoint == checkpoint
    assert config.low_confidence_directory == project_root / "runs" / "webcam-review"


def test_committed_webcam_config_contains_the_planned_defaults() -> None:
    project_root = Path(__file__).resolve().parents[2]

    config = WebcamConfig.load(project_root / "configs" / "inference" / "webcam.yaml")

    assert config.checkpoint == (project_root / "runs" / "baseline-3900" / "weights" / "best.pt")
    assert config.camera_index == 0
    assert config.confidence == 0.5
    assert config.image_size == 640
    assert config.device == "mps"
    assert config.save_low_confidence is False
    assert config.low_confidence_directory == project_root / "runs" / "webcam-review"


def test_webcam_loop_predicts_and_displays_three_annotated_frames(tmp_path: Path) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    frames = [np.zeros((40, 60, 3), dtype=np.uint8) for _ in range(3)]
    source = FakeSource(frames)
    detector = FakeDetector([Detection(0, "pikachu", 0.91, (5, 6, 30, 35))])
    display = FakeDisplay()

    exit_code = run_webcam(
        make_config(checkpoint),
        source_factory=lambda camera_index, image_size: source,
        detector_factory=lambda checkpoint, confidence, device, image_size: detector,
        display_factory=lambda: display,
        clock=StepClock(),
    )

    assert exit_code == 0
    assert detector.prediction_count == 3
    assert len(display.frames) == 3
    assert np.count_nonzero(display.frames[0]) > 0
    assert source.close_count == 1
    assert display.close_count == 1


def test_overlay_draws_detection_box_and_all_required_text(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    rectangles: list[tuple[tuple[int, int], tuple[int, int], tuple[int, int, int], int]] = []
    texts: list[str] = []
    real_rectangle = cv2.rectangle
    real_put_text = cv2.putText

    def record_rectangle(
        image: np.ndarray,
        first: tuple[int, int],
        second: tuple[int, int],
        color: tuple[int, int, int],
        thickness: int,
    ) -> np.ndarray:
        rectangles.append((first, second, color, thickness))
        return real_rectangle(image, first, second, color, thickness)

    def record_text(
        image: np.ndarray,
        text: str,
        origin: tuple[int, int],
        font: int,
        scale: float,
        color: tuple[int, int, int],
        thickness: int,
        line_type: int,
    ) -> np.ndarray:
        texts.append(text)
        return real_put_text(
            image,
            text,
            origin,
            font,
            scale,
            color,
            thickness,
            line_type,
        )

    monkeypatch.setattr(webcam_module.cv2, "rectangle", record_rectangle)
    monkeypatch.setattr(webcam_module.cv2, "putText", record_text)

    annotated = annotate_frame(
        np.zeros((50, 60, 3), dtype=np.uint8),
        [Detection(0, "pikachu", 0.91, (5, 6, 30, 35))],
        fps=20.0,
        inference_ms=12.3,
    )

    assert rectangles == [((5, 6), (30, 35), (0, 255, 255), 2)]
    assert texts == ["pikachu 0.91", "FPS 20.0 | inference 12.3 ms"]
    assert np.count_nonzero(annotated) > 0


@pytest.mark.parametrize("exit_key", [ord("q"), 27])
def test_webcam_loop_exits_on_q_or_escape(tmp_path: Path, exit_key: int) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    source = FakeSource([np.zeros((20, 20, 3), dtype=np.uint8)] * 3)
    detector = FakeDetector([])
    display = FakeDisplay([exit_key])

    exit_code = run_webcam(
        make_config(checkpoint),
        source_factory=lambda camera_index, image_size: source,
        detector_factory=lambda checkpoint, confidence, device, image_size: detector,
        display_factory=lambda: display,
        clock=StepClock(),
    )

    assert exit_code == 0
    assert detector.prediction_count == 1
    assert source.close_count == 1
    assert display.close_count == 1


def test_immediate_end_of_stream_does_not_destroy_an_uncreated_opencv_window(
    tmp_path: Path,
) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    source = FakeSource([])
    backend = WindowTrackingBackend()

    exit_code = run_webcam(
        make_config(checkpoint),
        source_factory=lambda camera_index, image_size: source,
        detector_factory=lambda checkpoint, confidence, device, image_size: FakeDetector([]),
        display_factory=lambda: OpenCVDisplay(backend=backend),
    )

    assert exit_code == 0
    assert backend.destroy_count == 0
    assert source.close_count == 1


def test_missing_checkpoint_returns_two_before_opening_camera(tmp_path: Path) -> None:
    source_calls = 0

    def source_factory(camera_index: int, image_size: int) -> FakeSource:
        nonlocal source_calls
        source_calls += 1
        return FakeSource([])

    exit_code = run_webcam(
        make_config(tmp_path / "missing.pt"),
        source_factory=source_factory,
        detector_factory=lambda checkpoint, confidence, device, image_size: FakeDetector([]),
        display_factory=FakeDisplay,
    )

    assert exit_code == 2
    assert source_calls == 0


def test_unavailable_camera_returns_three(tmp_path: Path) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()

    def unavailable(camera_index: int, image_size: int) -> FakeSource:
        raise CameraUnavailableError("camera unavailable")

    exit_code = run_webcam(
        make_config(checkpoint),
        source_factory=unavailable,
        detector_factory=lambda checkpoint, confidence, device, image_size: FakeDetector([]),
        display_factory=FakeDisplay,
    )

    assert exit_code == 3


def test_inference_exception_returns_four_and_closes_camera(tmp_path: Path) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    source = FakeSource([np.zeros((20, 20, 3), dtype=np.uint8)])
    detector = FakeDetector([], error=RuntimeError("backend failed"))
    display = FakeDisplay()

    exit_code = run_webcam(
        make_config(checkpoint),
        source_factory=lambda camera_index, image_size: source,
        detector_factory=lambda checkpoint, confidence, device, image_size: detector,
        display_factory=lambda: display,
        clock=StepClock(),
    )

    assert exit_code == 4
    assert source.close_count == 1
    assert display.close_count == 1


def test_low_confidence_saving_is_opt_in_and_names_class_and_timestamp(tmp_path: Path) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    source = FakeSource([np.zeros((30, 30, 3), dtype=np.uint8)])
    detector = FakeDetector([Detection(1, "charmander", 0.55, (2, 2, 20, 25))])
    display = FakeDisplay()
    review_dir = tmp_path / "review"

    exit_code = run_webcam(
        make_config(
            checkpoint,
            save_low_confidence=True,
            low_confidence_directory=review_dir,
        ),
        source_factory=lambda camera_index, image_size: source,
        detector_factory=lambda checkpoint, confidence, device, image_size: detector,
        display_factory=lambda: display,
        clock=StepClock(),
        timestamp_factory=lambda: "20260902T120000000000Z",
    )

    saved = list(review_dir.glob("*.jpg"))
    assert exit_code == 0
    assert [path.name for path in saved] == ["20260902T120000000000Z_charmander_0.550.jpg"]
    assert cv2.imread(str(saved[0])) is not None


def test_low_confidence_saving_does_not_write_when_disabled(tmp_path: Path) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    source = FakeSource([np.zeros((30, 30, 3), dtype=np.uint8)])
    detector = FakeDetector([Detection(1, "charmander", 0.55, (2, 2, 20, 25))])
    display = FakeDisplay()
    review_dir = tmp_path / "review"

    exit_code = run_webcam(
        make_config(
            checkpoint,
            save_low_confidence=False,
            low_confidence_directory=review_dir,
        ),
        source_factory=lambda camera_index, image_size: source,
        detector_factory=lambda checkpoint, confidence, device, image_size: detector,
        display_factory=lambda: display,
        clock=StepClock(),
        timestamp_factory=lambda: "20260902T120000000000Z",
    )

    assert exit_code == 0
    assert not review_dir.exists()


@pytest.mark.parametrize("confidence", [0.65, 0.90])
def test_enabled_review_saving_skips_threshold_and_high_confidence_frames(
    tmp_path: Path,
    confidence: float,
) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    source = FakeSource([np.zeros((30, 30, 3), dtype=np.uint8)])
    detector = FakeDetector([Detection(0, "pikachu", confidence, (2, 2, 20, 25))])
    display = FakeDisplay()
    review_dir = tmp_path / "review"

    exit_code = run_webcam(
        make_config(
            checkpoint,
            save_low_confidence=True,
            low_confidence_directory=review_dir,
        ),
        source_factory=lambda camera_index, image_size: source,
        detector_factory=lambda checkpoint, confidence, device, image_size: detector,
        display_factory=lambda: display,
        clock=StepClock(),
    )

    assert exit_code == 0
    assert not review_dir.exists()


class FakeCapture:
    def __init__(self, opened: bool, frames: list[tuple[bool, np.ndarray | None]]) -> None:
        self.opened = opened
        self.frames = iter(frames)
        self.settings: list[tuple[int, float]] = []
        self.release_count = 0

    def isOpened(self) -> bool:
        return self.opened

    def set(self, property_id: int, value: float) -> bool:
        self.settings.append((property_id, value))
        return True

    def read(self) -> tuple[bool, np.ndarray | None]:
        return next(self.frames, (False, None))

    def release(self) -> None:
        self.release_count += 1


class FailingSetCapture(FakeCapture):
    def set(self, property_id: int, value: float) -> bool:
        raise RuntimeError("camera property failure")


def test_opencv_frame_source_sets_requested_size_reads_and_releases() -> None:
    frame = np.zeros((10, 20, 3), dtype=np.uint8)
    capture = FakeCapture(True, [(True, frame), (False, None)])

    source = OpenCVFrameSource(2, requested_size=640, capture_factory=lambda index: capture)

    assert source.read() is frame
    assert source.read() is None
    source.close()
    assert capture.settings == [
        (cv2.CAP_PROP_FRAME_WIDTH, 640.0),
        (cv2.CAP_PROP_FRAME_HEIGHT, 640.0),
    ]
    assert capture.release_count == 1


def test_opencv_frame_source_releases_and_raises_when_camera_is_unavailable() -> None:
    capture = FakeCapture(False, [])

    with pytest.raises(CameraUnavailableError, match="camera index 4"):
        OpenCVFrameSource(4, capture_factory=lambda index: capture)

    assert capture.release_count == 1


def test_opencv_frame_source_releases_when_post_open_initialization_fails(
    tmp_path: Path,
) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    capture = FailingSetCapture(True, [])

    exit_code = run_webcam(
        make_config(checkpoint),
        source_factory=lambda camera_index, image_size: OpenCVFrameSource(
            camera_index,
            requested_size=image_size,
            capture_factory=lambda index: capture,
        ),
        detector_factory=lambda checkpoint, confidence, device, image_size: FakeDetector([]),
        display_factory=FakeDisplay,
    )

    assert exit_code == 4
    assert capture.release_count == 1


class FakeBoxes:
    xyxy = np.array([[1.0, 2.0, 30.0, 40.0]])
    cls = np.array([2.0])
    conf = np.array([0.875])


class FakeResult:
    boxes = FakeBoxes()
    names: ClassVar[dict[int, str]] = {
        0: "pikachu",
        1: "charmander",
        2: "squirtle",
    }


class FakeYolo:
    def __init__(self) -> None:
        self.calls: list[dict[str, object]] = []

    def predict(self, frame: np.ndarray, **kwargs: object) -> list[FakeResult]:
        self.calls.append(kwargs)
        return [FakeResult()]


class SubpixelBoxes:
    xyxy = np.array(
        [
            [1.1, 2.0, 1.4, 8.0],
            [-2.0, -3.0, 60.0, 70.0],
        ]
    )
    cls = np.array([0.0, 2.0])
    conf = np.array([0.8, 0.9])


class SubpixelResult:
    boxes = SubpixelBoxes()
    names: ClassVar[dict[int, str]] = {
        0: "pikachu",
        1: "charmander",
        2: "squirtle",
    }


class SubpixelYolo:
    def predict(self, frame: np.ndarray, **kwargs: object) -> list[SubpixelResult]:
        return [SubpixelResult()]


def test_ultralytics_detector_converts_boxes_and_uses_exact_predict_settings(
    tmp_path: Path,
) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    model = FakeYolo()
    received_checkpoint: list[str] = []

    def factory(path: str) -> FakeYolo:
        received_checkpoint.append(path)
        return model

    detector = UltralyticsDetector(
        checkpoint,
        confidence=0.5,
        device="cpu",
        image_size=640,
        yolo_factory=factory,
    )

    detections = detector.predict(np.zeros((50, 50, 3), dtype=np.uint8))

    assert received_checkpoint == [str(checkpoint)]
    assert detections == [Detection(2, "squirtle", 0.875, (1, 2, 30, 40))]
    assert model.calls == [{"conf": 0.5, "imgsz": 640, "device": "cpu", "verbose": False}]


def test_ultralytics_detector_preserves_subpixel_boxes_and_clamps_to_frame(
    tmp_path: Path,
) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    detector = UltralyticsDetector(
        checkpoint,
        confidence=0.5,
        device="cpu",
        image_size=640,
        yolo_factory=lambda path: SubpixelYolo(),
    )

    detections = detector.predict(np.zeros((50, 50, 3), dtype=np.uint8))

    assert detections == [
        Detection(0, "pikachu", 0.8, (1, 2, 2, 8)),
        Detection(2, "squirtle", 0.9, (0, 0, 50, 50)),
    ]


def test_cli_loads_config_and_returns_runner_exit_code(tmp_path: Path) -> None:
    checkpoint = tmp_path / "best.pt"
    checkpoint.touch()
    config_path = tmp_path / "webcam.yaml"
    config_path.write_text(
        f"checkpoint: {checkpoint}\n"
        "camera_index: 0\nconfidence: 0.5\nimage_size: 640\ndevice: cpu\n"
        "save_low_confidence: false\n"
        f"low_confidence_directory: {tmp_path / 'review'}\n",
        encoding="utf-8",
    )
    received: list[WebcamConfig] = []

    def runner(config: WebcamConfig) -> int:
        received.append(config)
        return 3

    exit_code = webcam_main(["--config", str(config_path)], runner=runner)

    assert exit_code == 3
    assert received[0].checkpoint == checkpoint
