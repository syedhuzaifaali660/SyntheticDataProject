"""OpenCV webcam frame source."""

from __future__ import annotations

from collections.abc import Callable
from contextlib import suppress
from typing import Any

import cv2
import numpy as np


class CameraUnavailableError(RuntimeError):
    """Raised when OpenCV cannot open the requested camera."""


class OpenCVFrameSource:
    """Own an OpenCV VideoCapture and expose frames until end-of-stream."""

    def __init__(
        self,
        camera_index: int,
        requested_size: int = 640,
        *,
        capture_factory: Callable[[int], Any] = cv2.VideoCapture,
    ) -> None:
        if isinstance(camera_index, bool) or not isinstance(camera_index, int):
            raise TypeError("camera_index must be an integer")
        if camera_index < 0:
            raise ValueError("camera_index must be non-negative")
        if requested_size <= 0:
            raise ValueError("requested_size must be positive")
        self._capture = capture_factory(camera_index)
        self._closed = False
        try:
            if not self._capture.isOpened():
                self._capture.release()
                self._closed = True
                raise CameraUnavailableError(f"unable to open camera index {camera_index}")
            self._capture.set(cv2.CAP_PROP_FRAME_WIDTH, float(requested_size))
            self._capture.set(cv2.CAP_PROP_FRAME_HEIGHT, float(requested_size))
        except CameraUnavailableError:
            raise
        except Exception:
            with suppress(Exception):
                self._capture.release()
            self._closed = True
            raise

    def read(self) -> np.ndarray | None:
        success, frame = self._capture.read()
        if not success or frame is None:
            return None
        return frame

    def close(self) -> None:
        if not self._closed:
            self._capture.release()
            self._closed = True
