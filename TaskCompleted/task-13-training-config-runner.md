# Task 13: Gated YOLO26n Training Runner

- Status: Completed
- Completed: 2026-09-01
- Implementation commit: `0779af2`
- Review-hardening commit: `f7ae9da`

## Outcome

Delivered the testable local training boundary for the three-Pokémon detector:

- Added reusable five-epoch smoke and 100-epoch baseline configurations for `yolo26n.pt` on Apple MPS.
- Added strict resolved configuration loading with dataset/run-name overrides, safe run names, CPU/MPS device validation, and reproducible absolute paths.
- Added `python -m pokemon_detector.cli.train --dataset DATASET --config CONFIG --run-name NAME` for the later 300-image pilot and baseline runs.
- Added a validation gate that rejects missing, malformed, contradictory, or failed `validation-report.json` evidence before YOLO is constructed.
- Added the exact planned Ultralytics training arguments, including deterministic mode, fixed seed, requested project directory, and run name.
- Resolves the actual Ultralytics output from `model.trainer.save_dir`, requires `weights/best.pt`, and returns that checkpoint path.
- Writes `resolved-config.yaml` and `environment.json` beside the checkpoint, including Python, platform, PyTorch, Ultralytics, and MPS availability.
- Preserves the fresh dataset validation result beside every processed `dataset.yaml`, making a split self-contained for the training gate.

## Test-first and review hardening

- Initial RED failed during collection because the training package and CLI did not exist.
- Fake-YOLO tests prove exact argument forwarding, checkpoint discovery, resolved artifact output, and CLI operation without downloading weights or starting real training.
- Invalid-report tests prove YOLO construction never occurs for unvalidated datasets.
- Independent review found that the first splitter implementation copied possibly stale on-disk evidence even though it gated on a fresh in-memory result.
- A focused regression planted a stale failed source report and failed against the implementation commit.
- The splitter now serializes the same fresh `ValidationReport` that passed its gate; scoped re-review found no remaining Critical or Important issues.

## Verification

- `uv run pytest tests/training -v`: 19 passed.
- Task 13 covering tests: 33 passed.
- `uv run pytest -q`: 73 passed.
- `uv run ruff check .`: all checks passed.
- `uv lock --check`: current; 71 packages resolved.
- Real `smoke-20` split: passing fresh validation report, 0 validation errors, 14/4/2 images, and 11/3/2 isolated background groups.
- No real YOLO training, model-weight download, or network-backed training operation was run in Task 13.

## Next task

Task 14 adds real-image annotation, synthetic/real evaluation reports, per-class metrics, and the bounded failure gallery.
