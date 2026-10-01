"""Ultralytics validation adapter with provenance-preserving reports."""

from __future__ import annotations

import json
import shutil
from collections.abc import Callable
from dataclasses import asdict, dataclass
from itertools import pairwise
from pathlib import Path
from typing import Any

import yaml

from pokemon_detector.domain import PokemonClass
from pokemon_detector.evaluation.gallery import PredictionBox, build_failure_gallery

_CLASS_NAMES = {0: "pikachu", 1: "charmander", 2: "squirtle"}


@dataclass(frozen=True, slots=True)
class PerClassEvaluation:
    class_id: int
    class_name: str
    precision: float
    recall: float
    f1: float
    map50: float
    map50_95: float
    true_positives: int
    false_positives: int
    false_negatives: int


@dataclass(frozen=True, slots=True)
class EvaluationReport:
    kind: str
    split: str
    checkpoint: str
    dataset_yaml: str
    overall: dict[str, float]
    per_class: list[PerClassEvaluation]

    def to_dict(self) -> dict[str, object]:
        return {
            "kind": self.kind,
            "split": self.split,
            "checkpoint": self.checkpoint,
            "dataset_yaml": self.dataset_yaml,
            "overall": self.overall,
            "per_class": [asdict(metric) for metric in self.per_class],
        }


def _infer_kind(dataset_yaml: Path) -> str:
    parts = dataset_yaml.resolve().parts
    has_prepared_root = any(
        first == "data" and second == "prepared" for first, second in pairwise(parts)
    )
    if not has_prepared_root:
        raise ValueError("dataset path must be beneath data/prepared")
    return "synthetic"


def _load_dataset(dataset_yaml: Path) -> dict[str, Any]:
    try:
        payload = yaml.safe_load(dataset_yaml.read_text(encoding="utf-8"))
    except yaml.YAMLError as error:
        raise ValueError(f"dataset YAML is invalid: {dataset_yaml}") from error
    names = payload.get("names") if isinstance(payload, dict) else None
    if (
        not isinstance(names, dict)
        or set(names) != set(_CLASS_NAMES)
        or any(type(class_id) is not int for class_id in names)
        or any(type(name) is not str for name in names.values())
        or names != _CLASS_NAMES
    ):
        raise ValueError("dataset names must be exactly 0=pikachu, 1=charmander, 2=squirtle")
    return payload


def _confusion_counts(metrics: Any, class_id: int) -> tuple[int, int, int]:
    matrix = metrics.confusion_matrix.matrix
    true_positives = int(matrix[class_id][class_id])
    false_positives = int(sum(matrix[class_id])) - true_positives
    false_negatives = int(sum(row[class_id] for row in matrix)) - true_positives
    return true_positives, false_positives, false_negatives


def _adapt_class_metrics(metrics: Any) -> list[PerClassEvaluation]:
    present = {int(class_id): position for position, class_id in enumerate(metrics.box.ap_class_index)}
    adapted: list[PerClassEvaluation] = []
    for class_id in PokemonClass:
        if int(class_id) in present:
            precision, recall, map50, map50_95 = metrics.box.class_result(present[int(class_id)])
            f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0.0
        else:
            precision = recall = map50 = map50_95 = f1 = 0.0
        true_positives, false_positives, false_negatives = _confusion_counts(metrics, int(class_id))
        adapted.append(
            PerClassEvaluation(
                class_id=int(class_id),
                class_name=_CLASS_NAMES[int(class_id)],
                precision=float(precision),
                recall=float(recall),
                f1=float(f1),
                map50=float(map50),
                map50_95=float(map50_95),
                true_positives=true_positives,
                false_positives=false_positives,
                false_negatives=false_negatives,
            )
        )
    return adapted


def _copy_confusion_artifacts(metrics: Any, output_dir: Path) -> None:
    matrix = getattr(metrics, "confusion_matrix", None)
    if matrix is not None and hasattr(matrix, "plot"):
        matrix.plot(save_dir=output_dir, normalize=True)
    source_dir = Path(getattr(metrics, "save_dir", output_dir))
    for source_name, target_name in {
        "confusion_matrix.png": "confusion-matrix.png",
        "confusion_matrix_normalized.png": "confusion-matrix-normalized.png",
    }.items():
        generated_artifact = output_dir / source_name
        target = output_dir / target_name
        if generated_artifact.is_file():
            generated_artifact.replace(target)
            continue
        source = source_dir / source_name
        if source.is_file():
            shutil.copy2(source, target)


