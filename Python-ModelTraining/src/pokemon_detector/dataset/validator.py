"""Dataset-wide validation for generated YOLO datasets."""

from __future__ import annotations

import json
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path

from PIL import Image, UnidentifiedImageError

from pokemon_detector.dataset.parser import LabelParseError, parse_yolo_label
from pokemon_detector.domain import ManifestObject, ManifestRecord, PokemonClass, YoloBox

FRAME_PREFIX = "frame_"
FRAME_DIGITS = 6
BOX_TOLERANCE = 1e-6
_MIXED_PROFILE_TARGETS = {"Square": 0.15, "IPhonePortrait": 0.25, "IPhoneLandscape": 0.20, "Webcam": 0.20, "LaptopWindow": 0.20}


@dataclass(frozen=True, slots=True)
class ValidationIssue:
    code: str
    message: str
    path: str | None = None
    frame_id: int | None = None

    def to_dict(self) -> dict[str, object]:
        payload: dict[str, object] = {"code": self.code, "message": self.message}
        if self.path is not None:
            payload["path"] = self.path
        if self.frame_id is not None:
            payload["frame_id"] = self.frame_id
        return payload


@dataclass(frozen=True, slots=True)
class ValidationReport:
    run_id: str
    image_count: int
    label_count: int
    object_count_by_class: dict[int, int]
    negative_image_count: int
    errors: tuple[ValidationIssue, ...]
    warnings: tuple[ValidationIssue, ...]
    capture_profile_counts: dict[str, int] = field(default_factory=dict)
    lighting_band_counts: dict[str, int] = field(default_factory=dict)
    object_size_band_counts: dict[str, int] = field(default_factory=dict)

    @property
    def passed(self) -> bool:
        return not self.errors

    def to_dict(self) -> dict[str, object]:
        return {
            "run_id": self.run_id,
            "image_count": self.image_count,
            "label_count": self.label_count,
            "object_count_by_class": self.object_count_by_class,
            "negative_image_count": self.negative_image_count,
            "errors": [issue.to_dict() for issue in self.errors],
            "warnings": [issue.to_dict() for issue in self.warnings],
            "capture_profile_counts": self.capture_profile_counts,
            "lighting_band_counts": self.lighting_band_counts,
            "object_size_band_counts": self.object_size_band_counts,
            "passed": self.passed,
        }


def _report_with_error(run_id: str, code: str, message: str, path: str | None = None) -> ValidationReport:
    return ValidationReport(
        run_id=run_id,
        image_count=0,
        label_count=0,
        object_count_by_class={member.value: 0 for member in PokemonClass},
        negative_image_count=0,
        errors=(ValidationIssue(code=code, message=message, path=path),),
        warnings=(),
    )


def _frame_id_from_path(path: Path) -> int | None:
    if path.stem.startswith(FRAME_PREFIX):
        suffix = path.stem[len(FRAME_PREFIX) :]
        if len(suffix) == FRAME_DIGITS and suffix.isdigit():
            return int(suffix)
    return None


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


def _missing_class_warning(object_counts: Counter[int]) -> tuple[ValidationIssue, ...]:
    missing_names = [
        member.name.lower()
        for member in PokemonClass
        if object_counts.get(member.value, 0) == 0
    ]
    if not missing_names:
        return ()
    return (
        ValidationIssue(
            code="MISSING_CLASS_BALANCE",
            message=f"dataset is missing objects for classes: {', '.join(missing_names)}",
        ),
    )


def _append_issue(issues: list[ValidationIssue], code: str, message: str, path: Path | None = None, frame_id: int | None = None) -> None:
    issues.append(
        ValidationIssue(
            code=code,
            message=message,
            path=str(path) if path is not None else None,
            frame_id=frame_id,
        )
    )


def _collect_paths(directory: Path, suffix: str) -> dict[int, Path]:
    collected: dict[int, Path] = {}
    for path in sorted(directory.glob(f"*{suffix}")):
        frame_id = _frame_id_from_path(path)
        if frame_id is not None:
            collected[frame_id] = path
    return collected


def _is_uniform_image(image: Image.Image) -> bool:
    return all(minimum == maximum for minimum, maximum in image.convert("RGB").getextrema())


