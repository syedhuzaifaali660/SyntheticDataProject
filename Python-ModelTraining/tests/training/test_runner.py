import json
from pathlib import Path

import pytest
import yaml

from pokemon_detector.cli.train import main
from pokemon_detector.training.config import TrainingConfig
from pokemon_detector.training.runner import DatasetNotValidatedError, train


def write_dataset_artifacts(
    dataset_dir: Path,
    *,
    report_payload: dict[str, object] | None = None,
) -> Path:
    dataset_dir.mkdir(parents=True, exist_ok=True)
    dataset_yaml = dataset_dir / "dataset.yaml"
    dataset_yaml.write_text(
        yaml.safe_dump(
            {
                "path": str(dataset_dir),
                "train": "images/train",
                "val": "images/val",
                "test": "images/test",
                "names": {0: "pikachu", 1: "charmander", 2: "squirtle"},
            },
            sort_keys=False,
        ),
        encoding="utf-8",
    )
    payload = (
        report_payload
        if report_payload is not None
        else {
            "passed": True,
            "errors": [],
            "warnings": [],
            "run_id": "run-123",
            "image_count": 10,
            "label_count": 10,
            "object_count_by_class": {"0": 3, "1": 3, "2": 4},
            "negative_image_count": 1,
        }
    )
    (dataset_dir / "validation-report.json").write_text(
        json.dumps(payload, indent=2) + "\n",
        encoding="utf-8",
    )
    return dataset_yaml


class FakeYoloModel:
    def __init__(self, save_dir: Path) -> None:
        self.save_dir = save_dir
        self.received_train_kwargs: dict[str, object] | None = None
        self.trainer = type("Trainer", (), {"save_dir": save_dir})()

    def train(self, **kwargs: object) -> dict[str, object]:
        self.received_train_kwargs = kwargs
        weights_dir = self.save_dir / "weights"
        weights_dir.mkdir(parents=True, exist_ok=True)
        (weights_dir / "best.pt").write_text("checkpoint", encoding="utf-8")
        return {"ok": True}


def make_config(config_path: Path, dataset_yaml: Path, runs_dir: Path, run_name: str = "training-pilot-300") -> TrainingConfig:
    return TrainingConfig.load(
        config_path,
        dataset_override=dataset_yaml,
        run_name_override=run_name,
        runs_dir_override=runs_dir,
    )


def write_train_config(path: Path) -> Path:
    (path.parent / "pyproject.toml").touch()
    path.write_text(
        yaml.safe_dump(
            {
                "model": "models/yolo26n.pt",
                "epochs": 5,
                "patience": 5,
                "image_size": 640,
                "batch": 8,
                "workers": 2,
                "device": "mps",
                "seed": 42,
            },
            sort_keys=False,
        ),
        encoding="utf-8",
    )
    return path


def test_train_uses_backend_save_dir_and_writes_resolved_artifacts(tmp_path: Path) -> None:
    dataset_yaml = write_dataset_artifacts(tmp_path / "processed")
    split_report = {"run_id": "run-123", "image_counts": {"train": 7, "val": 2, "test": 1}}
    (dataset_yaml.parent / "split-report.json").write_text(
        json.dumps(split_report, indent=2) + "\n",
        encoding="utf-8",
    )
    config_path = write_train_config(tmp_path / "train.yaml")
    backend_save_dir = tmp_path / "backend-runs" / "actual-run"
    fake_model = FakeYoloModel(backend_save_dir)
    created_models: list[FakeYoloModel] = []

    def fake_factory(model_name: str) -> FakeYoloModel:
        assert model_name == str((tmp_path / "models" / "yolo26n.pt").resolve())
        created_models.append(fake_model)
        return fake_model

    config = make_config(config_path, dataset_yaml, tmp_path / "requested-runs")

    checkpoint_path = train(config, yolo_factory=fake_factory)

    assert checkpoint_path == backend_save_dir / "weights" / "best.pt"
    assert created_models == [fake_model]
    assert fake_model.received_train_kwargs == {
        "data": str(dataset_yaml.resolve()),
        "epochs": 5,
        "patience": 5,
        "imgsz": 640,
        "batch": 8,
        "nbs": 64,
        "workers": 2,
        "device": "mps",
        "seed": 42,
        "deterministic": True,
        "project": str((tmp_path / "requested-runs").resolve()),
        "name": "training-pilot-300",
    }
    resolved_config = yaml.safe_load((backend_save_dir / "resolved-config.yaml").read_text(encoding="utf-8"))
    assert resolved_config == {
        "config_path": str(config_path.resolve()),
        "dataset_yaml": str(dataset_yaml.resolve()),
        "run_name": "training-pilot-300",
        "runs_dir": str((tmp_path / "requested-runs").resolve()),
        "model": str((tmp_path / "models" / "yolo26n.pt").resolve()),
        "epochs": 5,
        "patience": 5,
        "image_size": 640,
        "batch": 8,
        "workers": 2,
        "device": "mps",
        "seed": 42,
        "nominal_batch_size": 64,
        "training_augmentation": True,
    }
    environment = json.loads((backend_save_dir / "environment.json").read_text(encoding="utf-8"))
    assert environment["python_version"]
    assert environment["platform"]
    assert isinstance(environment["mps_available"], bool)
    assert environment["ultralytics_version"]
    assert environment["torch_version"]
    archived_split_report = json.loads(
        (backend_save_dir / "split-report.json").read_text(encoding="utf-8")
    )
    assert archived_split_report == split_report


