"""Validate a generated YOLO dataset run and render overlay samples."""

from __future__ import annotations

import argparse
import json
from collections.abc import Sequence
from pathlib import Path

from pokemon_detector.dataset.overlay import (
    ProfileBalancedOverlaySelectionError,
    render_overlays,
    render_profile_balanced_overlays,
)
from pokemon_detector.dataset.validator import ValidationIssue, validate_run

DEFAULT_REAL_OVERLAY_SEED = 42


def _write_report(run_dir: Path, payload: dict[str, object]) -> None:
    report_path = run_dir / "validation-report.json"
    report_path.write_text(json.dumps(payload, indent=2, sort_keys=False) + "\n", encoding="utf-8")


def _load_overlay_seed(run_dir: Path) -> int:
    config_path = run_dir / "run-config.json"
    try:
        payload = json.loads(config_path.read_text(encoding="utf-8"))
    except (FileNotFoundError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError(f"run-config.json is invalid: {error}") from error

    try:
        seed = payload["Seed"]
    except KeyError as error:
        raise ValueError("run-config.json is invalid: missing Seed") from error
    if isinstance(seed, bool) or not isinstance(seed, int):
        raise TypeError("run-config.json is invalid: Seed must be an integer")
    return seed


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_dir", type=Path)
    parser.add_argument("--overlay-count", type=int, default=9)
    parser.add_argument("--seed", type=int, help="override the overlay sampling seed")
    parser.add_argument(
        "--profile-balanced",
        action="store_true",
        help="select exactly 20 overlays per documented mixed-device capture profile",
    )
    parser.add_argument("--expected-frame-count", type=int, help="require this count and the mixed-device distribution gates")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    run_dir: Path = args.run_dir
    report = validate_run(run_dir, expected_frame_count=args.expected_frame_count)

    overlay_paths = []
    overlay_coverage: dict[str, object] | None = None
    if report.passed and args.overlay_count > 0:
        overlay_seed = args.seed
        if overlay_seed is None:
            if (run_dir / "manifest.jsonl").exists() or (run_dir / "run-config.json").exists():
                try:
                    overlay_seed = _load_overlay_seed(run_dir)
                except (TypeError, ValueError) as error:
                    payload = report.to_dict()
                    payload["errors"] = [ValidationIssue(code="INVALID_RUN_CONFIG", message=str(error), path=str(run_dir / "run-config.json")).to_dict()]
                    payload["warnings"] = [issue.to_dict() for issue in report.warnings]
                    payload["passed"] = False
                    payload["overlays"] = []
                    _write_report(run_dir, payload)
                    return 1
            else:
                overlay_seed = DEFAULT_REAL_OVERLAY_SEED

        try:
            if args.profile_balanced:
                selection = render_profile_balanced_overlays(
                    run_dir=run_dir,
                    output_dir=run_dir / "validation-overlays",
                    limit=args.overlay_count,
                    seed=overlay_seed,
                )
                overlay_paths = selection.paths
                overlay_coverage = selection.coverage
            else:
                overlay_paths = render_overlays(
                    run_dir=run_dir,
                    output_dir=run_dir / "validation-overlays",
                    limit=args.overlay_count,
                    seed=overlay_seed,
                )
        except ProfileBalancedOverlaySelectionError as error:
            payload = report.to_dict()
            payload["errors"] = [
                *payload["errors"],
                ValidationIssue(code="OVERLAY_COVERAGE", message=str(error)).to_dict(),
            ]
            payload["passed"] = False
            payload["overlays"] = []
            payload["overlay_coverage"] = {"mode": "profile_balanced", "error": str(error)}
            _write_report(run_dir, payload)
            return 1

    payload = report.to_dict()
    payload["overlays"] = [
        path.relative_to(run_dir / "validation-overlays").as_posix() for path in overlay_paths
    ]
    if overlay_coverage is not None:
        payload["overlay_coverage"] = overlay_coverage
    _write_report(run_dir, payload)
    return 0 if report.passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
