import json
from pathlib import Path

import pytest

from pokemon_detector.cli.visual_review import main


def create_validated_run(tmp_path: Path, overlay_count: int = 100) -> Path:
    run_dir = tmp_path / "visual-review-100"
    overlays_dir = run_dir / "validation-overlays"
    overlays_dir.mkdir(parents=True)
    overlays = [f"frame_{frame_id:06d}.png" for frame_id in range(1, overlay_count + 1)]
    for overlay in overlays:
        (overlays_dir / overlay).touch()
    (run_dir / "validation-report.json").write_text(
        json.dumps(
            {
                "run_id": "visual-review-100",
                "image_count": 100,
                "label_count": 100,
                "errors": [],
                "passed": True,
                "overlays": overlays,
            }
        ),
        encoding="utf-8",
    )
    return run_dir


def test_visual_review_cli_accepts_complete_review_and_records_all_counts(tmp_path: Path) -> None:
    run_dir = create_validated_run(tmp_path)

    exit_code = main(
        [
            str(run_dir),
            "--wrong-class",
            "0",
            "--loose-box",
            "2",
            "--clipped-box",
            "1",
            "--missing-object",
            "0",
            "--invisible-object",
            "0",
            "--texture-failure",
            "0",
            "--implausible-scene",
            "3",
        ]
    )

    assert exit_code == 0
    payload = json.loads((run_dir / "visual-review-report.json").read_text(encoding="utf-8"))
    assert payload["accepted"] is True
    assert payload["reviewed_overlay_count"] == 100
    assert payload["counts"] == {
        "wrong_class": 0,
        "loose_box": 2,
        "clipped_box": 1,
        "missing_object": 0,
        "invisible_object": 0,
        "texture_failure": 0,
        "implausible_scene": 3,
    }


def test_visual_review_cli_accepts_100_overlays_from_larger_dataset(tmp_path: Path) -> None:
    run_dir = create_validated_run(tmp_path)
    report_path = run_dir / "validation-report.json"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report["image_count"] = 3900
    report["label_count"] = 3900
    report_path.write_text(json.dumps(report), encoding="utf-8")

    exit_code = main(
        [
            str(run_dir),
            "--wrong-class",
            "0",
            "--loose-box",
            "0",
            "--clipped-box",
            "0",
            "--missing-object",
            "0",
            "--invisible-object",
            "0",
            "--texture-failure",
            "0",
            "--implausible-scene",
            "0",
        ]
    )

    assert exit_code == 0
    payload = json.loads((run_dir / "visual-review-report.json").read_text(encoding="utf-8"))
    assert payload["accepted"] is True
    assert payload["reviewed_overlay_count"] == 100


def test_visual_review_cli_rejects_incomplete_or_failed_gate(tmp_path: Path) -> None:
    run_dir = create_validated_run(tmp_path, overlay_count=99)

    exit_code = main(
        [
            str(run_dir),
            "--wrong-class",
            "1",
            "--loose-box",
            "0",
            "--clipped-box",
            "0",
            "--missing-object",
            "1",
            "--invisible-object",
            "0",
            "--texture-failure",
            "0",
            "--implausible-scene",
            "0",
        ]
    )

    assert exit_code == 1
    payload = json.loads((run_dir / "visual-review-report.json").read_text(encoding="utf-8"))
    assert payload["accepted"] is False
    assert "expected exactly 100 overlays" in payload["reasons"]
    assert "wrong class count must be zero" in payload["reasons"]
    assert "missing object count must be zero" in payload["reasons"]


def test_visual_review_cli_rejects_nonzero_invisible_object_count(tmp_path: Path) -> None:
    run_dir = create_validated_run(tmp_path)

    exit_code = main(
        [
            str(run_dir),
            "--wrong-class",
            "0",
            "--loose-box",
            "0",
            "--clipped-box",
            "0",
            "--missing-object",
            "0",
            "--invisible-object",
            "1",
            "--texture-failure",
            "0",
            "--implausible-scene",
            "0",
        ]
    )

    assert exit_code == 1
    payload = json.loads((run_dir / "visual-review-report.json").read_text(encoding="utf-8"))
    assert payload["counts"]["invisible_object"] == 1
    assert "invisible object count must be zero" in payload["reasons"]


def test_visual_review_cli_rejects_balanced_mixed_device_report_missing_required_coverage(
    tmp_path: Path,
) -> None:
    run_dir = create_validated_run(tmp_path)
    report_path = run_dir / "validation-report.json"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report["overlay_coverage"] = {
        "mode": "profile_balanced",
        "profile_quota": 20,
        "profile_counts": {
            "Square": 20,
            "IPhonePortrait": 20,
            "IPhoneLandscape": 20,
            "Webcam": 20,
            "LaptopWindow": 20,
        },
        "category_counts": {
            "low_light": 1,
            "small": 1,
            "large": 1,
            "near_edge": 0,
            "negative": 1,
        },
    }
    report_path.write_text(json.dumps(report), encoding="utf-8")

    exit_code = main(
        [
            str(run_dir),
            "--wrong-class",
            "0",
            "--loose-box",
            "0",
            "--clipped-box",
            "0",
            "--missing-object",
            "0",
            "--invisible-object",
            "0",
            "--texture-failure",
            "0",
            "--implausible-scene",
            "0",
        ]
    )

    assert exit_code == 1
    payload = json.loads((run_dir / "visual-review-report.json").read_text(encoding="utf-8"))
    assert "mixed-device balanced review is missing required near_edge coverage" in payload["reasons"]


@pytest.mark.parametrize(
    "coverage",
    [None, {}, {"mode": "unstratified"}],
    ids=["absent", "missing-mode", "invalid-mode"],
)
def test_visual_review_cli_rejects_mixed_device_reports_without_profile_balanced_evidence(
    tmp_path: Path, coverage: dict[str, object] | None
) -> None:
    run_dir = create_validated_run(tmp_path)
    report_path = run_dir / "validation-report.json"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report["run_id"] = "mixed-device-6000-640-final-0907"
    report["capture_profile_counts"] = {
        "Square": 20,
        "IPhonePortrait": 20,
        "IPhoneLandscape": 20,
        "Webcam": 20,
        "LaptopWindow": 20,
    }
    if coverage is not None:
        report["overlay_coverage"] = coverage
    report_path.write_text(json.dumps(report), encoding="utf-8")

    exit_code = main(
        [
            str(run_dir),
            "--wrong-class",
            "0",
            "--loose-box",
            "0",
            "--clipped-box",
            "0",
            "--missing-object",
            "0",
            "--invisible-object",
            "0",
            "--texture-failure",
            "0",
            "--implausible-scene",
            "0",
        ]
    )

    assert exit_code == 1
    payload = json.loads((run_dir / "visual-review-report.json").read_text(encoding="utf-8"))
    assert "mixed-device review requires profile-balanced overlay coverage evidence" in payload["reasons"]
