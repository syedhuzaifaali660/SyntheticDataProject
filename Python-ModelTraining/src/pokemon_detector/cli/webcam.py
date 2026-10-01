"""Run real-time Pokemon detection from a webcam."""

from __future__ import annotations

import argparse
import sys
from collections.abc import Callable, Sequence
from pathlib import Path

from pokemon_detector.inference.webcam import WebcamConfig, run_webcam


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    return parser


def main(
    argv: Sequence[str] | None = None,
    *,
    runner: Callable[[WebcamConfig], int] = run_webcam,
) -> int:
    args = build_parser().parse_args(argv)
    try:
        config = WebcamConfig.load(args.config)
    except (TypeError, ValueError) as error:
        print(error, file=sys.stderr)
        return 2
    return runner(config)


if __name__ == "__main__":
    raise SystemExit(main())