def _compare_manifest_boxes(
    frame_id: int, image_path: Path, label_boxes: list[YoloBox], record: ManifestRecord
) -> list[ValidationIssue]:
    issues: list[ValidationIssue] = []
    if len(label_boxes) != len(record.objects):
        issues.append(
            ValidationIssue(
                code="OBJECT_COUNT_MISMATCH",
                message=(
                    f"frame {frame_id:06d} has {len(label_boxes)} labels but "
                    f"{len(record.objects)} manifest objects"
                ),
                path=str(image_path),
                frame_id=frame_id,
            )
        )
        return issues
    for index, (label_box, manifest_object) in enumerate(zip(label_boxes, record.objects), start=1):
        if label_box.class_id != manifest_object.class_id:
            issues.append(
                ValidationIssue(
                    code="INVALID_CLASS_ID",
                    message=(
                        f"frame {frame_id:06d} object {index} has class_id {label_box.class_id} "
                        f"but manifest expected {manifest_object.class_id}"
                    ),
                    path=str(image_path),
                    frame_id=frame_id,
                )
            )
            continue
        manifest_box = manifest_object.box
        for coordinate in ("center_x", "center_y", "width", "height"):
            if abs(getattr(label_box, coordinate) - getattr(manifest_box, coordinate)) > BOX_TOLERANCE:
                issues.append(
                    ValidationIssue(
                        code="BOX_MISMATCH",
                        message=(
                            f"frame {frame_id:06d} object {index} {coordinate} does not match manifest"
                        ),
                        path=str(image_path),
                        frame_id=frame_id,
                    )
                )
                break
    return issues


