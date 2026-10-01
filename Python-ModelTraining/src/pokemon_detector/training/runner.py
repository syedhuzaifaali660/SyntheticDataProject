"""Validated Ultralytics training runner."""

from __future__ import annotations

import json
import platform
import shutil
from pathlib import Path
from typing import Any

import torch
import yaml

from pokemon_detector.training.config import TrainingConfig


class DatasetNotValidatedError(ValueError):
    """Raised when the dataset lacks fresh passing validation evidence."""


def _load_validation_report(dataset_yaml: Path) -> None:
    report_path = dataset_yaml.parent / "validation-report.json"
    try:
        payload = json.loads(report_path.read_text(encoding="utf-8"))
    except FileNotFoundError as error:
        raise DatasetNotValidatedError(f"validation-report.json is missing: {report_path}") from error
    except UnicodeDecodeError as error:
        raise DatasetNotValidatedError(f"validation-report.json is not valid UTF-8: {report_path}") from error
    except json.JSONDecodeError as error:
        raise DatasetNotValidatedError(f"validation-report.json is not valid JSON: {error}") from error

    if not isinstance(payload, dict):
        raise DatasetNotValidatedError("validation-report.json is not valid JSON object")

    if "passed" not in payload or "errors" not in payload:
        raise DatasetNotValidatedError("validation-report.json is missing required fields")

    passed = payload.get("passed")
    errors = payload.get("errors")
    if not isinstance(errors, list):
        raise DatasetNotValidatedError("validation-report.json errors must be a list")
    if passed is True and errors != []:
        raise DatasetNotValidatedError("validation report contradicts passed status with errors")
    if passed is False and errors == []:
        raise DatasetNotValidatedError("validation report contradicts failed status without errors")
    if passed is not True:
        raise DatasetNotValidatedError("dataset validation failed")


def _resolve_save_dir(model: Any) -> Path:
    trainer = getattr(model, "trainer", None)
    save_dir = getattr(trainer, "save_dir", None)
    if save_dir is None:
        raise ValueError("training backend did not expose trainer.save_dir")
    return Path(save_dir).resolve()


def _write_resolved_config(run_dir: Path, config: TrainingConfig) -> None:
    run_dir.mkdir(parents=True, exist_ok=True)
    (run_dir / "resolved-config.yaml").write_text(
        yaml.safe_dump(config.to_dict(), sort_keys=False),
        encoding="utf-8",
    )


def _write_environment(run_dir: Path) -> None:
    from ultralytics import __version__ as ultralytics_version

    payload = {
        "python_version": platform.python_version(),
        "platform": platform.platform(),
        "torch_version": torch.__version__,
        "ultralytics_version": ultralytics_version,
        "mps_available": bool(torch.backends.mps.is_available()),
    }
    (run_dir / "environment.json").write_text(
        json.dumps(payload, indent=2, sort_keys=False) + "\n",
        encoding="utf-8",
    )


def _archive_split_report(run_dir: Path, dataset_yaml: Path) -> None:
    split_report_path = dataset_yaml.parent / "split-report.json"
    if split_report_path.is_file():
        shutil.copy2(split_report_path, run_dir / "split-report.json")


def _augmentation_overrides(config: TrainingConfig) -> dict[str, float]:
    if config.training_augmentation:
        return {}
    return {
        "hsv_h": 0.0,
        "hsv_s": 0.0,
        "hsv_v": 0.0,
        "translate": 0.0,
        "scale": 0.0,
        "fliplr": 0.0,
        "mosaic": 0.0,
    }


def train(config: TrainingConfig, yolo_factory: Any = None) -> Path:
    if yolo_factory is None:
        from ultralytics import YOLO

        yolo_factory = YOLO
    _load_validation_report(config.dataset_yaml)
    model = yolo_factory(str(config.model))
    model.train(
        data=str(config.dataset_yaml),
        epochs=config.epochs,
        patience=config.patience,
        imgsz=config.image_size,
        batch=config.batch,
        nbs=config.nominal_batch_size,
        workers=config.workers,
        device=config.device,
        seed=config.seed,
        deterministic=True,
        project=str(config.runs_dir),
        name=config.run_name,
        **_augmentation_overrides(config),
    )
    run_dir = _resolve_save_dir(model)
    checkpoint_path = run_dir / "weights" / "best.pt"
    if not checkpoint_path.is_file():
        raise ValueError(f"training backend did not produce checkpoint: {checkpoint_path}")
    _write_resolved_config(run_dir, config)
    _write_environment(run_dir)
    _archive_split_report(run_dir, config.dataset_yaml)
    return checkpoint_path
