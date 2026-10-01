from __future__ import annotations

import pytest

from pokemon_detector.inference.fps import RollingFPS


class FakeClock:
    def __init__(self, timestamps: list[float]) -> None:
        self._timestamps = iter(timestamps)

    def __call__(self) -> float:
        return next(self._timestamps)


def test_rolling_fps_uses_elapsed_intervals_and_first_frame_is_zero() -> None:
    counter = RollingFPS(window_size=4, clock=FakeClock([0.0, 0.05, 0.10, 0.15]))

    values = [counter.update() for _ in range(4)]

    assert values[0] == 0.0
    assert values[-1] == pytest.approx(20.0)


def test_rolling_fps_rejects_a_window_smaller_than_two_samples() -> None:
    with pytest.raises(ValueError, match="at least 2"):
        RollingFPS(window_size=1)