def validate_run(run_dir: Path, *, expected_frame_count: int | None = None) -> ValidationReport:
    if expected_frame_count is not None and expected_frame_count <= 0:
        raise ValueError("expected_frame_count must be positive")
    images_dir = run_dir / "images"
    labels_dir = run_dir / "labels"
    manifest_path = run_dir / "manifest.jsonl"

    image_paths = _collect_paths(images_dir, ".png")
    label_paths = _collect_paths(labels_dir, ".txt")
    try:
        manifest_lines = manifest_path.read_text(encoding="utf-8").splitlines()
    except FileNotFoundError:
        return _report_with_error(run_dir.name, "INVALID_MANIFEST", "manifest.jsonl is missing", str(manifest_path))
    except UnicodeDecodeError:
        return _report_with_error(run_dir.name, "INVALID_MANIFEST", "manifest.jsonl is not valid UTF-8", str(manifest_path))

    manifest_records: dict[int, ManifestRecord] = {}
    for line_number, line in enumerate(manifest_lines, start=1):
        if not line.strip():
            continue
        try:
            record = _load_manifest_record(line)
        except (json.JSONDecodeError, KeyError, TypeError, ValueError) as error:
            return _report_with_error(
                run_dir.name,
                "INVALID_MANIFEST",
                f"manifest.jsonl line {line_number} is invalid: {error}",
                str(manifest_path),
            )
        if record.frame_id in manifest_records:
            return _report_with_error(
                run_dir.name,
                "INVALID_MANIFEST",
                (
                    f"manifest.jsonl line {line_number} repeats frame "
                    f"{record.frame_id:06d}; frame IDs must be unique"
                ),
                str(manifest_path),
            )
        manifest_records[record.frame_id] = record

    run_id = next(iter(manifest_records.values())).run_id if manifest_records else run_dir.name
    object_counts = Counter({member.value: 0 for member in PokemonClass})
    negative_image_count = 0
    errors: list[ValidationIssue] = []
    profile_counts: Counter[str] = Counter()
    lighting_counts: Counter[str] = Counter()
    size_counts: Counter[str] = Counter()

    image_frame_ids = set(image_paths)
    label_frame_ids = set(label_paths)
    manifest_frame_ids = set(manifest_records)

    for frame_id in sorted(image_frame_ids - label_frame_ids):
        errors.append(
            ValidationIssue(
                code="MISSING_LABEL",
                message=f"missing label for frame {frame_id:06d}",
                path=str(image_paths[frame_id]),
                frame_id=frame_id,
            )
        )
    for frame_id in sorted(label_frame_ids - image_frame_ids):
        errors.append(
            ValidationIssue(
                code="ORPHAN_LABEL",
                message=f"label has no matching image for frame {frame_id:06d}",
                path=str(label_paths[frame_id]),
                frame_id=frame_id,
            )
        )
    for frame_id in sorted((image_frame_ids & label_frame_ids) - manifest_frame_ids):
        _append_issue(
            errors,
            "MANIFEST_FRAME_MISSING",
            f"manifest is missing frame {frame_id:06d}",
            image_paths[frame_id],
            frame_id,
        )
    for frame_id in sorted(manifest_frame_ids - (image_frame_ids | label_frame_ids)):
        _append_issue(
            errors,
            "MANIFEST_FILE_MISSING",
            f"manifest frame {frame_id:06d} has no matching image or label file",
            manifest_path,
            frame_id,
        )

    for frame_id in sorted(image_frame_ids & label_frame_ids & manifest_frame_ids):
        image_path = image_paths[frame_id]
        label_path = label_paths[frame_id]
        record = manifest_records[frame_id]
        if record.capture_profile:
            profile_counts[record.capture_profile] += 1
        if record.lighting_band:
            lighting_counts[record.lighting_band] += 1
        for obj in record.objects:
            if obj.size_band:
                size_counts[obj.size_band] += 1

        try:
            with Image.open(image_path) as image:
                image.load()
                width, height = image.size
                is_uniform = _is_uniform_image(image)
        except (OSError, UnidentifiedImageError, ModuleNotFoundError):
            errors.append(
                ValidationIssue(
                    code="UNREADABLE_IMAGE",
                    message=f"image cannot be opened for frame {frame_id:06d}",
                    path=str(image_path),
                    frame_id=frame_id,
                )
            )
            continue

        if is_uniform:
            errors.append(
                ValidationIssue(
                    code="UNIFORM_IMAGE",
                    message=f"image has no pixel variation for frame {frame_id:06d}",
                    path=str(image_path),
                    frame_id=frame_id,
                )
            )
            continue

        if width != record.image_width or height != record.image_height:
            errors.append(
                ValidationIssue(
                    code="DIMENSION_MISMATCH",
                    message=(
                        f"frame {frame_id:06d} image is {width}x{height} but manifest "
                        f"declares {record.image_width}x{record.image_height}"
                    ),
                    path=str(image_path),
                    frame_id=frame_id,
                )
            )
            continue

        try:
            label_boxes = parse_yolo_label(label_path)
        except LabelParseError as error:
            _append_issue(errors, error.code, str(error), label_path, frame_id)
            continue

        frame_issues = _compare_manifest_boxes(frame_id, image_path, label_boxes, record)
        errors.extend(frame_issues)
        if frame_issues:
            continue

        for box in label_boxes:
            object_counts[box.class_id] += 1
        if not label_boxes and not record.objects:
            negative_image_count += 1

    # An incomplete production capture must fail, rather than bypassing its gates.
    if expected_frame_count is None and run_id == "mixed-device-6000-640":
        expected_frame_count = 6000
    if expected_frame_count is not None:
        if any(count != expected_frame_count for count in (len(image_paths), len(label_paths), len(manifest_records))):
            errors.append(ValidationIssue("FRAME_COUNT", f"expected {expected_frame_count} image/label/manifest triples"))
        for name, target in _MIXED_PROFILE_TARGETS.items():
            actual = profile_counts.get(name, 0) / expected_frame_count
            if abs(actual - target) > 0.03:
                errors.append(ValidationIssue("PROFILE_DISTRIBUTION", f"profile {name} is {actual:.1%}; expected {target:.1%} ±3pp"))
        for name, target in (("Low", .20), ("Normal", .60), ("Bright", .20)):
            actual = lighting_counts.get(name, 0) / expected_frame_count
            if abs(actual - target) > 0.03:
                errors.append(ValidationIssue("LIGHTING_DISTRIBUTION", f"lighting band {name} is {actual:.1%}; expected {target:.1%} ±3pp"))
        total_objects = sum(object_counts.values())
        for name, target in (("Small", .30), ("Medium", .50), ("Large", .20)):
            actual = size_counts.get(name, 0) / max(total_objects, 1)
            if abs(actual - target) > .05:
                errors.append(ValidationIssue("SIZE_DISTRIBUTION", f"size band {name} is {actual:.1%}; expected {target:.1%} ±5pp"))
        average = total_objects / 3
        if not total_objects or any(abs(count - average) > average * .05 for count in object_counts.values()):
            errors.append(ValidationIssue("CLASS_DISTRIBUTION", "class object counts must be within 5% of their mean"))

    return ValidationReport(
        run_id=run_id,
        image_count=len(image_paths),
        label_count=len(label_paths),
        object_count_by_class=dict(sorted(object_counts.items())),
        negative_image_count=negative_image_count,
        errors=tuple(errors),
        warnings=_missing_class_warning(object_counts),
        capture_profile_counts=dict(sorted(profile_counts.items())),
        lighting_band_counts=dict(sorted(lighting_counts.items())),
        object_size_band_counts=dict(sorted(size_counts.items())),
    )
