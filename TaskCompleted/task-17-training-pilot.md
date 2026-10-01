# Task 17: 300-Image Training Pilot

- Status: Completed
- Completed: 2026-09-02
- Implementation commit: `6fba8a1`
- Documentation commit: `581c252`

## Outcome

- Preserved the failed `training-pilot-300` run after `-nographics` produced 300
  uniform gray frames despite structurally valid labels and manifests.
- Added a validator invariant that rejects uniform captures, then regenerated the
  accepted `training-pilot-300-v2` run with graphics enabled.
- Validated 300 images and labels from 91 backgrounds, with 491 objects and 61
  negative frames.
- Created a deterministic 207/57/36 train/validation/test split across 63/18/10
  mutually disjoint background groups.
- Added an augmentation-free overfit configuration with nominal batch size one so
  each tiny-set sample produces an optimizer update.
- Overfit a balanced 20-image singleton fixture with every box inside a 5% image
  margin. The selected checkpoint recalled all 20 training objects at same-class
  IoU 0.5 and confidence 0.001.
- Completed five-epoch smoke training and held-out validation and test evaluation.
- Fixed the evaluation module entry point, which previously returned success from
  the documented `python -m` command without invoking the CLI.

## Verification

- Graphics-enabled Unity batch capture: 300 PNGs, 300 labels, 300 manifest rows,
  91 distinct backgrounds, and zero uniform images.
- Validation: passed with 163 Pikachu, 164 Charmander, 164 Squirtle, 61 negative
  frames, no errors, no warnings, and 50 overlays.
- Grouped split: 207 train, 57 validation, and 36 test images with no background
  leakage.
- Overfit losses: box 0.54673 to 0.01831, class 9.84154 to 0.01137, and L1
  0.00735 to 0.00025; explicit same-class IoU-0.5 recall was 20/20.
- Smoke training: best and last checkpoints, resolved configuration, environment
  report, and five metrics rows exist under `runs/training-pilot-300/`.
- Synthetic validation: precision 0.6609, recall 0.6648, mAP50 0.7742, and
  mAP50-95 0.6759.
- Synthetic test: precision 0.6170, recall 0.6674, mAP50 0.7828, and mAP50-95
  0.7008.
- All three classes had nonzero predictions and true positives in both held-out
  splits.
- `uv run pytest -q`: 134 passed.
- `uv run ruff check .`: all checks passed.
- `git diff --check`: passed.

## Deferred gates

None.

## Next task

Task 18 generates approximately 3,900 images and trains the first baseline.
