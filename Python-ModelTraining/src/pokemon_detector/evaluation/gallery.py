"""Deterministic failure artifact creation for held-out YOLO image splits."""

from __future__ import annotations

import json
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path

from pokemon_detector.dataset.parser import parse_yolo_label
from pokemon_detector.domain import YoloBox


@dataclass(frozen=True, slots=True)
class PredictionBox:
    class_id: int
    center_x: float
    center_y: float
    width: float
    height: float
    confidence: float

    def as_yolo(self) -> YoloBox:
        left = max(0.0, self.center_x - self.width / 2)
        top = max(0.0, self.center_y - self.height / 2)
        right = min(1.0, self.center_x + self.width / 2)
        bottom = min(1.0, self.center_y + self.height / 2)
        if right <= left or bottom <= top:
            raise ValueError("prediction box must overlap the normalized image")
        return YoloBox(
            self.class_id,
            (left + right) / 2,
            (top + bottom) / 2,
            right - left,
            bottom - top,
        )


def _iou(first: YoloBox, second: YoloBox) -> float:
    first_left, first_top = first.center_x - first.width / 2, first.center_y - first.height / 2
    first_right, first_bottom = first.center_x + first.width / 2, first.center_y + first.height / 2
    second_left, second_top = second.center_x - second.width / 2, second.center_y - second.height / 2
    second_right, second_bottom = second.center_x + second.width / 2, second.center_y + second.height / 2
    overlap = max(0.0, min(first_right, second_right) - max(first_left, second_left)) * max(0.0, min(first_bottom, second_bottom) - max(first_top, second_top))
    union = first.width * first.height + second.width * second.height - overlap
    return overlap / union if union else 0.0


def _label_path(image_path: Path) -> Path:
    parts = list(image_path.parts)
    try:
        position = parts.index("images")
    except ValueError as error:
        raise ValueError(f"gallery image path must be under an images directory: {image_path}") from error
    return Path(*parts[:position], "labels", *parts[position + 1:]).with_suffix(".txt")


def _pixel_rect(box: YoloBox, width: int, height: int) -> tuple[tuple[int, int], tuple[int, int]]:
    left = round((box.center_x - box.width / 2) * width)
    top = round((box.center_y - box.height / 2) * height)
    right = round((box.center_x + box.width / 2) * width)
    bottom = round((box.center_y + box.height / 2) * height)
    return (left, top), (right, bottom)


def _render_overlay(image_path: Path, target: Path, truths: list[YoloBox], predictions: list[PredictionBox]) -> None:
    import cv2

    image = cv2.imread(str(image_path))
    if image is None:
        raise ValueError(f"unable to read gallery image: {image_path}")
    height, width = image.shape[:2]
    for truth in truths:
        cv2.rectangle(image, *_pixel_rect(truth, width, height), (0, 255, 0), 1)
    for prediction in predictions:
        cv2.rectangle(image, *_pixel_rect(prediction.as_yolo(), width, height), (0, 0, 255), 1)
    temporary = target.with_name(f".{target.stem}.tmp{target.suffix}")
    if not cv2.imwrite(str(temporary), image):
        raise ValueError(f"unable to write gallery image: {temporary}")
    temporary.replace(target)


def build_failure_gallery(
    images_dir: Path,
    output_dir: Path,
    predictor: Callable[[Path], list[PredictionBox]],
    *,
    limit: int = 50,
) -> list[dict[str, object]]:
    """Render a bounded, deterministic set of held-out missed/weak detections."""
    if limit < 1:
        raise ValueError("gallery limit must be at least one")
    records: list[tuple[tuple[object, ...], Path, list[YoloBox], list[PredictionBox], dict[str, object]]] = []
    for image_path in sorted(path for path in images_dir.iterdir() if path.is_file()):
        truths = parse_yolo_label(_label_path(image_path)) if _label_path(image_path).is_file() else []
        predictions = predictor(image_path)
        candidates = [
            (_iou(truth, prediction.as_yolo()), prediction.confidence, truth_index, prediction_index)
            for truth_index, truth in enumerate(truths)
            for prediction_index, prediction in enumerate(predictions)
            if truth.class_id == prediction.class_id and _iou(truth, prediction.as_yolo()) >= 0.5
        ]
        used_truths: set[int] = set()
        used_predictions: set[int] = set()
        matches: list[PredictionBox] = []
        for _, _, truth_index, prediction_index in sorted(
            candidates, key=lambda item: (-item[0], -item[1], item[2], item[3])
        ):
            if truth_index not in used_truths and prediction_index not in used_predictions:
                used_truths.add(truth_index)
                used_predictions.add(prediction_index)
                matches.append(predictions[prediction_index])
        missed = len(truths) - len(used_truths)
        if missed:
            payload = {"image": image_path.name, "false_negatives": missed, "lowest_correct_confidence": None}
            records.append(((0, -missed, image_path.name), image_path, truths, predictions, payload))
        elif matches:
            confidence = min(prediction.confidence for prediction in matches)
            payload = {"image": image_path.name, "false_negatives": 0, "lowest_correct_confidence": confidence}
            records.append(((1, confidence, image_path.name), image_path, truths, predictions, payload))
    output_dir.mkdir(parents=True, exist_ok=True)
    for path in output_dir.iterdir():
        if path.is_file() or path.is_symlink():
            path.unlink()
    selected = sorted(records, key=lambda item: item[0])[: min(limit, 50)]
    saved: list[dict[str, object]] = []
    for _, image_path, truths, predictions, payload in selected:
        _render_overlay(image_path, output_dir / image_path.name, truths, predictions)
        saved.append(payload)
    (output_dir / "failures.json").write_text(json.dumps(saved, indent=2) + "\n", encoding="utf-8")
    return saved
