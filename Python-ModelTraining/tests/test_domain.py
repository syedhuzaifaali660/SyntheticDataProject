import math

import pytest

from pokemon_detector.domain import ManifestObject, ManifestRecord, PokemonClass, YoloBox


def test_class_ids_are_stable() -> None:
    assert PokemonClass.PIKACHU.value == 0
    assert PokemonClass.CHARMANDER.value == 1
    assert PokemonClass.SQUIRTLE.value == 2


def test_yolo_box_rejects_out_of_range_coordinates() -> None:
    with pytest.raises(ValueError, match="between 0 and 1"):
        YoloBox(class_id=0, center_x=1.2, center_y=0.5, width=0.2, height=0.2)


@pytest.mark.parametrize("class_id", [-1, 3])
def test_yolo_box_rejects_unknown_class_ids(class_id: int) -> None:
    with pytest.raises(ValueError, match="class_id"):
        YoloBox(class_id=class_id, center_x=0.5, center_y=0.5, width=0.2, height=0.2)


@pytest.mark.parametrize("class_id", [0.0, True])
def test_yolo_box_rejects_non_integer_class_ids(class_id: object) -> None:
    with pytest.raises(TypeError, match="class_id"):
        YoloBox(class_id=class_id, center_x=0.5, center_y=0.5, width=0.2, height=0.2)


@pytest.mark.parametrize("width,height", [(0.0, 0.2), (0.2, 0.0)])
def test_yolo_box_requires_positive_area(width: float, height: float) -> None:
    with pytest.raises(ValueError, match="greater than 0"):
        YoloBox(class_id=0, center_x=0.5, center_y=0.5, width=width, height=height)


@pytest.mark.parametrize("coordinate", [math.nan, math.inf, -math.inf])
def test_yolo_box_rejects_non_finite_coordinates(coordinate: float) -> None:
    with pytest.raises(ValueError, match="finite"):
        YoloBox(class_id=0, center_x=coordinate, center_y=0.5, width=0.2, height=0.2)


def test_yolo_box_rejects_edges_outside_image() -> None:
    with pytest.raises(ValueError, match="edges"):
        YoloBox(class_id=0, center_x=0.95, center_y=0.5, width=0.2, height=0.2)


def test_manifest_record_is_immutable_and_validates_dimensions() -> None:
    box = YoloBox(class_id=0, center_x=0.5, center_y=0.5, width=0.2, height=0.2)
    obj = ManifestObject(class_id=0, class_name="pikachu", box=box)

    record = ManifestRecord(
        run_id="run-001",
        frame_id=0,
        seed=42,
        split_hint="train",
        background_id="room-001",
        image_width=640,
        image_height=640,
        objects=(obj,),
    )

    assert record.objects == (obj,)
    with pytest.raises(ValueError, match="image_width"):
        ManifestRecord(
            run_id="run-001",
            frame_id=0,
            seed=42,
            split_hint="train",
            background_id="room-001",
            image_width=0,
            image_height=640,
            objects=(obj,),
        )


def test_manifest_record_accepts_signed_32_bit_seeds() -> None:
    box = YoloBox(class_id=0, center_x=0.5, center_y=0.5, width=0.2, height=0.2)
    obj = ManifestObject(class_id=0, class_name="pikachu", box=box)

    record = ManifestRecord(
        run_id="run-001",
        frame_id=0,
        seed=-2_147_483_648,
        split_hint="train",
        background_id="room-001",
        image_width=640,
        image_height=640,
        objects=(obj,),
    )

    assert record.seed == -2_147_483_648


@pytest.mark.parametrize("seed", [-2_147_483_649, 2_147_483_648])
def test_manifest_record_rejects_out_of_range_signed_32_bit_seeds(seed: int) -> None:
    box = YoloBox(class_id=0, center_x=0.5, center_y=0.5, width=0.2, height=0.2)
    obj = ManifestObject(class_id=0, class_name="pikachu", box=box)

    with pytest.raises(ValueError, match="signed 32-bit"):
        ManifestRecord(
            run_id="run-001",
            frame_id=0,
            seed=seed,
            split_hint="train",
            background_id="room-001",
            image_width=640,
            image_height=640,
            objects=(obj,),
        )
