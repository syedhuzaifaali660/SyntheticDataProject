"""Deterministic background-grouped dataset splitting."""

from __future__ import annotations

import json
import math
import random
import shutil
import tempfile
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path

import yaml

from pokemon_detector.dataset.validator import ValidationReport, validate_run
from pokemon_detector.domain import ManifestObject, ManifestRecord, PokemonClass, YoloBox

_CLASS_NAMES = {
    PokemonClass.PIKACHU.value: "pikachu",
    PokemonClass.CHARMANDER.value: "charmander",
    PokemonClass.SQUIRTLE.value: "squirtle",
}
_RATIO_TOLERANCE = 1e-9
_MIN_BACKGROUND_COUNT = 10


@dataclass(frozen=True, slots=True)
class SplitConfig:
    seed: int
    train_ratio: float
    val_ratio: float
    test_ratio: float
    target_train_images: int | None = None
    target_val_images: int | None = None
    target_test_images: int | None = None

    def __post_init__(self) -> None:
        if isinstance(self.seed, bool) or not isinstance(self.seed, int):
            raise TypeError("seed must be an integer")

        for field_name in ("train_ratio", "val_ratio", "test_ratio"):
            value = getattr(self, field_name)
            if not math.isfinite(value):
                raise ValueError(f"{field_name} must be finite")
            if value <= 0:
                raise ValueError(f"{field_name} must be positive")

        ratio_sum = self.train_ratio + self.val_ratio + self.test_ratio
        if not math.isclose(ratio_sum, 1.0, rel_tol=0.0, abs_tol=_RATIO_TOLERANCE):
            raise ValueError("ratios must sum to 1")

        target_counts = self.target_image_counts
        if any(count is None for count in target_counts) and any(
            count is not None for count in target_counts
        ):
            raise ValueError(
                "target image counts must be provided for train, val, and test together"
            )
        for field_name, count in zip(
            ("target_train_images", "target_val_images", "target_test_images"), target_counts
        ):
            if count is not None and (
                isinstance(count, bool) or not isinstance(count, int) or count <= 0
            ):
                raise ValueError(f"{field_name} must be a positive integer")

    @property
    def target_image_counts(self) -> tuple[int | None, int | None, int | None]:
        return self.target_train_images, self.target_val_images, self.target_test_images

    @property
    def expected_frame_count(self) -> int | None:
        target_counts = self.target_image_counts
        if any(count is None for count in target_counts):
            return None
        return sum(count for count in target_counts if count is not None)

    @classmethod
    def from_yaml(cls, path: Path) -> SplitConfig:
        try:
            payload = yaml.safe_load(path.read_text(encoding="utf-8"))
        except FileNotFoundError as error:
            raise ValueError(f"config file does not exist: {path}") from error
        except UnicodeDecodeError as error:
            raise ValueError(f"config file is not valid UTF-8: {path}") from error
        except yaml.YAMLError as error:
            raise ValueError(f"config file is not valid YAML: {error}") from error

        if not isinstance(payload, dict):
            raise TypeError("config file must contain a YAML mapping")

        try:
            seed = payload["seed"]
            train_ratio = payload["train_ratio"]
            val_ratio = payload["val_ratio"]
            test_ratio = payload["test_ratio"]
        except KeyError as error:
            raise ValueError(f"config file is missing required field: {error.args[0]}") from error

        return cls(
            seed=seed,
            train_ratio=float(train_ratio),
            val_ratio=float(val_ratio),
            test_ratio=float(test_ratio),
            target_train_images=payload.get("target_train_images"),
            target_val_images=payload.get("target_val_images"),
            target_test_images=payload.get("target_test_images"),
        )


@dataclass(frozen=True, slots=True)
class SplitReport:
    run_id: str
    seed: int
    train_ratio: float
    val_ratio: float
    test_ratio: float
    image_counts: dict[str, int]
    train_background_ids: tuple[str, ...]
    val_background_ids: tuple[str, ...]
    test_background_ids: tuple[str, ...]

    def to_dict(self) -> dict[str, object]:
        return {
            "run_id": self.run_id,
            "seed": self.seed,
            "ratios": {
                "train": self.train_ratio,
                "val": self.val_ratio,
                "test": self.test_ratio,
            },
            "image_counts": self.image_counts,
            "train_background_ids": list(self.train_background_ids),
            "val_background_ids": list(self.val_background_ids),
            "test_background_ids": list(self.test_background_ids),
        }


