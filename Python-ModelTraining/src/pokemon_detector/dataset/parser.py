"""Strict YOLO label parsing for generated Pokemon datasets."""

from __future__ import annotations

import math
from dataclasses import dataclass
from decimal import Decimal, InvalidOperation
from pathlib import Path

from pokemon_detector.domain import YoloBox

HALF_ULP_SIX_DECIMALS = Decimal("0.0000005")


@dataclass(frozen=True, slots=True)
class LabelParseError(ValueError):
    """A line-level YOLO label parsing failure."""

    code: str
    path: Path
    line_number: int
    detail: str

    def __str__(self) -> str:
        return f"{self.code}: {self.path.name}: line {self.line_number}: {self.detail}"


def _decimal_places(token: str) -> int:
    _, dot, fraction = token.partition(".")
    if not dot:
        return 0
    return len(fraction)


def _validate_edge_rounding(tokens: tuple[str, str, str, str]) -> tuple[float, float]:
    center_x_token, center_y_token, width_token, height_token = tokens
    try:
        center_x = Decimal(center_x_token)
        center_y = Decimal(center_y_token)
        width = Decimal(width_token)
        height = Decimal(height_token)
    except InvalidOperation as error:
        raise ValueError("non-numeric value in label row") from error

    decimal_places = [_decimal_places(token) for token in tokens]
    left = center_x - (width / Decimal(2))
    right = center_x + (width / Decimal(2))
    top = center_y - (height / Decimal(2))
    bottom = center_y + (height / Decimal(2))

    tolerated = {
        "left": Decimal(0) - HALF_ULP_SIX_DECIMALS <= left < Decimal(0),
        "right": Decimal(1) < right <= Decimal(1) + HALF_ULP_SIX_DECIMALS,
        "top": Decimal(0) - HALF_ULP_SIX_DECIMALS <= top < Decimal(0),
        "bottom": Decimal(1) < bottom <= Decimal(1) + HALF_ULP_SIX_DECIMALS,
    }

    if any(tolerated.values()) and any(places != 6 for places in decimal_places):
        raise ValueError("box edges must remain between 0 and 1")

    if left < 0 and not tolerated["left"]:
        raise ValueError("box edges must remain between 0 and 1")
    if right > 1 and not tolerated["right"]:
        raise ValueError("box edges must remain between 0 and 1")
    if top < 0 and not tolerated["top"]:
        raise ValueError("box edges must remain between 0 and 1")
    if bottom > 1 and not tolerated["bottom"]:
        raise ValueError("box edges must remain between 0 and 1")

    normalized_center_x = float(center_x)
    normalized_center_y = float(center_y)
    if tolerated["left"]:
        normalized_center_x = math.nextafter(float(width / Decimal(2)), 1.0)
    elif tolerated["right"]:
        normalized_center_x = math.nextafter(float(Decimal(1) - (width / Decimal(2))), 0.0)
    if tolerated["top"]:
        normalized_center_y = math.nextafter(float(height / Decimal(2)), 1.0)
    elif tolerated["bottom"]:
        normalized_center_y = math.nextafter(float(Decimal(1) - (height / Decimal(2))), 0.0)

    return normalized_center_x, normalized_center_y


def parse_yolo_label(path: Path) -> list[YoloBox]:
    """Parse a YOLO label file into immutable box records."""

    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except UnicodeDecodeError as error:
        raise LabelParseError("INVALID_LABEL_ENCODING", path, 0, "label file is not valid UTF-8") from error

    boxes: list[YoloBox] = []
    for line_number, raw_line in enumerate(lines, start=1):
        line = raw_line.strip()
        if not line:
            continue
        parts = line.split()
        if len(parts) != 5:
            raise LabelParseError(
                "INVALID_BOX",
                path,
                line_number,
                f"expected 5 columns, found {len(parts)}",
            )
        try:
            class_id = int(parts[0])
            normalized_center_x, normalized_center_y = _validate_edge_rounding(
                (parts[1], parts[2], parts[3], parts[4])
            )
            width = float(parts[3])
            height = float(parts[4])
        except ValueError as error:
            raise LabelParseError("INVALID_BOX", path, line_number, str(error)) from error

        try:
            boxes.append(
                YoloBox(
                    class_id=class_id,
                    center_x=normalized_center_x,
                    center_y=normalized_center_y,
                    width=width,
                    height=height,
                )
            )
        except ValueError as error:
            code = "INVALID_CLASS_ID" if "class_id" in str(error) else "INVALID_BOX"
            raise LabelParseError(code, path, line_number, str(error)) from error
        except TypeError as error:
            raise LabelParseError("INVALID_CLASS_ID", path, line_number, str(error)) from error
    return boxes
