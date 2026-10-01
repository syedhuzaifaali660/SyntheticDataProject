# Task 12: Background-Grouped Dataset Splits

- Status: Completed
- Completed: 2026-09-01
- Implementation commit: `f051728`
- Review-hardening commit: `22116dd`

## Outcome

Delivered deterministic, leakage-safe Ultralytics dataset partitioning:

- Loads and validates the seed and train/validation/test ratios from `configs/dataset-split.yaml`.
- Revalidates every Unity source run immediately before splitting.
- Sorts unique background IDs, shuffles them with a local seeded random generator, and assigns whole background groups to one partition only.
- Copies complete image/label pairs into `images/{train,val,test}` and `labels/{train,val,test}` through a staging directory.
- Generates an absolute-path `dataset.yaml` using the immutable Pikachu, Charmander, and Squirtle taxonomy.
- Generates a deterministic `split-report.json` containing background assignments and frame counts.
- Refuses invalid datasets, fewer than ten backgrounds, missing files, malformed configuration, non-empty output destinations, or detected leakage.
- Adds `python -m pokemon_detector.cli.split RUN_DIR OUTPUT_DIR --config CONFIG_PATH` with controlled failure output.

## Review hardening

- The independent review reproduced a leakage path where duplicate manifest frame IDs could be assigned to different background groups.
- Validation now rejects the second occurrence of a frame ID as `INVALID_MANIFEST` with its line and frame number.
- The splitter independently enforces the same uniqueness invariant before copying.
- Happy-path tests now prove copied filenames are globally unique and agree with the split report counts.
- Scoped re-review confirmed both blocking findings were addressed with no new Critical or Important breakage.

## Smoke-run result

The real `data/generated/smoke-20` run contains 16 unique backgrounds. With seed 42 it deterministically produced:

- Train: 11 backgrounds, 14 image/label pairs.
- Validation: 3 backgrounds, 4 image/label pairs.
- Test: 2 backgrounds, 2 image/label pairs.
- Total: 16 unique backgrounds and 20 complete pairs.
- Train/validation, train/test, and validation/test background intersections: empty.

Two independent output directories produced identical split reports. Each generated `dataset.yaml` used its own resolved absolute output path and the exact class mapping `0: pikachu`, `1: charmander`, `2: squirtle`.

## Verification

- `uv lock --check`: current; 71 packages resolved.
- `uv run pytest -q`: 53 passed.
- `uv run ruff check .`: all checks passed.
- Two fresh splitter CLI runs: exit 0.
- Exact background counts: 11/3/2.
- Exact frame-pair counts: 14/4/2.
- Pairwise background intersections: none.

## Next task

Task 13 adds the gated YOLO26n smoke and baseline training configurations plus the local MPS training runner.