def _load_manifest_record(raw_line: str) -> ManifestRecord:
    payload = json.loads(raw_line)
    objects = tuple(
        ManifestObject(
            class_id=int(item["class_id"]),
            class_name=str(item["class_name"]),
            box=YoloBox(
                class_id=int(item["class_id"]),
                center_x=float(item["bbox"][0]),
                center_y=float(item["bbox"][1]),
                width=float(item["bbox"][2]),
                height=float(item["bbox"][3]),
            ),
            size_band=item.get("size_band"),
            model_input_width=(float(item["model_input_width"]) if item.get("model_input_width") is not None else None),
            model_input_height=(float(item["model_input_height"]) if item.get("model_input_height") is not None else None),
        )
        for item in payload["objects"]
    )
    return ManifestRecord(
        run_id=str(payload["run_id"]),
        frame_id=int(payload["frame_id"]),
        seed=int(payload["seed"]),
        split_hint=str(payload["split_hint"]),
        background_id=str(payload["background_id"]),
        image_width=int(payload["width"]),
        image_height=int(payload["height"]),
        objects=objects,
        capture_profile=payload.get("capture_profile"),
        source_width=(int(payload["source_width"]) if payload.get("source_width") is not None else None),
        source_height=(int(payload["source_height"]) if payload.get("source_height") is not None else None),
        source_aspect_ratio=(float(payload["source_aspect_ratio"]) if payload.get("source_aspect_ratio") is not None else None),
        letterbox_scale=(float(payload["letterbox_scale"]) if payload.get("letterbox_scale") is not None else None),
        letterbox_pad_x=(float(payload["letterbox_pad_x"]) if payload.get("letterbox_pad_x") is not None else None),
        letterbox_pad_y=(float(payload["letterbox_pad_y"]) if payload.get("letterbox_pad_y") is not None else None),
        lighting_band=payload.get("lighting_band"),
    )


def _load_manifest_records(run_dir: Path) -> list[ManifestRecord]:
    manifest_path = run_dir / "manifest.jsonl"
    try:
        lines = manifest_path.read_text(encoding="utf-8").splitlines()
    except FileNotFoundError as error:
        raise ValueError(f"manifest.jsonl is missing: {manifest_path}") from error
    except UnicodeDecodeError as error:
        raise ValueError(f"manifest.jsonl is not valid UTF-8: {manifest_path}") from error

    records: list[ManifestRecord] = []
    seen_frame_ids: set[int] = set()
    for line_number, line in enumerate(lines, start=1):
        if not line.strip():
            continue
        try:
            record = _load_manifest_record(line)
        except (json.JSONDecodeError, KeyError, TypeError, ValueError) as error:
            raise ValueError(f"manifest.jsonl line {line_number} is invalid: {error}") from error
        if record.frame_id in seen_frame_ids:
            raise ValueError(
                f"manifest.jsonl line {line_number} repeats frame {record.frame_id:06d}; "
                "frame IDs must be unique"
            )
        seen_frame_ids.add(record.frame_id)
        records.append(record)
    return records


def _raise_for_validation_errors(report: ValidationReport) -> None:
    if report.passed:
        return
    issue = report.errors[0]
    location = f" ({issue.path})" if issue.path else ""
    raise ValueError(f"validation failed: {issue.code}: {issue.message}{location}")


