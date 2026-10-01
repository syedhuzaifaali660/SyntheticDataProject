import json
from pathlib import Path

import pytest
from PIL import Image

from pokemon_detector.cli.validate import main
from pokemon_detector.dataset.validator import validate_run


def write_png(path: Path, size: tuple[int, int] = (32, 32), color: tuple[int, int, int] = (255, 255, 255)) -> None:
    image = Image.new("RGB", size, color)
    image.putpixel((0, 0), (0, 0, 0))
    image.save(path)


def write_run_config(path: Path) -> None:
    path.write_text('{"Seed":42,"Width":32,"Height":32}', encoding="utf-8")


def write_manifest(path: Path, lines: list[str]) -> None:
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def create_valid_run(tmp_path: Path) -> Path:
    run_dir = tmp_path / "run"
    images_dir = run_dir / "images"
    labels_dir = run_dir / "labels"
    images_dir.mkdir(parents=True)
    labels_dir.mkdir()
    write_run_config(run_dir / "run-config.json")
    write_png(images_dir / "frame_000001.png")
    write_png(images_dir / "frame_000002.png")
    (labels_dir / "frame_000001.txt").write_text("0 0.500000 0.500000 0.250000 0.250000\n", encoding="utf-8")
    (labels_dir / "frame_000002.txt").write_text("", encoding="utf-8")
    write_manifest(
        run_dir / "manifest.jsonl",
        [
            '{"run_id":"run","frame_id":1,"seed":-1,"split_hint":"train","background_id":"bg-1","width":32,"height":32,"objects":[{"class_id":0,"class_name":"pikachu","bbox":[0.5,0.5,0.25,0.25]}]}',
            '{"run_id":"run","frame_id":2,"seed":2,"split_hint":"train","background_id":"bg-2","width":32,"height":32,"objects":[]}',
        ],
    )
    return run_dir


def test_incomplete_production_capture_cannot_pass_validation(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)
    manifest = run_dir / "manifest.jsonl"
    manifest.write_text(manifest.read_text().replace('"run_id":"run"', '"run_id":"mixed-device-6000-640"'))
    report = validate_run(run_dir)
    assert not report.passed
    assert "FRAME_COUNT" in {issue.code for issue in report.errors}


def test_explicit_mixed_run_gate_rejects_missing_strata(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)
    report = validate_run(run_dir, expected_frame_count=2)
    assert {"PROFILE_DISTRIBUTION", "LIGHTING_DISTRIBUTION", "SIZE_DISTRIBUTION"} <= {
        issue.code for issue in report.errors
    }


def test_validate_run_accepts_valid_fixture_and_writes_passing_report(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)

    report = validate_run(run_dir)

    assert report.run_id == "run"
    assert report.image_count == 2
    assert report.label_count == 2
    assert report.object_count_by_class == {0: 1, 1: 0, 2: 0}
    assert report.negative_image_count == 1
    assert report.errors == ()
    assert [issue.code for issue in report.warnings] == ["MISSING_CLASS_BALANCE"]
    assert report.passed is True

    exit_code = main([str(run_dir), "--overlay-count", "1"])

    assert exit_code == 0
    report_payload = json.loads((run_dir / "validation-report.json").read_text(encoding="utf-8"))
    assert report_payload["passed"] is True
    assert report_payload["image_count"] == 2
    assert len(report_payload["overlays"]) == 1


def test_validate_run_without_generated_metadata_is_invalid(tmp_path: Path) -> None:
    run_dir = tmp_path / "run"
    run_dir.mkdir()

    report = validate_run(run_dir)

    assert not report.passed
    assert report.errors[0].code == "INVALID_MANIFEST"


def test_validate_cli_uses_explicit_overlay_seed(tmp_path: Path, monkeypatch) -> None:
    run_dir = create_valid_run(tmp_path)
    captured_seed = None

    def fake_render_overlays(*, run_dir: Path, output_dir: Path, limit: int, seed: int):
        nonlocal captured_seed
        captured_seed = seed
        return []

    monkeypatch.setattr("pokemon_detector.cli.validate.render_overlays", fake_render_overlays)

    exit_code = main([str(run_dir), "--overlay-count", "1", "--seed", "7"])

    assert exit_code == 0
    assert captured_seed == 7


def test_validate_run_warns_when_fixed_classes_are_missing(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)

    report = validate_run(run_dir)

    assert [issue.code for issue in report.warnings] == ["MISSING_CLASS_BALANCE"]
    assert "charmander" in report.warnings[0].message
    assert "squirtle" in report.warnings[0].message


def test_validate_run_reports_manifest_only_frame(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)
    write_manifest(
        run_dir / "manifest.jsonl",
        [
            '{"run_id":"run","frame_id":1,"seed":-1,"split_hint":"train","background_id":"bg-1","width":32,"height":32,"objects":[{"class_id":0,"class_name":"pikachu","bbox":[0.5,0.5,0.25,0.25]}]}',
            '{"run_id":"run","frame_id":2,"seed":2,"split_hint":"train","background_id":"bg-2","width":32,"height":32,"objects":[]}',
            '{"run_id":"run","frame_id":3,"seed":3,"split_hint":"train","background_id":"bg-3","width":32,"height":32,"objects":[]}',
        ],
    )

    report = validate_run(run_dir)

    assert [issue.code for issue in report.errors] == ["MANIFEST_FILE_MISSING"]


