from pathlib import Path

import pytest
import yaml

from pokemon_detector.training.config import TrainingConfig

PROJECT_ROOT = Path(__file__).resolve().parents[2]
OCCLUSION_SAFE_DATASET = (
    PROJECT_ROOT / "data/prepared/mixed-device-6000-640-occlusion-safe-0907/dataset.yaml"
).resolve()


def write_config(
    path: Path,
    *,
    model: object = "models/yolo26n.pt",
    epochs: object = 5,
    patience: object = 5,
    image_size: object = 640,
    batch: object = 8,
    workers: object = 2,
    device: object = "mps",
    seed: object = 42,
    nominal_batch_size: object = 64,
    training_augmentation: object = True,
    dataset_yaml: object | None = None,
    run_name: object | None = None,
    runs_dir: object | None = None,
) -> Path:
    project_root = path.parent
    for parent in path.parents:
        if parent.name == "configs":
            project_root = parent.parent
            break
    (project_root / "pyproject.toml").touch()
    payload: dict[str, object] = {
        "model": model,
        "epochs": epochs,
        "patience": patience,
        "image_size": image_size,
        "batch": batch,
        "workers": workers,
        "device": device,
        "seed": seed,
        "nominal_batch_size": nominal_batch_size,
        "training_augmentation": training_augmentation,
    }
    if dataset_yaml is not None:
        payload["dataset_yaml"] = dataset_yaml
    if run_name is not None:
        payload["run_name"] = run_name
    if runs_dir is not None:
        payload["runs_dir"] = runs_dir
    path.write_text(yaml.safe_dump(payload, sort_keys=False), encoding="utf-8")
    return path


