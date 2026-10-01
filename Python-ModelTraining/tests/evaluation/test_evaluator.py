import json
import subprocess
import sys
from pathlib import Path

import cv2
import numpy as np
import pytest
import yaml

from pokemon_detector.evaluation.evaluator import evaluate_dataset
from pokemon_detector.evaluation.gallery import PredictionBox, build_failure_gallery


def test_evaluate_module_invokes_cli() -> None:
    result = subprocess.run(
        [sys.executable, "-m", "pokemon_detector.cli.evaluate", "--help"],
        check=False,
        capture_output=True,
        text=True,
    )

    assert result.returncode == 0
    assert "--checkpoint" in result.stdout


def test_evaluate_dataset_accepts_canonical_prepared_dataset_path(tmp_path: Path) -> None:
    dataset_yaml = write_dataset_yaml(tmp_path / "data" / "prepared" / "accepted-run")
    checkpoint = tmp_path / "best.pt"
    checkpoint.write_text("weights", encoding="utf-8")

    report = evaluate_dataset(
        checkpoint,
        dataset_yaml,
        "test",
        output_root=tmp_path / "evaluation",
        yolo_factory=lambda path: FakeYolo(path, tmp_path / "backend"),
    )

    assert report.kind == "synthetic"


def test_evaluate_dataset_defaults_to_the_checkpoint_run_evaluation_directory(
    tmp_path: Path,
) -> None:
    dataset_yaml = write_dataset_yaml(tmp_path / "data" / "prepared" / "accepted-run")
    run_dir = tmp_path / "runs" / "model-run"
    checkpoint = run_dir / "weights" / "best.pt"
    checkpoint.parent.mkdir(parents=True)
    checkpoint.write_text("weights", encoding="utf-8")

    evaluate_dataset(
        checkpoint,
        dataset_yaml,
        "test",
        yolo_factory=lambda path: FakeYolo(path, tmp_path / "backend"),
    )

    assert (run_dir / "evaluation" / "test" / "report.json").is_file()


class FakeConfusionMatrix:
    def __init__(self) -> None:
        self.matrix = [[2, 0, 1, 0], [0, 3, 0, 0], [0, 0, 0, 1], [1, 0, 0, 0]]

    def plot(self, save_dir: Path, normalize: bool = True, on_plot: object = None) -> None:
        (Path(save_dir) / "confusion_matrix.png").write_bytes(b"matrix")
        (Path(save_dir) / "confusion_matrix_normalized.png").write_bytes(b"normalized")


class FakeMetrics:
    def __init__(self, save_dir: Path) -> None:
        self.results_dict = {
            "metrics/precision(B)": 0.75,
            "metrics/recall(B)": 0.6,
            "metrics/mAP50(B)": 0.7,
            "metrics/mAP50-95(B)": 0.5,
        }
        self.box = self
        self.ap_class_index = [0, 2]
        self.save_dir = save_dir
        self.confusion_matrix = FakeConfusionMatrix()

    def class_result(self, position: int) -> tuple[float, float, float, float]:
        return ((0.8, 0.7, 0.75, 0.55), (0.6, 0.5, 0.55, 0.35))[position]


class FakeYolo:
    def __init__(self, checkpoint: Path, save_dir: Path) -> None:
        self.checkpoint = checkpoint
        self.save_dir = save_dir
        self.val_kwargs: dict[str, object] | None = None

    def val(self, **kwargs: object) -> FakeMetrics:
        self.val_kwargs = kwargs
        return FakeMetrics(self.save_dir)

    def __call__(self, image_path: str) -> list[object]:
        boxes = type(
            "Boxes",
            (),
            {"cls": [0.0], "xywhn": [[0.5, 0.5, 0.4, 0.4]], "conf": [0.9]},
        )()
        return [type("Result", (), {"boxes": boxes})()]


def write_dataset_yaml(
    root: Path,
    *,
    path_value: str | None = None,
    split_values: dict[str, str] | None = None,
    names: object | None = None,
) -> Path:
    root.mkdir(parents=True, exist_ok=True)
    (root / "images" / "train").mkdir(parents=True, exist_ok=True)
    (root / "images" / "val").mkdir(parents=True, exist_ok=True)
    (root / "images" / "test").mkdir(parents=True, exist_ok=True)
    path = root / "dataset.yaml"
    path.write_text(
        yaml.safe_dump(
            {
                "path": path_value if path_value is not None else str(root),
                **(split_values or {"train": "images/train", "val": "images/val", "test": "images/test"}),
                "names": names if names is not None else {0: "pikachu", 1: "charmander", 2: "squirtle"},
            },
            sort_keys=False,
        ),
        encoding="utf-8",
    )
    return path


