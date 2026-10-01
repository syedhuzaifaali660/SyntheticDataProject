from pathlib import Path

import pytest

from pokemon_detector.dataset.parser import LabelParseError, parse_yolo_label


def write_label(path: Path, contents: str) -> Path:
    path.write_text(contents, encoding="utf-8")
    return path


def test_parse_yolo_label_returns_empty_list_for_negative_sample(tmp_path: Path) -> None:
    label_path = write_label(tmp_path / "frame_000001.txt", "")

    assert parse_yolo_label(label_path) == []


def test_parse_yolo_label_preserves_six_decimal_values(tmp_path: Path) -> None:
    label_path = write_label(
        tmp_path / "frame_000001.txt",
        "2 0.248257 0.559066 0.112042 0.126477\n",
    )

    boxes = parse_yolo_label(label_path)

    assert len(boxes) == 1
    assert boxes[0].class_id == 2
    assert boxes[0].center_x == 0.248257
    assert boxes[0].center_y == 0.559066
    assert boxes[0].width == 0.112042
    assert boxes[0].height == 0.126477


def test_parse_yolo_label_rejects_unknown_class_id(tmp_path: Path) -> None:
    label_path = write_label(tmp_path / "frame_000001.txt", "3 0.5 0.5 0.3 0.4\n")

    with pytest.raises(LabelParseError, match="INVALID_CLASS_ID"):
        parse_yolo_label(label_path)


def test_parse_yolo_label_rejects_missing_columns(tmp_path: Path) -> None:
    label_path = write_label(tmp_path / "frame_000001.txt", "0 0.1 0.2 0.3\n")

    with pytest.raises(LabelParseError, match="expected 5 columns"):
        parse_yolo_label(label_path)


def test_parse_yolo_label_rejects_non_numeric_values(tmp_path: Path) -> None:
    label_path = write_label(tmp_path / "frame_000001.txt", "0 nope 0.2 0.3 0.4\n")

    with pytest.raises(LabelParseError, match="non-numeric"):
        parse_yolo_label(label_path)


def test_parse_yolo_label_rejects_zero_area_boxes(tmp_path: Path) -> None:
    label_path = write_label(tmp_path / "frame_000001.txt", "0 0.5 0.5 0.0 0.4\n")

    with pytest.raises(LabelParseError, match="INVALID_BOX"):
        parse_yolo_label(label_path)


def test_parse_yolo_label_rejects_boxes_outside_image(tmp_path: Path) -> None:
    label_path = write_label(tmp_path / "frame_000001.txt", "0 0.95 0.5 0.2 0.2\n")

    with pytest.raises(LabelParseError, match="INVALID_BOX"):
        parse_yolo_label(label_path)


def test_parse_yolo_label_allows_six_decimal_half_ulp_edge_rounding(tmp_path: Path) -> None:
    label_path = write_label(
        tmp_path / "frame_000001.txt",
        "0 0.768712 0.577458 0.462577 0.600042\n",
    )

    boxes = parse_yolo_label(label_path)

    assert len(boxes) == 1
    assert boxes[0].class_id == 0
    assert boxes[0].width == 0.462577


def test_parse_yolo_label_rejects_non_six_decimal_out_of_bounds_rounding_case(tmp_path: Path) -> None:
    label_path = write_label(
        tmp_path / "frame_000001.txt",
        "0 0.1000000 0.500000 0.2000010 0.200000\n",
    )

    with pytest.raises(LabelParseError, match="INVALID_BOX"):
        parse_yolo_label(label_path)