def test_training_config_load_merges_overrides_and_resolves_paths(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    project_dir = tmp_path / "project"
    config_dir = project_dir / "configs" / "training"
    config_dir.mkdir(parents=True)
    dataset_dir = tmp_path / "inputs"
    dataset_dir.mkdir()
    dataset_yaml = dataset_dir / "dataset.yaml"
    dataset_yaml.write_text("path: /tmp/example\n", encoding="utf-8")
    config_path = write_config(config_dir / "train-smoke.yaml")
    monkeypatch.chdir(tmp_path)

    config = TrainingConfig.load(
        config_path,
        dataset_override=Path("inputs/dataset.yaml"),
        run_name_override="training-pilot-300",
    )

    assert config.model == (project_dir / "models" / "yolo26n.pt").resolve()
    assert config.epochs == 5
    assert config.patience == 5
    assert config.image_size == 640
    assert config.batch == 8
    assert config.workers == 2
    assert config.device == "mps"
    assert config.seed == 42
    assert config.nominal_batch_size == 64
    assert config.training_augmentation is True
    assert config.dataset_yaml == dataset_yaml.resolve()
    assert config.run_name == "training-pilot-300"
    assert config.runs_dir == (project_dir / "runs").resolve()
    assert config.config_path == config_path.resolve()


@pytest.mark.parametrize(
    ("field_name", "field_value", "message"),
    [
        ("model", "yolo11n.pt", "model must be exactly yolo26n.pt"),
        ("epochs", 0, "epochs must be at least 1"),
        ("patience", 6, "patience must be less than or equal to epochs"),
        ("image_size", 0, "image_size must be positive"),
        ("batch", 0, "batch must be positive"),
        ("nominal_batch_size", 0, "nominal_batch_size must be positive"),
        ("workers", 0, "workers must be positive"),
        ("device", "cuda:0", "device must be one of: cpu, mps"),
        ("run_name", "../escape", "run_name contains unsafe characters"),
    ],
)
def test_training_config_load_rejects_invalid_values(
    tmp_path: Path,
    field_name: str,
    field_value: object,
    message: str,
) -> None:
    config_path = write_config(
        tmp_path / "train.yaml",
        **{field_name: field_value},
    )
    dataset_yaml = tmp_path / "dataset.yaml"
    dataset_yaml.write_text("path: /tmp/example\n", encoding="utf-8")
    kwargs: dict[str, object | None] = {
        "dataset_override": dataset_yaml,
        "run_name_override": "valid-run",
    }
    if field_name == "run_name":
        kwargs["run_name_override"] = None

    with pytest.raises(ValueError, match=message):
        TrainingConfig.load(config_path, **kwargs)


def test_training_config_load_rejects_non_boolean_training_augmentation(tmp_path: Path) -> None:
    config_path = write_config(tmp_path / "train.yaml", training_augmentation="no")
    dataset_yaml = tmp_path / "dataset.yaml"
    dataset_yaml.write_text("path: /tmp/example\n", encoding="utf-8")

    with pytest.raises(TypeError, match="training_augmentation must be a boolean"):
        TrainingConfig.load(
            config_path,
            dataset_override=dataset_yaml,
            run_name_override="valid-run",
        )


def test_training_config_load_requires_dataset_after_override_merge(tmp_path: Path) -> None:
    config_path = write_config(tmp_path / "train.yaml")

    with pytest.raises(ValueError, match="dataset_yaml is required"):
        TrainingConfig.load(config_path, run_name_override="valid-run")


def test_training_config_load_requires_dataset_file_to_exist(tmp_path: Path) -> None:
    config_path = write_config(tmp_path / "train.yaml")

    with pytest.raises(ValueError, match="dataset_yaml does not exist"):
        TrainingConfig.load(
            config_path,
            dataset_override=tmp_path / "missing-dataset.yaml",
            run_name_override="valid-run",
        )


def test_training_config_load_uses_yaml_relative_paths_when_present(tmp_path: Path) -> None:
    project_dir = tmp_path / "project"
    config_dir = project_dir / "configs" / "training"
    data_dir = project_dir / "data" / "prepared" / "example"
    config_dir.mkdir(parents=True)
    data_dir.mkdir(parents=True)
    dataset_yaml = data_dir / "dataset.yaml"
    dataset_yaml.write_text("path: /tmp/example\n", encoding="utf-8")
    config_path = write_config(
        config_dir / "train.yaml",
        dataset_yaml="data/prepared/example/dataset.yaml",
        run_name="baseline-3900",
        runs_dir="runs",
    )

    config = TrainingConfig.load(config_path)

    assert config.dataset_yaml == dataset_yaml.resolve()
    assert config.run_name == "baseline-3900"
    assert config.runs_dir == (project_dir / "runs").resolve()
    assert config.model == (project_dir / "models" / "yolo26n.pt").resolve()


@pytest.mark.parametrize(
    ("config_name", "epochs", "patience", "run_name"),
    [
        (
            "train-mixed-device-smoke-640.yaml",
            1,
            1,
            "mixed-device-6000-640-occlusion-safe-0907-smoke",
        ),
        (
            "train-mixed-device-6000-640.yaml",
            100,
            20,
            "mixed-device-6000-640-occlusion-safe-0907",
        ),
    ],
)
def test_mixed_device_training_configs_match_approved_mps_contract(
    config_name: str,
    epochs: int,
    patience: int,
    run_name: str,
) -> None:
    config = TrainingConfig.load(PROJECT_ROOT / "configs/training" / config_name)

    assert config.dataset_yaml == OCCLUSION_SAFE_DATASET
    assert config.run_name == run_name
    assert config.runs_dir == (PROJECT_ROOT / "runs").resolve()
    assert config.model == (PROJECT_ROOT / "models" / "yolo26n.pt").resolve()
    assert config.epochs == epochs
    assert config.patience == patience
    assert config.image_size == 640
    assert config.batch == 64
    assert config.nominal_batch_size == 64
    assert config.workers == 4
    assert config.device == "mps"
    assert config.seed == 42
    assert config.training_augmentation is True
