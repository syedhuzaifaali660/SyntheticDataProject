"""Training configuration loading and validation."""

from __future__ import annotations

import re
from dataclasses import asdict, dataclass
from pathlib import Path

import yaml

from pokemon_detector.project_paths import resolve_project_path

_SAFE_RUN_NAME = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
_SUPPORTED_DEVICES = ("cpu", "mps")
_DEFAULT_RUNS_DIR = Path("runs")


@dataclass(frozen=True, slots=True)
class TrainingConfig:
    config_path: Path
    dataset_yaml: Path
    run_name: str
    runs_dir: Path
    model: Path
    epochs: int
    patience: int
    image_size: int
    batch: int
    workers: int
    device: str
    seed: int
    nominal_batch_size: int
    training_augmentation: bool

    def __post_init__(self) -> None:
        if self.model.name != "yolo26n.pt":
            raise ValueError("model must be exactly yolo26n.pt")
        if self.epochs < 1:
            raise ValueError("epochs must be at least 1")
        if self.patience > self.epochs:
            raise ValueError("patience must be less than or equal to epochs")
        if self.image_size <= 0:
            raise ValueError("image_size must be positive")
        if self.batch <= 0:
            raise ValueError("batch must be positive")
        if self.nominal_batch_size <= 0:
            raise ValueError("nominal_batch_size must be positive")
        if self.workers <= 0:
            raise ValueError("workers must be positive")
        if self.device not in _SUPPORTED_DEVICES:
            raise ValueError("device must be one of: cpu, mps")
        if not isinstance(self.training_augmentation, bool):
            raise TypeError("training_augmentation must be a boolean")
        if not self.run_name:
            raise ValueError("run_name must not be empty")
        if not _SAFE_RUN_NAME.fullmatch(self.run_name):
            raise ValueError("run_name contains unsafe characters")
        if not self.dataset_yaml.is_file():
            raise ValueError(f"dataset_yaml does not exist: {self.dataset_yaml}")

    @classmethod
    def load(
        cls,
        path: Path,
        *,
        dataset_override: Path | None = None,
        run_name_override: str | None = None,
        runs_dir_override: Path | None = None,
    ) -> TrainingConfig:
        config_path = path.resolve()
        try:
            payload = yaml.safe_load(config_path.read_text(encoding="utf-8"))
        except FileNotFoundError as error:
            raise ValueError(f"config file does not exist: {path}") from error
        except UnicodeDecodeError as error:
            raise ValueError(f"config file is not valid UTF-8: {path}") from error
        except yaml.YAMLError as error:
            raise ValueError(f"config file is not valid YAML: {error}") from error

        if not isinstance(payload, dict):
            raise TypeError("config file must contain a YAML mapping")

        dataset_value = dataset_override if dataset_override is not None else payload.get("dataset_yaml")
        if dataset_value is None:
            raise ValueError("dataset_yaml is required")
        dataset_yaml = cls._resolve_input_path(
            dataset_value,
            config_path=config_path,
            use_cwd=dataset_override is not None,
        )

        run_name = run_name_override if run_name_override is not None else payload.get("run_name")
        if run_name is None:
            raise ValueError("run_name is required")
        if not isinstance(run_name, str):
            raise TypeError("run_name must be a string")

        runs_value = runs_dir_override if runs_dir_override is not None else payload.get("runs_dir", _DEFAULT_RUNS_DIR)
        runs_dir = cls._resolve_input_path(
            runs_value,
            config_path=config_path,
            use_cwd=runs_dir_override is not None,
        )

        try:
            return cls(
                config_path=config_path,
                dataset_yaml=dataset_yaml,
                run_name=run_name,
                runs_dir=runs_dir,
                model=cls._resolve_input_path(
                    payload["model"],
                    config_path=config_path,
                    use_cwd=False,
                ),
                epochs=cls._coerce_int(payload["epochs"], field_name="epochs"),
                patience=cls._coerce_int(payload["patience"], field_name="patience"),
                image_size=cls._coerce_int(payload["image_size"], field_name="image_size"),
                batch=cls._coerce_int(payload["batch"], field_name="batch"),
                workers=cls._coerce_int(payload["workers"], field_name="workers"),
                device=str(payload["device"]),
                seed=cls._coerce_int(payload["seed"], field_name="seed"),
                nominal_batch_size=cls._coerce_int(
                    payload.get("nominal_batch_size", 64),
                    field_name="nominal_batch_size",
                ),
                training_augmentation=cls._coerce_bool(
                    payload.get("training_augmentation", True),
                    field_name="training_augmentation",
                ),
            )
        except KeyError as error:
            raise ValueError(f"config file is missing required field: {error.args[0]}") from error

    @staticmethod
    def _coerce_int(value: object, *, field_name: str) -> int:
        if isinstance(value, bool) or not isinstance(value, int):
            raise TypeError(f"{field_name} must be an integer")
        return value

    @staticmethod
    def _coerce_bool(value: object, *, field_name: str) -> bool:
        if not isinstance(value, bool):
            raise TypeError(f"{field_name} must be a boolean")
        return value

    @staticmethod
    def _resolve_input_path(value: object, *, config_path: Path, use_cwd: bool) -> Path:
        if not isinstance(value, (str, Path)):
            raise TypeError("path values must be strings")
        path = Path(value)
        if use_cwd:
            return path.resolve()
        return resolve_project_path(path, start=config_path)

    def to_dict(self) -> dict[str, object]:
        payload = asdict(self)
        return {
            key: str(value) if isinstance(value, Path) else value
            for key, value in payload.items()
        }