def test_evaluate_dataset_refuses_a_noncanonical_prepared_directory(tmp_path: Path) -> None:
    dataset_yaml = write_dataset_yaml(tmp_path / "scratch" / "processed")
    checkpoint = tmp_path / "best.pt"
    checkpoint.write_text("weights", encoding="utf-8")

    with pytest.raises(ValueError, match="data/prepared"):
        evaluate_dataset(
            checkpoint,
            dataset_yaml,
            "val",
            yolo_factory=lambda _: pytest.fail("must not construct YOLO"),
        )


@pytest.mark.parametrize(
    ("path_value", "split_values", "message"),
    [
        ("../../outside", None, "canonical provenance root"),
        (
            None,
            {
                "train": "images/train",
                "val": "../../outside/images/val",
                "test": "images/test",
            },
            "beneath the prepared dataset root",
        ),
        (
            None,
            {"train": "images/train", "val": "images/../images/train", "test": "images/test"},
            "disjoint",
        ),
    ],
)
def test_evaluate_dataset_refuses_resolved_cross_provenance_or_training_split_before_factory(
    tmp_path: Path,
    path_value: str | None,
    split_values: dict[str, str] | None,
    message: str,
) -> None:
    processed = tmp_path / "data" / "prepared" / "run"
    outside = tmp_path / "outside"
    write_dataset_yaml(outside)
    dataset_yaml = write_dataset_yaml(processed, path_value=path_value, split_values=split_values)
    checkpoint = tmp_path / "best.pt"
    checkpoint.write_text("weights", encoding="utf-8")

    with pytest.raises(ValueError, match=message):
        evaluate_dataset(checkpoint, dataset_yaml, "val", yolo_factory=lambda _: pytest.fail("factory called"))


def test_evaluate_dataset_refuses_absolute_cross_provenance_split_before_factory(
    tmp_path: Path,
) -> None:
    processed = tmp_path / "data" / "prepared" / "run"
    outside = tmp_path / "outside"
    write_dataset_yaml(outside)
    dataset_yaml = write_dataset_yaml(
        processed,
        split_values={
            "train": "images/train",
            "val": str(outside / "images" / "val"),
            "test": "images/test",
        },
    )
    checkpoint = tmp_path / "best.pt"
    checkpoint.write_text("weights", encoding="utf-8")

    with pytest.raises(ValueError, match="beneath the prepared dataset root"):
        evaluate_dataset(
            checkpoint,
            dataset_yaml,
            "val",
            yolo_factory=lambda _: pytest.fail("factory called"),
        )


def test_evaluate_dataset_refuses_symlink_equivalent_training_split_before_factory(tmp_path: Path) -> None:
    root = tmp_path / "data" / "prepared" / "run"
    dataset_yaml = write_dataset_yaml(root)
    alias = root / "images" / "test_alias"
    alias.symlink_to(root / "images" / "train", target_is_directory=True)
    dataset_yaml = write_dataset_yaml(
        root,
        split_values={"train": "images/train", "val": "images/test_alias", "test": "images/test"},
    )
    checkpoint = tmp_path / "best.pt"
    checkpoint.write_text("weights", encoding="utf-8")

    with pytest.raises(ValueError, match="disjoint"):
        evaluate_dataset(checkpoint, dataset_yaml, "val", yolo_factory=lambda _: pytest.fail("factory called"))


@pytest.mark.parametrize(
    ("checkpoint", "dataset", "split", "message"),
    [
        ("missing.pt", "dataset.yaml", "val", "checkpoint does not exist"),
        ("best.pt", "missing.yaml", "val", "dataset YAML does not exist"),
        ("best.pt", "dataset.yaml", "train", "val or test"),
    ],
)
def test_evaluate_dataset_refuses_invalid_inputs_before_factory(
    tmp_path: Path, checkpoint: str, dataset: str, split: str, message: str
) -> None:
    root = tmp_path / "data" / "prepared" / "run"
    dataset_yaml = write_dataset_yaml(root)
    (tmp_path / "best.pt").write_text("weights", encoding="utf-8")

    with pytest.raises(ValueError, match=message):
        evaluate_dataset(tmp_path / checkpoint, dataset_yaml if dataset == "dataset.yaml" else tmp_path / dataset, split, yolo_factory=lambda _: pytest.fail("factory called"))


