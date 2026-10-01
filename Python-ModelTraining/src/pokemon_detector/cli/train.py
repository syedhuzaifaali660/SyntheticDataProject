"""Train the Pokemon detector from a validated prepared dataset."""

from __future__ import annotations

import argparse
import sys
from collections.abc import Sequence
from pathlib import Path
from typing import Any

from pokemon_detector.training.config import TrainingConfig
from pokemon_detector.training.runner import DatasetNotValidatedError, train


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--run-name", required=True)
    return parser


def main(argv: Sequence[str] | None = None, *, yolo_factory: Any = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        config = TrainingConfig.load(
            args.config,
            dataset_override=args.dataset,
            run_name_override=args.run_name,
        )
        train(config, yolo_factory=yolo_factory) if yolo_factory is not None else train(config)
    except (TypeError, ValueError, DatasetNotValidatedError) as error:
        print(error, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
