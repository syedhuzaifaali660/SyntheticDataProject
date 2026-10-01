# Task 27: Mixed-device training configuration and smoke gate

## Completed

- Added dedicated one-epoch smoke and 100-epoch full training configurations for the
  accepted occlusion-safe 6,000-frame prepared dataset.
- Kept the approved YOLO26n, 640-pixel, physical-batch-64, MPS, seed-42 contract.
- Documented the corrected smoke and full training commands.
- Ran the smoke job on native Apple MPS without deleting or replacing older datasets,
  experiments, or trained models.

## Artifacts

- Smoke run: `Python-ModelTraining/experiments/mixed-device-6000-640-occlusion-safe-0907-smoke`
- Best smoke checkpoint: `Python-ModelTraining/experiments/mixed-device-6000-640-occlusion-safe-0907-smoke/weights/best.pt`
- Preserved invalid restricted-environment attempt: `Python-ModelTraining/experiments/mixed-device-6000-640-occlusion-safe-0907-smoke-incomplete-cpu-fallback`
- Full config: `Python-ModelTraining/configs/training/train-mixed-device-6000-640.yaml`
- Smoke config: `Python-ModelTraining/configs/training/train-mixed-device-smoke-640.yaml`

## Verification

- Native PyTorch hardware check: MPS built and available.
- Smoke result: one epoch completed in 0.061 hours with batch 64 at 640 pixels.
- Peak reported GPU/unified memory: approximately 18.6 GB.
- Smoke artifacts include `best.pt`, `last.pt`, `resolved-config.yaml`, `environment.json`,
  `split-report.json`, `results.csv`, and diagnostic plots.
- Final smoke validation: mAP50 0.688 and mAP50-95 0.586. These are smoke-only metrics,
  not the final model evaluation.
- Full Python suite: 158 passed.
- Ruff: passed.
- Diff whitespace check: passed.
- Checkpoint commit: `a770891` (`feat: add occlusion-safe training smoke gate`).

## Next step

Run the full 100-epoch MPS training configuration. Preserve its resolved inputs and
reports, then compare the resulting checkpoint with the existing baseline before export.