def _assigned_backgrounds(
    unique_background_ids: list[str],
    config: SplitConfig,
    background_frame_counts: dict[str, int] | None = None,
) -> dict[str, str]:
    if len(unique_background_ids) < _MIN_BACKGROUND_COUNT:
        raise ValueError("at least 10 unique backgrounds are required")

    shuffled_background_ids = list(unique_background_ids)
    random.Random(config.seed).shuffle(shuffled_background_ids)

    if config.expected_frame_count is not None:
        if background_frame_counts is None:
            raise ValueError("background frame counts are required for exact image targets")
        return _assigned_backgrounds_to_exact_targets(
            shuffled_background_ids,
            background_frame_counts,
            config,
        )

    train_count = math.floor(len(shuffled_background_ids) * config.train_ratio)
    val_count = math.floor(len(shuffled_background_ids) * config.val_ratio)
    train_ids = shuffled_background_ids[:train_count]
    val_ids = shuffled_background_ids[train_count : train_count + val_count]
    test_ids = shuffled_background_ids[train_count + val_count :]

    assignments: dict[str, str] = {}
    for background_id in train_ids:
        assignments[background_id] = "train"
    for background_id in val_ids:
        if background_id in assignments:
            raise ValueError(f"invariant violation: background {background_id} crosses splits")
        assignments[background_id] = "val"
    for background_id in test_ids:
        if background_id in assignments:
            raise ValueError(f"invariant violation: background {background_id} crosses splits")
        assignments[background_id] = "test"
    return assignments


def _assigned_backgrounds_to_exact_targets(
    shuffled_background_ids: list[str],
    background_frame_counts: dict[str, int],
    config: SplitConfig,
) -> dict[str, str]:
    target_counts = {
        "train": config.target_train_images,
        "val": config.target_val_images,
        "test": config.target_test_images,
    }
    if any(count is None for count in target_counts.values()):
        raise ValueError("target image counts must be provided for train, val, and test together")
    if set(shuffled_background_ids) != set(background_frame_counts):
        raise ValueError("background frame counts must match manifest backgrounds")

    total_images = sum(background_frame_counts.values())
    if total_images != config.expected_frame_count:
        raise ValueError(
            f"exact image targets total {config.expected_frame_count}, but manifest contains {total_images} frames"
        )

    split_names = sorted(
        target_counts, key=lambda split_name: (target_counts[split_name], split_name)
    )
    first_split, second_split, remaining_split = split_names
    first_target = target_counts[first_split]
    second_target = target_counts[second_split]
    assert first_target is not None and second_target is not None

    # Each state stores the selections that reached its two exact partial sums.
    states: dict[tuple[int, int], tuple[tuple[int, int] | None, str | None, str | None]] = {
        (0, 0): (None, None, None)
    }
    for background_id in shuffled_background_ids:
        frame_count = background_frame_counts[background_id]
        prior_states = list(states.items())
        for (first_sum, second_sum), _ in prior_states:
            if first_sum + frame_count <= first_target:
                next_state = (first_sum + frame_count, second_sum)
                states.setdefault(next_state, ((first_sum, second_sum), background_id, first_split))
            if second_sum + frame_count <= second_target:
                next_state = (first_sum, second_sum + frame_count)
                states.setdefault(
                    next_state, ((first_sum, second_sum), background_id, second_split)
                )

    target_state = (first_target, second_target)
    if target_state not in states:
        raise ValueError(
            "background groups cannot satisfy exact image targets without crossing partitions"
        )

    assignments: dict[str, str] = {}
    state = target_state
    while state != (0, 0):
        previous_state, background_id, split_name = states[state]
        assert previous_state is not None and background_id is not None and split_name is not None
        assignments[background_id] = split_name
        state = previous_state
    for background_id in shuffled_background_ids:
        assignments.setdefault(background_id, remaining_split)

    image_counts = {
        split_name: sum(
            background_frame_counts[background_id]
            for background_id, assigned_split in assignments.items()
            if assigned_split == split_name
        )
        for split_name in target_counts
    }
    if image_counts != target_counts:
        raise ValueError(
            "background groups cannot satisfy exact image targets without crossing partitions"
        )
    return assignments


def _ensure_output_dir_is_usable(output_dir: Path) -> None:
    if output_dir.exists():
        if not output_dir.is_dir():
            raise ValueError(f"output path exists and is not a directory: {output_dir}")
        if any(output_dir.iterdir()):
            raise ValueError(f"output directory must be empty: {output_dir}")


def _source_paths(run_dir: Path, frame_id: int) -> tuple[Path, Path]:
    image_path = run_dir / "images" / f"frame_{frame_id:06d}.png"
    label_path = run_dir / "labels" / f"frame_{frame_id:06d}.txt"
    return image_path, label_path


