"""Shared immutable domain types for generation, training, and inference."""

from dataclasses import dataclass
from enum import IntEnum
from math import isfinite


class PokemonClass(IntEnum):
    """Stable class IDs shared by Unity, YOLO labels, and live inference."""

    PIKACHU = 0
    CHARMANDER = 1
    SQUIRTLE = 2


def _validate_class_id(class_id: int) -> None:
    if isinstance(class_id, bool) or not isinstance(class_id, int):
        raise TypeError(f"class_id must be an integer 0, 1, or 2; got {class_id!r}")
    try:
        PokemonClass(class_id)
    except (TypeError, ValueError) as error:
        raise ValueError(f"class_id must be one of 0, 1, or 2; got {class_id!r}") from error


def _validate_unit_interval(name: str, value: float) -> None:
    if not isfinite(value):
        raise ValueError(f"{name} must be finite")
    if not 0.0 <= value <= 1.0:
        raise ValueError(f"{name} must be between 0 and 1; got {value}")


@dataclass(frozen=True, slots=True)
class YoloBox:
    """A normalized YOLO object-detection annotation."""

    class_id: int
    center_x: float
    center_y: float
    width: float
    height: float

    def __post_init__(self) -> None:
        _validate_class_id(self.class_id)

        for name in ("center_x", "center_y", "width", "height"):
            _validate_unit_interval(name, getattr(self, name))

        if self.width <= 0.0:
            raise ValueError("width must be greater than 0")
        if self.height <= 0.0:
            raise ValueError("height must be greater than 0")

        left = self.center_x - self.width / 2.0
        right = self.center_x + self.width / 2.0
        top = self.center_y - self.height / 2.0
        bottom = self.center_y + self.height / 2.0
        if left < 0.0 or right > 1.0 or top < 0.0 or bottom > 1.0:
            raise ValueError("box edges must remain between 0 and 1")


@dataclass(frozen=True, slots=True)
class ManifestObject:
    """One labeled object recorded in a generated frame manifest."""

    class_id: int
    class_name: str
    box: YoloBox
    size_band: str | None = None
    model_input_width: float | None = None
    model_input_height: float | None = None

    def __post_init__(self) -> None:
        _validate_class_id(self.class_id)
        expected_name = PokemonClass(self.class_id).name.lower()
        if self.class_name != expected_name:
            raise ValueError(
                f"class_name must be {expected_name!r} for class_id {self.class_id}"
            )
        if self.box.class_id != self.class_id:
            raise ValueError("box class_id must match manifest object class_id")


@dataclass(frozen=True, slots=True)
class ManifestRecord:
    """Metadata for one generated image and its objects."""

    run_id: str
    frame_id: int
    seed: int
    split_hint: str
    background_id: str
    image_width: int
    image_height: int
    objects: tuple[ManifestObject, ...]
    capture_profile: str | None = None
    source_width: int | None = None
    source_height: int | None = None
    source_aspect_ratio: float | None = None
    letterbox_scale: float | None = None
    letterbox_pad_x: float | None = None
    letterbox_pad_y: float | None = None
    lighting_band: str | None = None

    def __post_init__(self) -> None:
        if not self.run_id.strip():
            raise ValueError("run_id must not be empty")
        if self.frame_id < 0:
            raise ValueError("frame_id must be non-negative")
        if not -(2**31) <= self.seed <= (2**31) - 1:
            raise ValueError("seed must fit in a signed 32-bit integer")
        if not self.split_hint.strip():
            raise ValueError("split_hint must not be empty")
        if not self.background_id.strip():
            raise ValueError("background_id must not be empty")
        if self.image_width <= 0:
            raise ValueError("image_width must be greater than 0")
        if self.image_height <= 0:
            raise ValueError("image_height must be greater than 0")
        if not isinstance(self.objects, tuple):
            raise TypeError("objects must be an immutable tuple")


__all__ = ["ManifestObject", "ManifestRecord", "PokemonClass", "YoloBox"]