@pytest.mark.parametrize(
    "names",
    [{False: "pikachu", 1: "charmander", 2: "squirtle"}, {0: "Pikachu", 1: "charmander", 2: "squirtle"}],
)
def test_evaluate_dataset_refuses_type_loose_or_noncanonical_taxonomy_before_factory(
    tmp_path: Path, names: object
) -> None:
    dataset_yaml = write_dataset_yaml(tmp_path / "data" / "prepared" / "run", names=names)
    checkpoint = tmp_path / "best.pt"
    checkpoint.write_text("weights", encoding="utf-8")

    with pytest.raises(ValueError, match="names must be exactly"):
        evaluate_dataset(checkpoint, dataset_yaml, "val", yolo_factory=lambda _: pytest.fail("factory called"))


def test_evaluate_dataset_serializes_isolated_report_and_adapts_present_classes(tmp_path: Path) -> None:
    dataset_yaml = write_dataset_yaml(tmp_path / "data" / "prepared" / "run")
    checkpoint = tmp_path / "best.pt"
    checkpoint.write_text("weights", encoding="utf-8")
    models: list[FakeYolo] = []

    def factory(path: Path) -> FakeYolo:
        model = FakeYolo(path, tmp_path / "backend")
        models.append(model)
        return model

    report = evaluate_dataset(
        checkpoint,
        dataset_yaml,
        "test",
        output_root=tmp_path / "evaluations",
        yolo_factory=factory,
    )

    report_path = tmp_path / "evaluations" / "test" / "report.json"
    assert models[0].checkpoint == checkpoint
    assert models[0].val_kwargs == {"data": str(dataset_yaml), "split": "test"}
    assert report_path.is_file()
    assert (report_path.parent / "confusion-matrix.png").read_bytes() == b"matrix"
    assert (report_path.parent / "confusion-matrix-normalized.png").read_bytes() == b"normalized"
    payload = json.loads(report_path.read_text(encoding="utf-8"))
    assert payload["kind"] == "synthetic"
    assert payload["dataset_yaml"] == str(dataset_yaml.resolve())
    assert payload["overall"]["precision"] == 0.75
    assert payload["per_class"] == [
        {"class_id": 0, "class_name": "pikachu", "precision": 0.8, "recall": 0.7, "f1": 0.7466666666666666, "map50": 0.75, "map50_95": 0.55, "true_positives": 2, "false_positives": 1, "false_negatives": 1},
        {"class_id": 1, "class_name": "charmander", "precision": 0.0, "recall": 0.0, "f1": 0.0, "map50": 0.0, "map50_95": 0.0, "true_positives": 3, "false_positives": 0, "false_negatives": 0},
        {"class_id": 2, "class_name": "squirtle", "precision": 0.6, "recall": 0.5, "f1": 0.5454545454545454, "map50": 0.55, "map50_95": 0.35, "true_positives": 0, "false_positives": 1, "false_negatives": 1},
    ]
    assert report.to_dict() == payload


def test_evaluate_dataset_writes_failure_gallery_below_the_isolated_report_directory(tmp_path: Path) -> None:
    dataset_root = tmp_path / "data" / "prepared" / "run"
    dataset_yaml = write_dataset_yaml(dataset_root)
    images_dir = dataset_root / "images" / "val"
    labels_dir = dataset_root / "labels" / "val"
    images_dir.mkdir(parents=True, exist_ok=True)
    labels_dir.mkdir(parents=True)
    cv2.imwrite(str(images_dir / "frame.jpg"), np.zeros((20, 20, 3), dtype=np.uint8))
    (labels_dir / "frame.txt").write_text("0 0.5 0.5 0.4 0.4\n", encoding="utf-8")
    checkpoint = tmp_path / "best.pt"
    checkpoint.write_text("weights", encoding="utf-8")

    evaluate_dataset(
        checkpoint,
        dataset_yaml,
        "val",
        output_root=tmp_path / "evaluations",
        yolo_factory=lambda path: FakeYolo(path, tmp_path / "backend"),
    )

    gallery_dir = tmp_path / "evaluations" / "val" / "failures"
    assert json.loads((gallery_dir / "failures.json").read_text(encoding="utf-8")) == [
        {"image": "frame.jpg", "false_negatives": 0, "lowest_correct_confidence": 0.9}
    ]
    assert np.any(cv2.imread(str(gallery_dir / "frame.jpg")) != 0)


