import json
from pathlib import Path

import pytest
from PIL import Image

from pokemon_detector.cli.validate import main as validate_main
from pokemon_detector.dataset.overlay import (
    ProfileBalancedOverlaySelectionError,
    render_overlays,
    render_profile_balanced_overlays,
)


def write_png(path: Path, color: tuple[int, int, int]) -> None:
    image = Image.new("RGB", (32, 32), color)
    image.putpixel((0, 0), (0, 0, 0))
    image.save(path)


def create_overlay_run(tmp_path: Path) -> Path:
    run_dir = tmp_path / "run"
    images_dir = run_dir / "images"
    labels_dir = run_dir / "labels"
    images_dir.mkdir(parents=True)
    labels_dir.mkdir()
    (run_dir / "run-config.json").write_text('{"Seed":17,"Width":32,"Height":32}', encoding="utf-8")
    write_png(images_dir / "frame_000001.png", (255, 255, 255))
    write_png(images_dir / "frame_000002.png", (254, 254, 254))
    write_png(images_dir / "frame_000003.png", (253, 253, 253))
    (labels_dir / "frame_000001.txt").write_text("0 0.500000 0.500000 0.250000 0.250000\n", encoding="utf-8")
    (labels_dir / "frame_000002.txt").write_text("1 0.500000 0.500000 0.250000 0.250000\n", encoding="utf-8")
    (labels_dir / "frame_000003.txt").write_text("", encoding="utf-8")
    (run_dir / "manifest.jsonl").write_text(
        (
            '{"run_id":"run","frame_id":1,"seed":17,"split_hint":"train","background_id":"bg-1","width":32,"height":32,"objects":[{"class_id":0,"class_name":"pikachu","bbox":[0.5,0.5,0.25,0.25]}]}\n'
            '{"run_id":"run","frame_id":2,"seed":18,"split_hint":"train","background_id":"bg-2","width":32,"height":32,"objects":[{"class_id":1,"class_name":"charmander","bbox":[0.5,0.5,0.25,0.25]}]}\n'
            '{"run_id":"run","frame_id":3,"seed":19,"split_hint":"train","background_id":"bg-3","width":32,"height":32,"objects":[]}\n'
        ),
        encoding="utf-8",
    )
    return run_dir


def test_render_overlays_is_deterministic_and_draws_box_edges(tmp_path: Path) -> None:
    run_dir = create_overlay_run(tmp_path)
    output_dir = run_dir / "validation-overlays"

    first = render_overlays(run_dir, output_dir, limit=2, seed=7)
    second = render_overlays(run_dir, output_dir, limit=2, seed=7)

    assert [path.name for path in first] == [path.name for path in second]
    assert [path.name for path in first] == ["frame_000001.png", "frame_000002.png"]

    rendered = Image.open(first[0])
    assert rendered.getpixel((12, 12)) != (255, 255, 255)


def create_profile_balanced_overlay_run(tmp_path: Path) -> Path:
    run_dir = tmp_path / "mixed-device-run"
    images_dir = run_dir / "images"
    labels_dir = run_dir / "labels"
    images_dir.mkdir(parents=True)
    labels_dir.mkdir()

    profiles = ("Square", "IPhonePortrait", "IPhoneLandscape", "Webcam", "LaptopWindow")
    manifest_records: list[dict[str, object]] = []
    frame_id = 1
    for profile in profiles:
        for index in range(25):
            write_png(images_dir / f"frame_{frame_id:06d}.png", (255, 255, 255 - index))
            objects: list[dict[str, object]] = []
            if index != 0:
                size_band = "Small" if index == 1 else "Large" if index == 2 else "Medium"
                bbox = [0.5, 0.5, 0.2, 0.2]
                if index == 3:
                    bbox = [0.1, 0.5, 0.2, 0.2]
                objects.append(
                    {
                        "class_id": 0,
                        "class_name": "pikachu",
                        "bbox": bbox,
                        "size_band": size_band,
                    }
                )
                (labels_dir / f"frame_{frame_id:06d}.txt").write_text(
                    f"0 {bbox[0]:.6f} {bbox[1]:.6f} {bbox[2]:.6f} {bbox[3]:.6f}\n",
                    encoding="utf-8",
                )
            else:
                (labels_dir / f"frame_{frame_id:06d}.txt").write_text("", encoding="utf-8")
            manifest_records.append(
                {
                    "run_id": "mixed-device-6000-640-final-0907",
                    "frame_id": frame_id,
                    "seed": 42 + frame_id,
                    "split_hint": "train",
                    "background_id": f"bg-{frame_id}",
                    "width": 32,
                    "height": 32,
                    "capture_profile": profile,
                    "lighting_band": "Low" if index == 4 else "Normal",
                    "objects": objects,
                }
            )
            frame_id += 1
    (run_dir / "manifest.jsonl").write_text(
        "\n".join(json.dumps(record) for record in manifest_records) + "\n", encoding="utf-8"
    )
    return run_dir


def test_profile_balanced_overlays_reserve_required_coverage_and_each_profile_quota(
    tmp_path: Path,
) -> None:
    run_dir = create_profile_balanced_overlay_run(tmp_path)

    selection = render_profile_balanced_overlays(
        run_dir, run_dir / "validation-overlays", limit=100, seed=17
    )

    assert len(selection.paths) == 100
    assert selection.coverage["profile_counts"] == {
        "Square": 20,
        "IPhonePortrait": 20,
        "IPhoneLandscape": 20,
        "Webcam": 20,
        "LaptopWindow": 20,
    }
    assert selection.coverage["category_counts"].keys() == {
        "low_light",
        "small",
        "large",
        "near_edge",
        "negative",
    }
    assert all(count >= 1 for count in selection.coverage["category_counts"].values())
    assert selection.coverage["near_edge_definition"] == "a normalized box edge is within 0.05 of an image boundary"
    assert len(selection.coverage["selected_frame_ids"]) == 100


def test_profile_balanced_overlays_fail_clearly_when_a_required_category_is_missing(
    tmp_path: Path,
) -> None:
    run_dir = create_profile_balanced_overlay_run(tmp_path)
    manifest_path = run_dir / "manifest.jsonl"
    manifest_path.write_text(
        manifest_path.read_text(encoding="utf-8").replace('"lighting_band": "Low"', '"lighting_band": "Normal"'),
        encoding="utf-8",
    )

    with pytest.raises(ProfileBalancedOverlaySelectionError, match="low_light"):
        render_profile_balanced_overlays(run_dir, run_dir / "validation-overlays", limit=100, seed=17)


def test_validate_cli_writes_profile_balanced_coverage_evidence(tmp_path: Path) -> None:
    run_dir = create_profile_balanced_overlay_run(tmp_path)

    exit_code = validate_main(
        [str(run_dir), "--overlay-count", "100", "--seed", "17", "--profile-balanced"]
    )

    assert exit_code == 0
    report = json.loads((run_dir / "validation-report.json").read_text(encoding="utf-8"))
    assert report["overlay_coverage"]["mode"] == "profile_balanced"
    assert report["overlay_coverage"]["profile_counts"]["IPhonePortrait"] == 20
    assert report["overlay_coverage"]["category_counts"]["near_edge"] >= 1
    assert len(report["overlays"]) == 100