def test_validate_run_reports_malformed_manifest_json_line(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)
    (run_dir / "manifest.jsonl").write_text('{"run_id":"run"\n', encoding="utf-8")

    report = validate_run(run_dir)

    assert [issue.code for issue in report.errors] == ["INVALID_MANIFEST"]


def test_validate_run_rejects_duplicate_manifest_frame_id_with_line_number_and_frame_id(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)
    write_manifest(
        run_dir / "manifest.jsonl",
        [
            '{"run_id":"run","frame_id":1,"seed":-1,"split_hint":"train","background_id":"bg-1","width":32,"height":32,"objects":[{"class_id":0,"class_name":"pikachu","bbox":[0.5,0.5,0.25,0.25]}]}',
            '{"run_id":"run","frame_id":1,"seed":2,"split_hint":"train","background_id":"bg-2","width":32,"height":32,"objects":[{"class_id":0,"class_name":"pikachu","bbox":[0.5,0.5,0.25,0.25]}]}',
        ],
    )

    report = validate_run(run_dir)

    assert report.passed is False
    assert [issue.code for issue in report.errors] == ["INVALID_MANIFEST"]
    assert "line 2" in report.errors[0].message
    assert "frame 000001" in report.errors[0].message


def test_validate_run_reports_invalid_utf8_label_file(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)
    (run_dir / "labels" / "frame_000001.txt").write_bytes(b"\xff\xfe\xfd")

    report = validate_run(run_dir)

    assert [issue.code for issue in report.errors] == ["INVALID_LABEL_ENCODING"]


def test_validate_run_rejects_uniform_null_render_image(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)
    Image.new("RGB", (32, 32), (205, 205, 205)).save(
        run_dir / "images" / "frame_000001.png"
    )

    report = validate_run(run_dir)

    assert report.passed is False
    assert [issue.code for issue in report.errors] == ["UNIFORM_IMAGE"]
    assert report.errors[0].frame_id == 1


def test_validate_run_reports_unreadable_image_when_image_plugin_is_missing(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    run_dir = create_valid_run(tmp_path)

    def missing_image_plugin(*args: object, **kwargs: object) -> None:
        raise ModuleNotFoundError("pi_heif")

    monkeypatch.setattr(Image, "open", missing_image_plugin)

    report = validate_run(run_dir)

    assert "UNREADABLE_IMAGE" in {issue.code for issue in report.errors}


@pytest.mark.parametrize(
    ("mutator", "expected_code"),
    [
        (lambda run_dir: (run_dir / "labels" / "frame_000001.txt").unlink(), "MISSING_LABEL"),
        (
            lambda run_dir: (run_dir / "labels" / "frame_000003.txt").write_text("", encoding="utf-8"),
            "ORPHAN_LABEL",
        ),
        (
            lambda run_dir: (run_dir / "images" / "frame_000001.png").write_bytes(b"not-a-real-png"),
            "UNREADABLE_IMAGE",
        ),
        (
            lambda run_dir: (run_dir / "manifest.jsonl").write_text(
                '{"run_id":"run","frame_id":2,"seed":2,"split_hint":"train","background_id":"bg-2","width":32,"height":32,"objects":[]}\n',
                encoding="utf-8",
            ),
            "MANIFEST_FRAME_MISSING",
        ),
        (
            lambda run_dir: (run_dir / "labels" / "frame_000001.txt").write_text(
                "3 0.500000 0.500000 0.250000 0.250000\n",
                encoding="utf-8",
            ),
            "INVALID_CLASS_ID",
        ),
        (
            lambda run_dir: (run_dir / "labels" / "frame_000001.txt").write_text(
                "0 0.950000 0.500000 0.200000 0.200000\n",
                encoding="utf-8",
            ),
            "INVALID_BOX",
        ),
    ],
)
def test_validate_run_reports_expected_structural_errors(
    tmp_path: Path, mutator, expected_code: str
) -> None:
    run_dir = create_valid_run(tmp_path)
    mutator(run_dir)

    report = validate_run(run_dir)

    assert report.passed is False
    assert [issue.code for issue in report.errors] == [expected_code]


def test_validate_cli_writes_controlled_error_report_for_malformed_run_config(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)
    (run_dir / "run-config.json").write_text("{", encoding="utf-8")

    exit_code = main([str(run_dir), "--overlay-count", "1"])

    assert exit_code == 1
    report_payload = json.loads((run_dir / "validation-report.json").read_text(encoding="utf-8"))
    assert report_payload["passed"] is False
    assert [issue["code"] for issue in report_payload["errors"]] == ["INVALID_RUN_CONFIG"]


def test_validate_cli_writes_controlled_error_report_for_missing_run_config_seed(tmp_path: Path) -> None:
    run_dir = create_valid_run(tmp_path)
    (run_dir / "run-config.json").write_text('{"Width":32,"Height":32}', encoding="utf-8")

    exit_code = main([str(run_dir), "--overlay-count", "1"])

    assert exit_code == 1
    report_payload = json.loads((run_dir / "validation-report.json").read_text(encoding="utf-8"))
    assert report_payload["passed"] is False
    assert [issue["code"] for issue in report_payload["errors"]] == ["INVALID_RUN_CONFIG"]