def test_failure_gallery_ranks_missed_images_before_low_confidence_correct_detections(tmp_path: Path) -> None:
    images_dir = tmp_path / "data" / "prepared" / "run" / "images" / "val"
    labels_dir = tmp_path / "data" / "prepared" / "run" / "labels" / "val"
    images_dir.mkdir(parents=True)
    labels_dir.mkdir(parents=True)
    for name, label in {
        "missed.jpg": "0 0.5 0.5 0.4 0.4\n0 0.2 0.2 0.1 0.1\n",
        "low.jpg": "1 0.5 0.5 0.4 0.4\n",
        "fp.jpg": "",
    }.items():
        cv2.imwrite(str(images_dir / name), np.zeros((20, 20, 3), dtype=np.uint8))
        (labels_dir / f"{Path(name).stem}.txt").write_text(label, encoding="utf-8")

    saved = build_failure_gallery(
        images_dir,
        tmp_path / "gallery",
        lambda image: {
            "missed.jpg": [PredictionBox(0, 0.5, 0.5, 0.4, 0.4, 0.9)],
            "low.jpg": [PredictionBox(1, 0.5, 0.5, 0.4, 0.4, 0.2)],
            "fp.jpg": [PredictionBox(2, 0.5, 0.5, 0.4, 0.4, 0.8)],
        }[image.name],
    )

    assert [entry["image"] for entry in saved] == ["missed.jpg", "low.jpg"]
    failures = json.loads((tmp_path / "gallery" / "failures.json").read_text(encoding="utf-8"))
    assert [entry["image"] for entry in failures] == ["missed.jpg", "low.jpg"]
    assert sorted(path.name for path in (tmp_path / "gallery").glob("*.jpg")) == ["low.jpg", "missed.jpg"]


def test_failure_gallery_hard_caps_and_cleans_previous_generated_images(tmp_path: Path) -> None:
    images_dir = tmp_path / "data" / "prepared" / "run" / "images" / "val"
    labels_dir = tmp_path / "data" / "prepared" / "run" / "labels" / "val"
    images_dir.mkdir(parents=True)
    labels_dir.mkdir(parents=True)
    for number in range(51):
        name = f"{number:02d}.jpg"
        cv2.imwrite(str(images_dir / name), np.zeros((10, 10, 3), dtype=np.uint8))
        (labels_dir / f"{number:02d}.txt").write_text("0 0.5 0.5 0.4 0.4\n", encoding="utf-8")
    gallery = tmp_path / "gallery"
    predictor = lambda _: [PredictionBox(0, 0.5, 0.5, 0.4, 0.4, 0.2)]

    build_failure_gallery(images_dir, gallery, predictor, limit=51)
    (labels_dir / "00.txt").write_text("", encoding="utf-8")
    build_failure_gallery(images_dir, gallery, predictor, limit=51)

    assert len(list(gallery.glob("*.jpg"))) == 50
    assert "00.jpg" not in {path.name for path in gallery.glob("*.jpg")}


def test_failure_gallery_matches_by_iou_before_confidence_to_avoid_misses(tmp_path: Path) -> None:
    images_dir = tmp_path / "data" / "prepared" / "run" / "images" / "val"
    labels_dir = tmp_path / "data" / "prepared" / "run" / "labels" / "val"
    images_dir.mkdir(parents=True)
    labels_dir.mkdir(parents=True)
    cv2.imwrite(str(images_dir / "overlap.jpg"), np.zeros((20, 20, 3), dtype=np.uint8))
    (labels_dir / "overlap.txt").write_text(
        "0 0.4 0.5 0.4 0.4\n0 0.5 0.5 0.21 0.4\n",
        encoding="utf-8",
    )

    saved = build_failure_gallery(
        images_dir,
        tmp_path / "gallery",
        lambda _: [
            PredictionBox(0, 0.5, 0.5, 0.21, 0.4, 0.99),
            PredictionBox(0, 0.35, 0.5, 0.4, 0.4, 0.1),
        ],
    )

    assert saved == [{"image": "overlap.jpg", "false_negatives": 0, "lowest_correct_confidence": 0.1}]


def test_failure_gallery_clips_predictions_that_extend_beyond_image_edge(tmp_path: Path) -> None:
    images_dir = tmp_path / "data" / "prepared" / "run" / "images" / "test"
    labels_dir = tmp_path / "data" / "prepared" / "run" / "labels" / "test"
    images_dir.mkdir(parents=True)
    labels_dir.mkdir(parents=True)
    cv2.imwrite(str(images_dir / "edge.jpg"), np.zeros((20, 20, 3), dtype=np.uint8))
    (labels_dir / "edge.txt").write_text("2 0.98 0.5 0.04 0.4\n", encoding="utf-8")

    saved = build_failure_gallery(
        images_dir,
        tmp_path / "gallery",
        lambda _: [PredictionBox(2, 0.99, 0.5, 0.04, 0.4, 0.8)],
    )

    assert saved == [
        {"image": "edge.jpg", "false_negatives": 0, "lowest_correct_confidence": 0.8}
    ]
