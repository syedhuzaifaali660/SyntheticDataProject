"""Record the manual gate for 100 overlays from a validated dataset run."""

from __future__ import annotations

import argparse
import json
from collections.abc import Sequence
from pathlib import Path

REQUIRED_REVIEW_COUNT = 100
MIXED_DEVICE_PROFILES = ("Square", "IPhonePortrait", "IPhoneLandscape", "Webcam", "LaptopWindow")
MIXED_DEVICE_COVERAGE_CATEGORIES = ("low_light", "small", "large", "near_edge", "negative")
MIXED_DEVICE_PROFILE_QUOTA = 20


def _nonnegative_int(value: str) -> int:
    parsed = int(value)
    if parsed < 0:
        raise argparse.ArgumentTypeError("count must be nonnegative")
    return parsed


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_dir", type=Path)
    parser.add_argument("--wrong-class", type=_nonnegative_int, required=True)
    parser.add_argument("--loose-box", type=_nonnegative_int, required=True)
    parser.add_argument("--clipped-box", type=_nonnegative_int, required=True)
    parser.add_argument("--missing-object", type=_nonnegative_int, required=True)
    parser.add_argument("--invisible-object", type=_nonnegative_int, required=True)
    parser.add_argument("--texture-failure", type=_nonnegative_int, required=True)
    parser.add_argument("--implausible-scene", type=_nonnegative_int, required=True)
    return parser


def _load_validation_report(run_dir: Path) -> dict[str, object]:
    report_path = run_dir / "validation-report.json"
    try:
        payload = json.loads(report_path.read_text(encoding="utf-8"))
    except (FileNotFoundError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError(f"validation-report.json is invalid: {error}") from error
    if not isinstance(payload, dict):
        raise TypeError("validation-report.json must contain an object")
    return payload


def _is_mixed_device_report(validation_report: dict[str, object]) -> bool:
    run_id = validation_report.get("run_id")
    if isinstance(run_id, str) and run_id.startswith("mixed-device-"):
        return True
    profile_counts = validation_report.get("capture_profile_counts")
    return isinstance(profile_counts, dict) and all(
        profile in profile_counts for profile in MIXED_DEVICE_PROFILES
    )


def _review_reasons(
    run_dir: Path,
    validation_report: dict[str, object],
    counts: dict[str, int],
) -> list[str]:
    reasons: list[str] = []
    if validation_report.get("passed") is not True:
        reasons.append("dataset validation must pass")

    overlays = validation_report.get("overlays")
    if not isinstance(overlays, list) or len(overlays) != REQUIRED_REVIEW_COUNT:
        reasons.append("expected exactly 100 overlays")
    elif (
        len(set(overlays)) != REQUIRED_REVIEW_COUNT
        or any(not isinstance(name, str) for name in overlays)
        or any(not (run_dir / "validation-overlays" / name).is_file() for name in overlays)
    ):
        reasons.append("all 100 overlay records must be unique existing files")

    if counts["wrong_class"] != 0:
        reasons.append("wrong class count must be zero")
    if counts["missing_object"] != 0:
        reasons.append("missing object count must be zero")
    if counts["invisible_object"] != 0:
        reasons.append("invisible object count must be zero")
    coverage = validation_report.get("overlay_coverage")
    if _is_mixed_device_report(validation_report) and (
        not isinstance(coverage, dict) or coverage.get("mode") != "profile_balanced"
    ):
        reasons.append("mixed-device review requires profile-balanced overlay coverage evidence")
    elif isinstance(coverage, dict) and coverage.get("mode") == "profile_balanced":
        if coverage.get("profile_quota") != MIXED_DEVICE_PROFILE_QUOTA:
            reasons.append("mixed-device balanced review must use a profile quota of 20")
        profile_counts = coverage.get("profile_counts")
        if not isinstance(profile_counts, dict):
            reasons.append("mixed-device balanced review is missing profile coverage counts")
        else:
            for profile in MIXED_DEVICE_PROFILES:
                if profile_counts.get(profile) != MIXED_DEVICE_PROFILE_QUOTA:
                    reasons.append(
                        f"mixed-device balanced review is missing required {profile} profile quota"
                    )
        category_counts = coverage.get("category_counts")
        if not isinstance(category_counts, dict):
            reasons.append("mixed-device balanced review is missing category coverage counts")
        else:
            for category in MIXED_DEVICE_COVERAGE_CATEGORIES:
                if not isinstance(category_counts.get(category), int) or category_counts[category] < 1:
                    reasons.append(
                        f"mixed-device balanced review is missing required {category} coverage"
                    )
    return reasons


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    run_dir: Path = args.run_dir
    counts = {
        "wrong_class": args.wrong_class,
        "loose_box": args.loose_box,
        "clipped_box": args.clipped_box,
        "missing_object": args.missing_object,
        "invisible_object": args.invisible_object,
        "texture_failure": args.texture_failure,
        "implausible_scene": args.implausible_scene,
    }

    try:
        validation_report = _load_validation_report(run_dir)
        reasons = _review_reasons(run_dir, validation_report, counts)
        run_id = validation_report.get("run_id", run_dir.name)
        overlays = validation_report.get("overlays")
        reviewed_overlay_count = len(overlays) if isinstance(overlays, list) else 0
    except (TypeError, ValueError) as error:
        reasons = [str(error)]
        run_id = run_dir.name
        reviewed_overlay_count = 0

    payload = {
        "run_id": run_id,
        "reviewed_overlay_count": reviewed_overlay_count,
        "counts": counts,
        "accepted": not reasons,
        "reasons": reasons,
    }
    output_path = run_dir / "visual-review-report.json"
    output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return 0 if payload["accepted"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