def _validate_sources_exist(run_dir: Path, records: list[ManifestRecord]) -> None:
    for record in records:
        image_path, label_path = _source_paths(run_dir, record.frame_id)
        if not image_path.is_file():
            raise ValueError(f"missing source image for frame {record.frame_id:06d}: {image_path}")
        if not label_path.is_file():
            raise ValueError(f"missing source label for frame {record.frame_id:06d}: {label_path}")


def _write_dataset_yaml(output_dir: Path, dataset_root: Path) -> None:
    payload = {
        "path": str(dataset_root.resolve()),
        "train": "images/train",
        "val": "images/val",
        "test": "images/test",
        "names": _CLASS_NAMES,
    }
    (output_dir / "dataset.yaml").write_text(
        yaml.safe_dump(payload, sort_keys=False),
        encoding="utf-8",
    )


def _write_validation_report(report: ValidationReport, output_dir: Path) -> None:
    (output_dir / "validation-report.json").write_text(
        json.dumps(report.to_dict(), indent=2) + "\n",
        encoding="utf-8",
    )


def _build_report(run_id: str, config: SplitConfig, assignments: dict[str, str], records: list[ManifestRecord]) -> SplitReport:
    grouped_records: dict[str, list[ManifestRecord]] = defaultdict(list)
    for record in records:
        grouped_records[record.background_id].append(record)

    split_to_backgrounds = {
        "train": sorted(background_id for background_id, split_name in assignments.items() if split_name == "train"),
        "val": sorted(background_id for background_id, split_name in assignments.items() if split_name == "val"),
        "test": sorted(background_id for background_id, split_name in assignments.items() if split_name == "test"),
    }
    image_counts = {
        split_name: sum(len(grouped_records[background_id]) for background_id in background_ids)
        for split_name, background_ids in split_to_backgrounds.items()
    }
    return SplitReport(
        run_id=run_id,
        seed=config.seed,
        train_ratio=config.train_ratio,
        val_ratio=config.val_ratio,
        test_ratio=config.test_ratio,
        image_counts=image_counts,
        train_background_ids=tuple(split_to_backgrounds["train"]),
        val_background_ids=tuple(split_to_backgrounds["val"]),
        test_background_ids=tuple(split_to_backgrounds["test"]),
    )


def split_run(run_dir: Path, output_dir: Path, config: SplitConfig) -> SplitReport:
    report = validate_run(run_dir, expected_frame_count=config.expected_frame_count)
    _raise_for_validation_errors(report)

    records = _load_manifest_records(run_dir)
    _validate_sources_exist(run_dir, records)
    _ensure_output_dir_is_usable(output_dir)

    unique_background_ids = sorted({record.background_id for record in records})
    background_frame_counts = Counter(record.background_id for record in records)
    assignments = _assigned_backgrounds(
        unique_background_ids, config, dict(background_frame_counts)
    )
    for record in records:
        split_name = assignments[record.background_id]
        if assignments[record.background_id] != split_name:
            raise ValueError(f"invariant violation: background {record.background_id} crosses splits")

    run_id = records[0].run_id if records else run_dir.name
    staging_dir = Path(
        tempfile.mkdtemp(prefix=f".{output_dir.name}.tmp-", dir=output_dir.parent)
    )
    try:
        for split_name in ("train", "val", "test"):
            (staging_dir / "images" / split_name).mkdir(parents=True, exist_ok=False)
            (staging_dir / "labels" / split_name).mkdir(parents=True, exist_ok=False)

        for record in records:
            split_name = assignments[record.background_id]
            image_path, label_path = _source_paths(run_dir, record.frame_id)
            shutil.copy2(image_path, staging_dir / "images" / split_name / image_path.name)
            shutil.copy2(label_path, staging_dir / "labels" / split_name / label_path.name)

        split_report = _build_report(run_id, config, assignments, records)
        _write_dataset_yaml(staging_dir, output_dir)
        _write_validation_report(report, staging_dir)
        (staging_dir / "split-report.json").write_text(
            json.dumps(split_report.to_dict(), indent=2) + "\n",
            encoding="utf-8",
        )

        if output_dir.exists():
            output_dir.rmdir()
        staging_dir.rename(output_dir)
        return split_report
    except Exception:
        if staging_dir.exists():
            shutil.rmtree(staging_dir)
        raise
