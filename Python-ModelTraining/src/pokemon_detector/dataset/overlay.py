"""Overlay rendering for validated YOLO datasets."""

from __future__ import annotations

import json
import random
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw

from pokemon_detector.dataset.parser import parse_yolo_label
from pokemon_detector.domain import PokemonClass, YoloBox

COLORS = {
    PokemonClass.PIKACHU.value: (255, 215, 0),
    PokemonClass.CHARMANDER.value: (255, 99, 71),
    PokemonClass.SQUIRTLE.value: (65, 105, 225),
}
IMAGE_SUFFIXES = {".bmp", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"}
PROFILE_ORDER = ("Square", "IPhonePortrait", "IPhoneLandscape", "Webcam", "LaptopWindow")
PROFILE_QUOTA = 20
NEAR_EDGE_THRESHOLD = 0.05
NEAR_EDGE_DEFINITION = "a normalized box edge is within 0.05 of an image boundary"
REQUIRED_COVERAGE_CATEGORIES = ("low_light", "small", "large", "near_edge", "negative")


class ProfileBalancedOverlaySelectionError(ValueError):
    """Raised when a requested balanced review sample cannot meet its contract."""


@dataclass(frozen=True, slots=True)
class ProfileBalancedOverlaySelection:
    """Rendered overlay paths and the manifest-derived review coverage they prove."""

    paths: list[Path]
    coverage: dict[str, object]


@dataclass(frozen=True, slots=True)
class _ProfileBalancedCandidate:
    frame_id: int
    profile: str
    lighting_band: str | None
    objects: tuple[tuple[str | None, tuple[float, float, float, float]], ...]
    label_path: Path
    image_path: Path


def _is_near_edge(box: tuple[float, float, float, float]) -> bool:
    center_x, center_y, width, height = box
    left = center_x - width / 2.0
    right = center_x + width / 2.0
    top = center_y - height / 2.0
    bottom = center_y + height / 2.0
    return min(left, top, 1.0 - right, 1.0 - bottom) <= NEAR_EDGE_THRESHOLD


def _candidate_has_category(candidate: _ProfileBalancedCandidate, category: str) -> bool:
    if category == "low_light":
        return candidate.lighting_band == "Low"
    if category == "small":
        return any(size_band == "Small" for size_band, _ in candidate.objects)
    if category == "large":
        return any(size_band == "Large" for size_band, _ in candidate.objects)
    if category == "near_edge":
        return any(_is_near_edge(box) for _, box in candidate.objects)
    if category == "negative":
        return not candidate.objects
    raise ValueError(f"unknown coverage category: {category}")


def _load_profile_balanced_candidates(run_dir: Path) -> list[_ProfileBalancedCandidate]:
    manifest_path = run_dir / "manifest.jsonl"
    try:
        lines = manifest_path.read_text(encoding="utf-8").splitlines()
    except FileNotFoundError as error:
        raise ProfileBalancedOverlaySelectionError("profile-balanced overlays require manifest.jsonl") from error
    except UnicodeDecodeError as error:
        raise ProfileBalancedOverlaySelectionError("manifest.jsonl is not valid UTF-8") from error

    candidates: list[_ProfileBalancedCandidate] = []
    seen_frame_ids: set[int] = set()
    for line_number, line in enumerate(lines, start=1):
        if not line.strip():
            continue
        try:
            payload: dict[str, Any] = json.loads(line)
            frame_id = int(payload["frame_id"])
            profile = payload["capture_profile"]
            objects_payload = payload["objects"]
            if not isinstance(profile, str) or profile not in PROFILE_ORDER:
                raise ValueError("capture_profile must be one of the documented mixed-device profiles")
            if not isinstance(objects_payload, list):
                raise TypeError("objects must be a list")
            objects = tuple(
                (
                    item.get("size_band"),
                    tuple(float(value) for value in item["bbox"]),
                )
                for item in objects_payload
            )
            if any(len(box) != 4 for _, box in objects):
                raise ValueError("object bbox must contain four normalized values")
        except (json.JSONDecodeError, KeyError, TypeError, ValueError) as error:
            raise ProfileBalancedOverlaySelectionError(
                f"manifest.jsonl line {line_number} cannot supply profile-balanced coverage: {error}"
            ) from error
        if frame_id in seen_frame_ids:
            raise ProfileBalancedOverlaySelectionError(
                f"manifest.jsonl repeats frame {frame_id:06d}"
            )
        seen_frame_ids.add(frame_id)
        stem = f"frame_{frame_id:06d}"
        label_path = run_dir / "labels" / f"{stem}.txt"
        image_path = run_dir / "images" / f"{stem}.png"
        if not label_path.is_file() or not image_path.is_file():
            raise ProfileBalancedOverlaySelectionError(
                f"manifest frame {frame_id:06d} has no matching image and label pair"
            )
        candidates.append(
            _ProfileBalancedCandidate(
                frame_id=frame_id,
                profile=profile,
                lighting_band=payload.get("lighting_band"),
                objects=objects,
                label_path=label_path,
                image_path=image_path,
            )
        )
    return candidates


def _select_profile_balanced_candidates(
    candidates: list[_ProfileBalancedCandidate], limit: int, seed: int
) -> list[_ProfileBalancedCandidate]:
    expected_limit = len(PROFILE_ORDER) * PROFILE_QUOTA
    if limit != expected_limit:
        raise ProfileBalancedOverlaySelectionError(
            f"profile-balanced overlays require limit={expected_limit}, got {limit}"
        )

    candidates_by_profile = {
        profile: sorted(
            (candidate for candidate in candidates if candidate.profile == profile),
            key=lambda candidate: candidate.frame_id,
        )
        for profile in PROFILE_ORDER
    }
    for profile, profile_candidates in candidates_by_profile.items():
        if len(profile_candidates) < PROFILE_QUOTA:
            raise ProfileBalancedOverlaySelectionError(
                f"profile {profile} has {len(profile_candidates)} frames; requires {PROFILE_QUOTA}"
            )

    for category in REQUIRED_COVERAGE_CATEGORIES:
        if not any(_candidate_has_category(candidate, category) for candidate in candidates):
            raise ProfileBalancedOverlaySelectionError(
                f"required profile-balanced coverage category is absent: {category}"
            )

    selected_by_frame_id: dict[int, _ProfileBalancedCandidate] = {}
    for category in REQUIRED_COVERAGE_CATEGORIES:
        if any(_candidate_has_category(candidate, category) for candidate in selected_by_frame_id.values()):
            continue
        matching = [candidate for candidate in candidates if _candidate_has_category(candidate, category)]
        random.Random(f"{seed}:{category}").shuffle(matching)
        selected_by_frame_id[matching[0].frame_id] = matching[0]

    for profile in PROFILE_ORDER:
        selected_count = sum(
            candidate.profile == profile for candidate in selected_by_frame_id.values()
        )
        remaining = [
            candidate
            for candidate in candidates_by_profile[profile]
            if candidate.frame_id not in selected_by_frame_id
        ]
        selected = random.Random(f"{seed}:{profile}").sample(
            remaining, PROFILE_QUOTA - selected_count
        )
        selected_by_frame_id.update({candidate.frame_id: candidate for candidate in selected})

    return sorted(selected_by_frame_id.values(), key=lambda candidate: candidate.frame_id)


def _render_candidate_overlays(
    candidates: list[_ProfileBalancedCandidate], output_dir: Path
) -> list[Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    rendered_paths: list[Path] = []
    for candidate in candidates:
        boxes = parse_yolo_label(candidate.label_path)
        with Image.open(candidate.image_path) as image:
            overlay = image.convert("RGB")
            draw = ImageDraw.Draw(overlay)
            for box in boxes:
                left, top, right, bottom = _to_pixels(box, overlay.size)
                color = COLORS[box.class_id]
                draw.rectangle((left, top, right, bottom), outline=color, width=2)
                class_name = PokemonClass(box.class_id).name.lower()
                draw.text(
                    (left, max(0.0, top - 12.0)),
                    (
                        f"{class_name} {box.center_x:.6f} {box.center_y:.6f} "
                        f"{box.width:.6f} {box.height:.6f}"
                    ),
                    fill=color,
                )
            output_path = output_dir / f"frame_{candidate.frame_id:06d}.png"
            overlay.save(output_path)
            rendered_paths.append(output_path)
    return rendered_paths


def render_profile_balanced_overlays(
    run_dir: Path, output_dir: Path, limit: int, seed: int
) -> ProfileBalancedOverlaySelection:
    """Render a deterministic five-profile review sample with required category coverage.

    A near-edge object has any normalized YOLO box edge within five percent of an
    image boundary. The returned coverage records that definition and the selected
    frame IDs so manual review has auditable evidence of the required sample.
    """

    selected = _select_profile_balanced_candidates(
        _load_profile_balanced_candidates(run_dir), limit, seed
    )
    profile_counts = {
        profile: sum(candidate.profile == profile for candidate in selected)
        for profile in PROFILE_ORDER
    }
    category_counts = {
        category: sum(_candidate_has_category(candidate, category) for candidate in selected)
        for category in REQUIRED_COVERAGE_CATEGORIES
    }
    if any(count != PROFILE_QUOTA for count in profile_counts.values()):
        raise ProfileBalancedOverlaySelectionError("profile-balanced selection did not meet every profile quota")
    if any(count < 1 for count in category_counts.values()):
        raise ProfileBalancedOverlaySelectionError("profile-balanced selection did not meet every coverage category")
    coverage: dict[str, object] = {
        "mode": "profile_balanced",
        "seed": seed,
        "profile_quota": PROFILE_QUOTA,
        "required_profiles": list(PROFILE_ORDER),
        "required_categories": list(REQUIRED_COVERAGE_CATEGORIES),
        "profile_counts": profile_counts,
        "category_counts": category_counts,
        "near_edge_definition": NEAR_EDGE_DEFINITION,
        "selected_frame_ids": [candidate.frame_id for candidate in selected],
    }
    return ProfileBalancedOverlaySelection(
        paths=_render_candidate_overlays(selected, output_dir), coverage=coverage
    )


def _to_pixels(box: YoloBox, image_size: tuple[int, int]) -> tuple[float, float, float, float]:
    width, height = image_size
    left = (box.center_x - box.width / 2.0) * width
    top = (box.center_y - box.height / 2.0) * height
    right = (box.center_x + box.width / 2.0) * width
    bottom = (box.center_y + box.height / 2.0) * height
    return left, top, right, bottom


def render_overlays(run_dir: Path, output_dir: Path, limit: int, seed: int) -> list[Path]:
    labels_dir = run_dir / "labels"
    images_dir = run_dir / "images"
    output_dir.mkdir(parents=True, exist_ok=True)

    candidates = sorted(path for path in labels_dir.rglob("*.txt"))
    if limit < len(candidates):
        candidates = sorted(random.Random(seed).sample(candidates, limit))

    images_by_stem = {
        path.relative_to(images_dir).with_suffix(""): path
        for path in images_dir.rglob("*")
        if path.is_file() and path.suffix.lower() in IMAGE_SUFFIXES
    }

    rendered_paths: list[Path] = []
    for label_path in candidates[:limit]:
        relative_stem = label_path.relative_to(labels_dir).with_suffix("")
        image_path = images_by_stem[relative_stem]
        boxes = parse_yolo_label(label_path)
        with Image.open(image_path) as image:
            overlay = image.convert("RGB")
            draw = ImageDraw.Draw(overlay)
            for box in boxes:
                left, top, right, bottom = _to_pixels(box, overlay.size)
                color = COLORS[box.class_id]
                draw.rectangle((left, top, right, bottom), outline=color, width=2)
                class_name = PokemonClass(box.class_id).name.lower()
                draw.text(
                    (left, max(0.0, top - 12.0)),
                    (
                        f"{class_name} {box.center_x:.6f} {box.center_y:.6f} "
                        f"{box.width:.6f} {box.height:.6f}"
                    ),
                    fill=color,
                )
            output_path = (output_dir / relative_stem).with_suffix(".png")
            output_path.parent.mkdir(parents=True, exist_ok=True)
            overlay.save(output_path)
            rendered_paths.append(output_path)
    return rendered_paths
