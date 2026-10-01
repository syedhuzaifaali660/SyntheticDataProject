# Mixed-device full training and synthetic evaluation

## Completed

- Completed all 100 training epochs on native Apple MPS using YOLO26n, 640-pixel input,
  physical batch 64, seed 42, and the accepted occlusion-safe split.
- Preserved every older model, dataset, and experiment.
- Evaluated the new checkpoint and `baseline-3900` on the identical untouched 300-image
  mixed-device test split.
- Produced separate confusion matrices, failure galleries, per-class reports, and a
  five-profile comparison.
- Updated evaluation provenance and controlled-trial defaults to the canonical
  `datasets/prepared` and `datasets/real` structure.

## Artifacts

- Full run: `Python-ModelTraining/experiments/mixed-device-6000-640-occlusion-safe-0907`
- New checkpoint: `Python-ModelTraining/experiments/mixed-device-6000-640-occlusion-safe-0907/weights/best.pt`
- New-model report: `Python-ModelTraining/experiments/mixed-device-6000-640-occlusion-safe-0907/evaluation/new-model-on-occlusion-safe/synthetic/test/synthetic-report.json`
- Baseline report: `Python-ModelTraining/experiments/mixed-device-6000-640-occlusion-safe-0907/evaluation/baseline-on-occlusion-safe/synthetic/test/synthetic-report.json`
- Device-profile comparison: `Python-ModelTraining/experiments/mixed-device-6000-640-occlusion-safe-0907/evaluation/profile-comparison.json`

## Verification

- Training process exit code: 0.
- Training duration: 5.638 hours.
- Completed epochs: 100/100.
- Best-checkpoint validation: precision 0.996, recall 0.988, mAP50 0.995,
  mAP50-95 0.978.
- New model on the held-out test split: precision 0.9897, recall 0.9905,
  mAP50 0.9947, mAP50-95 0.9756, 3 false negatives.
- Baseline on the same split: precision 0.9917, recall 0.9493, mAP50 0.9773,
  mAP50-95 0.9209, 20 false negatives.
- New-minus-baseline mAP50-95: Pikachu +0.0477, Charmander +0.0819,
  Squirtle +0.0347.
- New-minus-baseline profile mAP50-95: iPhone landscape +0.0800,
  iPhone portrait +0.0464, laptop window +0.0354, square +0.0459,
  webcam +0.0628.
- Evaluation-path TDD RED: three expected failures for the new prepared, real, and
  controlled-trial path contracts.
- Focused evaluation/trial tests: 31 passed.
- Full Python suite: 161 passed.
- Ruff and diff whitespace checks: passed.
- Evaluation path checkpoint: `5abe2e3` (`fix: align evaluation with canonical dataset paths`).

## Remaining promotion gate

`Python-ModelTraining/datasets/real` currently contains zero images. Real iPhone metrics,
Core ML export, Xcode installation, and device FPS are therefore not claimed. The old
Xcode model remains untouched until labeled real captures pass the promotion gate.
