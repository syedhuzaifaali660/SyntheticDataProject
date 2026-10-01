import json
from pathlib import Path

import pytest
from PIL import Image

from pokemon_detector.cli.split import main
from pokemon_detector.dataset.splitter import SplitConfig, split_run
from pokemon_detector.dataset.validator import validate_run


def write_png(path: Path, color: tuple[int, int, int]) -> None:
    image = Image.new("RGB", (32, 32), color)
    image.putpixel((0, 0), (0, 0, 0))
    image.save(path)


def write_run_config(path: Path) -> None:
    path.write_text('{"Seed":42,"Width":32,"Height":32}', encoding="utf-8")


def write_manifest(path: Path, lines: list[str]) -> None:
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_valid_run(tmp_path: Path, backgrounds: list[tuple[str, int]]) -> Path:
    run_dir = tmp_path / "run"
    images_dir = run_dir / "images"
    labels_dir = run_dir / "labels"
    images_dir.mkdir(parents=True)
    labels_dir.mkdir()
    write_run_config(run_dir / "run-config.json")
    (run_dir / "validation-report.json").write_text(
        json.dumps(
            {
                "run_id": "run-123",
                "image_count": sum(frame_count for _, frame_count in backgrounds),
                "label_count": sum(frame_count for _, frame_count in backgrounds),
                "object_count_by_class": {"0": 1, "1": 1, "2": 1},
                "negative_image_count": 0,
                "errors": [],
                "warnings": [],
                "passed": True,
            }
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_lines: list[str] = []
    frame_id = 1
    for background_id, frame_count in backgrounds:
        for _ in range(frame_count):
            write_png(images_dir / f"frame_{frame_id:06d}.png", (frame_id, frame_id, frame_id))
            (labels_dir / f"frame_{frame_id:06d}.txt").write_text(
                "0 0.500000 0.500000 0.250000 0.250000\n",
                encoding="utf-8",
            )
            manifest_lines.append(
                json.dumps(
                    {
                        "run_id": "run-123",
                        "frame_id": frame_id,
                        "seed": 99,
                        "split_hint": "train",
                        "background_id": background_id,
                        "width": 32,
                        "height": 32,
                        "objects": [
                            {
                                "class_id": 0,
                                "class_name": "pikachu",
                                "bbox": [0.5, 0.5, 0.25, 0.25],
                            }
                        ],
                    }
                )
            )
            frame_id += 1
    write_manifest(run_dir / "manifest.jsonl", manifest_lines)
    return run_dir


def build_valid_mixed_run(tmp_path: Path, backgrounds: list[tuple[str, int]]) -> Path:
    run_dir = build_valid_run(tmp_path, backgrounds)
    manifest_path = run_dir / "manifest.jsonl"
    records = [json.loads(line) for line in manifest_path.read_text(encoding="utf-8").splitlines()]
    profiles = (
        ["Square"] * 9
        + ["IPhonePortrait"] * 15
        + ["IPhoneLandscape"] * 12
        + ["Webcam"] * 12
        + ["LaptopWindow"] * 12
    )
    lighting = ["Low"] * 12 + ["Normal"] * 36 + ["Bright"] * 12
    sizes = ["Small"] * 18 + ["Medium"] * 30 + ["Large"] * 12
    classes = [0] * 20 + [1] * 20 + [2] * 20
    for record, profile, lighting_band, size_band, class_id in zip(
        records, profiles, lighting, sizes, classes, strict=True
    ):
        record["run_id"] = "mixed-device-6000-640-final-0907"
        record["capture_profile"] = profile
        record["lighting_band"] = lighting_band
        record["objects"][0]["class_id"] = class_id
        record["objects"][0]["class_name"] = ("pikachu", "charmander", "squirtle")[class_id]
        record["objects"][0]["size_band"] = size_band
        (run_dir / "labels" / f"frame_{record['frame_id']:06d}.txt").write_text(
            f"{class_id} 0.500000 0.500000 0.250000 0.250000\n", encoding="utf-8"
        )
    write_manifest(manifest_path, [json.dumps(record) for record in records])
    return run_dir


def write_split_config(path: Path, seed: object = 42, train: object = 0.7, val: object = 0.2, test: object = 0.1) -> Path:
    path.write_text(
        f"seed: {seed}\ntrain_ratio: {train}\nval_ratio: {val}\ntest_ratio: {test}\n",
        encoding="utf-8",
    )
    return path


def test_split_run_keeps_each_background_in_exactly_one_partition_and_writes_reports(tmp_path: Path) -> None:
    run_dir = build_valid_run(
        tmp_path,
        [
            ("bg-00", 2),
            ("bg-01", 1),
            ("bg-02", 3),
            ("bg-03", 2),
            ("bg-04", 1),
            ("bg-05", 2),
            ("bg-06", 1),
            ("bg-07", 2),
            ("bg-08", 3),
            ("bg-09", 1),
        ],
    )
    output_dir = tmp_path / "split-output"
    config = SplitConfig(seed=42, train_ratio=0.7, val_ratio=0.2, test_ratio=0.1)

    report = split_run(run_dir, output_dir, config)

    assert report.run_id == "run-123"
    assert report.seed == 42
    assert report.train_background_ids == (
        "bg-02",
        "bg-03",
        "bg-05",
        "bg-06",
        "bg-07",
        "bg-08",
        "bg-09",
    )
    assert report.val_background_ids == ("bg-00", "bg-04")
    assert report.test_background_ids == ("bg-01",)
    assert report.image_counts == {"train": 14, "val": 3, "test": 1}
    assert set(report.train_background_ids).isdisjoint(report.val_background_ids)
    assert set(report.train_background_ids).isdisjoint(report.test_background_ids)
    assert set(report.val_background_ids).isdisjoint(report.test_background_ids)
    train_image_names = sorted(path.name for path in (output_dir / "images" / "train").iterdir())
    assert train_image_names == [
        "frame_000004.png",
        "frame_000005.png",
        "frame_000006.png",
        "frame_000007.png",
        "frame_000008.png",
        "frame_000010.png",
        "frame_000011.png",
        "frame_000012.png",
        "frame_000013.png",
        "frame_000014.png",
        "frame_000015.png",
        "frame_000016.png",
        "frame_000017.png",
        "frame_000018.png",
    ]
    val_image_names = sorted(path.name for path in (output_dir / "images" / "val").iterdir())
    test_image_names = sorted(path.name for path in (output_dir / "images" / "test").iterdir())
    all_image_names = train_image_names + val_image_names + test_image_names
    assert len(all_image_names) == len(set(all_image_names))
    assert len(all_image_names) == sum(report.image_counts.values())
    assert len(list((output_dir / "labels" / "train").iterdir())) == report.image_counts["train"]
    assert len(list((output_dir / "labels" / "val").iterdir())) == report.image_counts["val"]
    assert len(list((output_dir / "labels" / "test").iterdir())) == report.image_counts["test"]
    dataset_yaml = (output_dir / "dataset.yaml").read_text(encoding="utf-8")
    assert f"path: {output_dir.resolve()}" in dataset_yaml
    assert "train: images/train" in dataset_yaml
    assert "val: images/val" in dataset_yaml
    assert "test: images/test" in dataset_yaml
    assert "0: pikachu" in dataset_yaml
    assert "1: charmander" in dataset_yaml
    assert "2: squirtle" in dataset_yaml
    validation_report = json.loads((output_dir / "validation-report.json").read_text(encoding="utf-8"))
    assert validation_report["passed"] is True
    assert validation_report["errors"] == []
    report_payload = json.loads((output_dir / "split-report.json").read_text(encoding="utf-8"))
    assert report_payload["train_background_ids"] == list(report.train_background_ids)
    assert report_payload["val_background_ids"] == list(report.val_background_ids)
    assert report_payload["test_background_ids"] == list(report.test_background_ids)


def test_split_run_serializes_fresh_validation_instead_of_copying_stale_report(tmp_path: Path) -> None:
    run_dir = build_valid_run(tmp_path, [(f"bg-{index:02d}", 1) for index in range(10)])
    stale_report = {
        "run_id": "stale-run",
        "image_count": 0,
        "label_count": 0,
        "object_count_by_class": {"0": 0, "1": 0, "2": 0},
        "negative_image_count": 0,
        "errors": [{"code": "STALE_ERROR", "message": "obsolete validation result"}],
        "warnings": [],
        "passed": False,
    }
    (run_dir / "validation-report.json").write_text(
        json.dumps(stale_report) + "\n",
        encoding="utf-8",
    )
    fresh_report = validate_run(run_dir)
    assert fresh_report.passed

    split_run(
        run_dir,
        tmp_path / "split-output",
        SplitConfig(seed=42, train_ratio=0.7, val_ratio=0.2, test_ratio=0.1),
    )

    written_report = json.loads(
        (tmp_path / "split-output" / "validation-report.json").read_text(encoding="utf-8")
    )
    expected_report = json.loads(json.dumps(fresh_report.to_dict()))
    assert written_report == expected_report


def test_split_run_is_deterministic_and_uses_floor_ratio_math_for_background_counts(tmp_path: Path) -> None:
    backgrounds = [(f"bg-{index:03d}", 1) for index in range(100)]
    run_dir = build_valid_run(tmp_path, backgrounds)
    first_output = tmp_path / "first-output"
    second_output = tmp_path / "second-output"
    config = SplitConfig(seed=42, train_ratio=0.7, val_ratio=0.2, test_ratio=0.1)

    first_report = split_run(run_dir, first_output, config)
    second_report = split_run(run_dir, second_output, config)

    assert first_report == second_report
    assert len(first_report.train_background_ids) == 70
    assert len(first_report.val_background_ids) == 20
    assert len(first_report.test_background_ids) == 10
    assert first_report.train_background_ids[:5] == (
        "bg-001",
        "bg-002",
        "bg-005",
        "bg-006",
        "bg-007",
    )
    assert first_report.val_background_ids[:5] == (
        "bg-000",
        "bg-004",
        "bg-011",
        "bg-025",
        "bg-027",
    )
    assert first_report.test_background_ids == (
        "bg-003",
        "bg-013",
        "bg-014",
        "bg-017",
        "bg-028",
        "bg-031",
        "bg-035",
        "bg-081",
        "bg-086",
        "bg-094",
    )
    first_output_names = sorted(path.name for path in first_output.glob("images/*/*.png"))
    assert len(first_output_names) == len(set(first_output_names))
    assert len(first_output_names) == sum(first_report.image_counts.values())


def test_split_run_accepts_minimum_ten_backgrounds_with_floor_allocation(tmp_path: Path) -> None:
    run_dir = build_valid_run(tmp_path, [(f"bg-{index:02d}", 1) for index in range(10)])
    output_dir = tmp_path / "minimum-output"

    report = split_run(
        run_dir,
        output_dir,
        SplitConfig(seed=7, train_ratio=0.6, val_ratio=0.2, test_ratio=0.2),
    )

    assert len(report.train_background_ids) == 6
    assert len(report.val_background_ids) == 2
    assert len(report.test_background_ids) == 2


def test_split_run_honors_explicit_image_targets_without_background_leakage(tmp_path: Path) -> None:
    run_dir = build_valid_mixed_run(
        tmp_path,
        [(f"four-{index:02d}", 4) for index in range(12)]
        + [(f"three-{index:02d}", 3) for index in range(4)],
    )
    config_path = tmp_path / "exact-split.yaml"
    config_path.write_text(
        "seed: 42\n"
        "train_ratio: 0.8\n"
        "val_ratio: 0.15\n"
        "test_ratio: 0.05\n"
        "target_train_images: 48\n"
        "target_val_images: 9\n"
        "target_test_images: 3\n",
        encoding="utf-8",
    )

    report = split_run(run_dir, tmp_path / "exact-output", SplitConfig.from_yaml(config_path))

    assert report.image_counts == {"train": 48, "val": 9, "test": 3}
    assert set(report.train_background_ids).isdisjoint(report.val_background_ids)
    assert set(report.train_background_ids).isdisjoint(report.test_background_ids)
    assert set(report.val_background_ids).isdisjoint(report.test_background_ids)


def test_split_run_applies_explicit_total_to_pre_copy_validation(tmp_path: Path) -> None:
    run_dir = build_valid_run(tmp_path, [(f"bg-{index:02d}", 6) for index in range(10)])
    config_path = tmp_path / "wrong-total-split.yaml"
    config_path.write_text(
        "seed: 42\n"
        "train_ratio: 0.8\n"
        "val_ratio: 0.15\n"
        "test_ratio: 0.05\n"
        "target_train_images: 49\n"
        "target_val_images: 9\n"
        "target_test_images: 3\n",
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match="FRAME_COUNT"):
        split_run(run_dir, tmp_path / "wrong-total-output", SplitConfig.from_yaml(config_path))

    assert not (tmp_path / "wrong-total-output").exists()


@pytest.mark.parametrize(
    ("seed", "train", "val", "test", "message"),
    [
        (True, 0.7, 0.2, 0.1, "seed must be an integer"),
        (3.14, 0.7, 0.2, 0.1, "seed must be an integer"),
        (42, 0.0, 0.5, 0.5, "train_ratio must be positive"),
        (42, 0.7, float("inf"), 0.3, "val_ratio must be finite"),
        (42, 0.7, 0.2, 0.2, "ratios must sum to 1"),
    ],
)
def test_split_config_from_yaml_rejects_invalid_values(
    tmp_path: Path, seed: object, train: object, val: object, test: object, message: str
) -> None:
    config_path = write_split_config(tmp_path / "dataset-split.yaml", seed, train, val, test)

    with pytest.raises((TypeError, ValueError), match=message):
        SplitConfig.from_yaml(config_path)


def test_split_run_refuses_fewer_than_ten_unique_backgrounds(tmp_path: Path) -> None:
    run_dir = build_valid_run(tmp_path, [(f"bg-{index:02d}", 1) for index in range(9)])

    with pytest.raises(ValueError, match="at least 10 unique backgrounds"):
        split_run(run_dir, tmp_path / "too-small-output", SplitConfig(seed=42, train_ratio=0.7, val_ratio=0.2, test_ratio=0.1))


def test_split_run_refuses_invalid_run_before_copying_and_leaves_no_partial_dataset(tmp_path: Path) -> None:
    run_dir = build_valid_run(tmp_path, [(f"bg-{index:02d}", 1) for index in range(10)])
    (run_dir / "labels" / "frame_000001.txt").unlink()
    output_dir = tmp_path / "invalid-output"

    with pytest.raises(ValueError, match="validation failed"):
        split_run(run_dir, output_dir, SplitConfig(seed=42, train_ratio=0.7, val_ratio=0.2, test_ratio=0.1))

    assert not output_dir.exists()


def test_split_run_refuses_missing_source_file_before_partial_copy(tmp_path: Path) -> None:
    run_dir = build_valid_run(tmp_path, [(f"bg-{index:02d}", 1) for index in range(10)])
    output_dir = tmp_path / "missing-source-output"
    (run_dir / "images" / "frame_000010.png").unlink()

    with pytest.raises(ValueError, match="frame 000010"):
        split_run(run_dir, output_dir, SplitConfig(seed=42, train_ratio=0.7, val_ratio=0.2, test_ratio=0.1))

    assert not output_dir.exists()


def test_split_run_refuses_duplicate_manifest_frame_id_before_creating_final_output(tmp_path: Path) -> None:
    run_dir = build_valid_run(tmp_path, [(f"bg-{index:02d}", 1) for index in range(10)])
    manifest_path = run_dir / "manifest.jsonl"
    manifest_lines = manifest_path.read_text(encoding="utf-8").splitlines()
    duplicate_payload = json.loads(manifest_lines[0])
    duplicate_payload["background_id"] = "bg-09"
    manifest_lines.append(json.dumps(duplicate_payload))
    write_manifest(manifest_path, manifest_lines)
    output_dir = tmp_path / "duplicate-output"

    with pytest.raises(ValueError, match="INVALID_MANIFEST"):
        split_run(run_dir, output_dir, SplitConfig(seed=42, train_ratio=0.7, val_ratio=0.2, test_ratio=0.1))

    assert not output_dir.exists()


def test_split_cli_returns_nonzero_and_actionable_stderr_for_refusal(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    run_dir = build_valid_run(tmp_path, [(f"bg-{index:02d}", 1) for index in range(9)])
    output_dir = tmp_path / "cli-output"
    config_path = write_split_config(tmp_path / "dataset-split.yaml")

    exit_code = main([str(run_dir), str(output_dir), "--config", str(config_path)])

    captured = capsys.readouterr()
    assert exit_code == 1
    assert "at least 10 unique backgrounds" in captured.err
    assert not output_dir.exists()
