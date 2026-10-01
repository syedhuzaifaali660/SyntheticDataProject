# Task 16: 100-Image Visual-Review Dataset

- Status: Completed
- Completed: 2026-09-02
- Implementation commit: `6fba8a1`
- Documentation commit: `581c252`

## Outcome

- Added an unattended Unity capture command that accepts a run ID, frame count,
  seed, and output root, reuses the production capture scene, refuses to overwrite
  an existing run, and verifies exact image, label, and manifest counts.
- Added an optional validation `--seed` override so the documented Task 16 command
  deterministically renders all overlays.
- Added a visual-review CLI that records every required defect count and only
  accepts a passing run with exactly 100 images, labels, and unique overlay files,
  zero wrong classes, and zero boxes missing their object.
- Preserved failed runs `visual-review-100` and `visual-review-100-v2`. The first
  exposed a six-decimal YOLO boundary-rounding defect; the second exposed wrapped
  background UVs during complete overlay review.
- Fixed boundary normalization and background crop/blur sampling, then generated
  and accepted `visual-review-100-v3` with seed 42.
- Validation reported 100 images, 100 labels, 100 overlays, 29 negative frames,
  51 objects per class, no errors, and no warnings.
- Reviewed all 100 overlays. Wrong class, loose box, clipped box, missing object,
  invisible object, texture failure, and implausible scene counts were all zero.

## Verification

- Unity `CaptureRunRequestValidationTests`: 20 passed.
- Unity `YoloBoxTests`: 4 passed.
- Unity complete edit-mode suite: 77 passed.
- One-frame batch capture probe: one PNG, one label, one manifest record, and one
  run configuration were written; Unity exited successfully.
- `uv run python -m pokemon_detector.cli.validate data/generated/visual-review-100-v3 --overlay-count 100 --seed 42`: passed with 100 overlays.
- `uv run python -m pokemon_detector.cli.visual_review data/generated/visual-review-100-v3 --wrong-class 0 --loose-box 0 --clipped-box 0 --missing-object 0 --invisible-object 0 --texture-failure 0 --implausible-scene 0`: accepted.
- `uv run pytest -q`: 129 passed.
- `uv run ruff check .`: all checks passed.
- Manual review: all 100 overlays reviewed; all seven defect counts were zero.

## Next task

Task 17 proves the 300-image training pilot, including validation, background-grouped
splitting, tiny-set overfit, and short smoke training.