@pytest.mark.parametrize(
    ("payload", "message"),
    [
        ({}, "validation-report.json is missing required fields"),
        ({"passed": False, "errors": []}, "validation report contradicts failed status without errors"),
        ({"passed": True, "errors": ["bad"]}, "validation report contradicts passed status with errors"),
        ({"passed": False, "errors": [{"code": "BAD"}]}, "dataset validation failed"),
    ],
)
def test_train_rejects_invalid_validation_evidence_before_constructing_yolo(
    tmp_path: Path,
    payload: dict[str, object],
    message: str,
) -> None:
    dataset_dir = tmp_path / "processed"
    dataset_yaml = write_dataset_artifacts(dataset_dir, report_payload=payload)
    config = make_config(
        write_train_config(tmp_path / "train.yaml"),
        dataset_yaml,
        tmp_path / "runs",
    )
    factory_calls: list[str] = []

    def fake_factory(model_name: str) -> FakeYoloModel:
        factory_calls.append(model_name)
        return FakeYoloModel(tmp_path / "should-not-exist")

    with pytest.raises(DatasetNotValidatedError, match=message):
        train(config, yolo_factory=fake_factory)

    assert factory_calls == []


def test_train_rejects_missing_validation_report_before_constructing_yolo(tmp_path: Path) -> None:
    dataset_dir = tmp_path / "processed"
    dataset_dir.mkdir()
    dataset_yaml = dataset_dir / "dataset.yaml"
    dataset_yaml.write_text("path: /tmp/example\n", encoding="utf-8")
    config = make_config(
        write_train_config(tmp_path / "train.yaml"),
        dataset_yaml,
        tmp_path / "runs",
    )

    with pytest.raises(DatasetNotValidatedError, match="validation-report.json is missing"):
        train(config, yolo_factory=lambda _: pytest.fail("YOLO factory must not be called"))


def test_train_cli_accepts_dataset_config_and_run_name(tmp_path: Path) -> None:
    dataset_yaml = write_dataset_artifacts(tmp_path / "processed")
    config_path = write_train_config(tmp_path / "train.yaml")
    backend_save_dir = tmp_path / "cli-runs" / "cli-run"

    def fake_factory(model_name: str) -> FakeYoloModel:
        assert model_name == str((tmp_path / "models" / "yolo26n.pt").resolve())
        return FakeYoloModel(backend_save_dir)

    exit_code = main(
        [
            "--dataset",
            str(dataset_yaml),
            "--config",
            str(config_path),
            "--run-name",
            "baseline-3900",
        ],
        yolo_factory=fake_factory,
    )

    assert exit_code == 0
    assert (backend_save_dir / "weights" / "best.pt").is_file()


def test_train_disables_geometric_and_color_augmentation_when_configured(tmp_path: Path) -> None:
    dataset_yaml = write_dataset_artifacts(tmp_path / "processed")
    config_path = write_train_config(tmp_path / "train.yaml")
    payload = yaml.safe_load(config_path.read_text(encoding="utf-8"))
    payload["training_augmentation"] = False
    config_path.write_text(yaml.safe_dump(payload, sort_keys=False), encoding="utf-8")
    fake_model = FakeYoloModel(tmp_path / "runs" / "overfit")
    config = make_config(config_path, dataset_yaml, tmp_path / "runs", "overfit-20")

    train(config, yolo_factory=lambda _: fake_model)

    assert fake_model.received_train_kwargs is not None
    assert {
        key: fake_model.received_train_kwargs[key]
        for key in ("hsv_h", "hsv_s", "hsv_v", "translate", "scale", "fliplr", "mosaic")
    } == {
        "hsv_h": 0.0,
        "hsv_s": 0.0,
        "hsv_v": 0.0,
        "translate": 0.0,
        "scale": 0.0,
        "fliplr": 0.0,
        "mosaic": 0.0,
    }
