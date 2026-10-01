"""Command-line entry point for provenance-preserving detector evaluation."""

from __future__ import annotations

import argparse
from pathlib import Path

from pokemon_detector.evaluation.evaluator import evaluate_dataset


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkpoint", required=True, type=Path)
    parser.add_argument("--dataset", required=True, type=Path)
    parser.add_argument("--split", required=True, choices=("val", "test"))
    parser.add_argument("--output-root", type=Path)
    args = parser.parse_args(argv)
    report = evaluate_dataset(
        args.checkpoint,
        args.dataset,
        args.split,
        output_root=args.output_root,
    )
    print(f"wrote {report.kind} {report.split} evaluation report")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