def _is_beneath(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
    except ValueError:
        return False
    return True


def _paths_overlap(first: Path, second: Path) -> bool:
    return _is_beneath(first, second) or _is_beneath(second, first)


def _resolve_split_images(dataset_yaml: Path, payload: dict[str, Any], split: str) -> Path:
    root_value = payload.get("path")
    train_value = payload.get("train")
    split_value = payload.get(split)
    if not isinstance(root_value, str) or not isinstance(train_value, str) or not isinstance(split_value, str):
        raise TypeError("dataset YAML must provide string path and split image directory values")
    root = Path(root_value)
    if not root.is_absolute():
        root = dataset_yaml.parent / root
    root = root.resolve()
    canonical_root = dataset_yaml.parent.resolve()
    if root != canonical_root:
        raise ValueError("dataset root must be the inferred canonical provenance root")
    images_dir = (root / split_value).resolve()
    training_dir = (root / train_value).resolve()
    if not _is_beneath(images_dir, canonical_root) or not _is_beneath(training_dir, canonical_root):
        raise ValueError("selected and training splits must remain beneath the prepared dataset root")
    if _paths_overlap(images_dir, training_dir):
        raise ValueError("selected evaluation split must be disjoint from training split")
    if not images_dir.is_dir():
        raise ValueError(f"dataset split image directory does not exist: {images_dir}")
    if not training_dir.is_dir():
        raise ValueError(f"dataset training image directory does not exist: {training_dir}")
    return images_dir


def _prediction_boxes(model: Any, image_path: Path) -> list[PredictionBox]:
    results = model(str(image_path))
    if not results:
        return []
    boxes = results[0].boxes
    return [
        PredictionBox(
            class_id=int(class_id),
            center_x=float(coordinates[0]),
            center_y=float(coordinates[1]),
            width=float(coordinates[2]),
            height=float(coordinates[3]),
            confidence=float(confidence),
        )
        for class_id, coordinates, confidence in zip(boxes.cls, boxes.xywhn, boxes.conf, strict=True)
    ]


def evaluate_dataset(
    checkpoint: Path,
    dataset_yaml: Path,
    split: str,
    *,
    output_root: Path | None = None,
    yolo_factory: Callable[[Path], Any] | None = None,
    gallery_limit: int = 50,
) -> EvaluationReport:
    """Validate one held-out synthetic dataset and persist an isolated JSON report."""
    checkpoint = checkpoint.resolve()
    dataset_yaml = dataset_yaml.resolve()
    if not checkpoint.is_file():
        raise ValueError(f"checkpoint does not exist: {checkpoint}")
    if not dataset_yaml.is_file():
        raise ValueError(f"dataset YAML does not exist: {dataset_yaml}")
    if split not in {"val", "test"}:
        raise ValueError("evaluation split must be val or test")
    kind = _infer_kind(dataset_yaml)
    dataset_payload = _load_dataset(dataset_yaml)
    images_dir = _resolve_split_images(dataset_yaml, dataset_payload, split)
    if yolo_factory is None:
        from ultralytics import YOLO

        yolo_factory = YOLO
    model = yolo_factory(checkpoint)
    metrics = model.val(data=str(dataset_yaml), split=split)
    results = metrics.results_dict
    overall = {
        "precision": float(results["metrics/precision(B)"]),
        "recall": float(results["metrics/recall(B)"]),
        "map50": float(results["metrics/mAP50(B)"]),
        "map50_95": float(results["metrics/mAP50-95(B)"]),
    }
    report = EvaluationReport(
        kind=kind,
        split=split,
        checkpoint=str(checkpoint),
        dataset_yaml=str(dataset_yaml),
        overall=overall,
        per_class=_adapt_class_metrics(metrics),
    )
    default_root = (
        checkpoint.parent.parent / "evaluation"
        if checkpoint.parent.name == "weights"
        else dataset_yaml.parent / "evaluation"
    )
    evaluation_root = (output_root or default_root).resolve()
    output_dir = evaluation_root / split
    output_dir.mkdir(parents=True, exist_ok=True)
    report_path = output_dir / "report.json"
    report_path.write_text(json.dumps(report.to_dict(), indent=2) + "\n", encoding="utf-8")
    _copy_confusion_artifacts(metrics, output_dir)
    build_failure_gallery(
        images_dir,
        output_dir / "failures",
        lambda image_path: _prediction_boxes(model, image_path),
        limit=gallery_limit,
    )
    return report
