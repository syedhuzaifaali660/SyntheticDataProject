"""Ultralytics adapter for live Pokemon detections."""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass
from math import ceil, floor, isfinite
from pathlib import Path
from typing import Any

import numpy as np

from pokemon_detector.domain import PokemonClass


@dataclass(frozen=True, slots=True)
class Detection:
    class_id: int
    class_name: str
    confidence: float
    xyxy: tuple[int, int, int, int]

    def __post_init__(self) -> None:
        try:
            pokemon_class = PokemonClass(self.class_id)
        except (TypeError, ValueError) as error:
            raise ValueError(f"unsupported class ID: {self.class_id}") from error
        expected_name = pokemon_class.name.lower()
        if self.class_name != expected_name:
            raise ValueError(f"class_name must be {expected_name!r} for class_id {self.class_id}")
        if not isfinite(self.confidence) or not 0.0 <= self.confidence <= 1.0:
            raise ValueError("confidence must be finite and between 0 and 1")
        left, top, right, bottom = self.xyxy
        if right <= left or bottom <= top:
            raise ValueError("xyxy must have positive width and height")


def _plain_list(value: Any) -> list[Any]:
    if hasattr(value, "detach"):
        value = value.detach()
    if hasattr(value, "cpu"):
        value = value.cpu()
    if hasattr(value, "tolist"):
        return value.tolist()
    return list(value)


class UltralyticsDetector:
    """Convert Ultralytics predictions into immutable live detections."""

    def __init__(
        self,
        checkpoint: Path,
        confidence: float,
        device: str,
        image_size: int = 640,
        *,
        yolo_factory: Callable[[str], Any] | None = None,
    ) -> None:
        checkpoint = checkpoint.resolve()
        if not checkpoint.is_file():
            raise ValueError(f"checkpoint does not exist: {checkpoint}")
        if not isfinite(confidence) or not 0.0 < confidence <= 1.0:
            raise ValueError("confidence must be greater than 0 and at most 1")
        if image_size <= 0:
            raise ValueError("image_size must be positive")
        if device not in {"cpu", "mps"}:
            raise ValueError("device must be one of: cpu, mps")
        if yolo_factory is None:
            from ultralytics import YOLO

            yolo_factory = YOLO
        self._model = yolo_factory(str(checkpoint))
        self._confidence = confidence
        self._device = device
        self._image_size = image_size

    def predict(self, frame: np.ndarray) -> list[Detection]:
        results = self._model.predict(
            frame,
            conf=self._confidence,
            imgsz=self._image_size,
            device=self._device,
            verbose=False,
        )
        if not results:
            return []
        result = results[0]
        boxes = getattr(result, "boxes", None)
        if boxes is None:
            return []
        coordinates = _plain_list(boxes.xyxy)
        classes = _plain_list(boxes.cls)
        confidences = _plain_list(boxes.conf)
        frame_height, frame_width = frame.shape[:2]
        detections: list[Detection] = []
        for coordinates_row, class_value, confidence in zip(
            coordinates, classes, confidences, strict=True
        ):
            class_id = int(class_value)
            try:
                expected_name = PokemonClass(class_id).name.lower()
            except ValueError as error:
                raise ValueError(f"model returned unsupported class ID: {class_id}") from error
            names = result.names
            model_name = names[class_id]
            if model_name != expected_name:
                raise ValueError(f"model taxonomy mismatch for class {class_id}: {model_name!r}")
            left, top, right, bottom = (float(value) for value in coordinates_row)
            pixel_box = (
                max(0, min(frame_width, floor(left))),
                max(0, min(frame_height, floor(top))),
                max(0, min(frame_width, ceil(right))),
                max(0, min(frame_height, ceil(bottom))),
            )
            if pixel_box[2] <= pixel_box[0] or pixel_box[3] <= pixel_box[1]:
                continue
            detections.append(
                Detection(
                    class_id,
                    expected_name,
                    float(confidence),
                    pixel_box,
                )
            )
        return detections
