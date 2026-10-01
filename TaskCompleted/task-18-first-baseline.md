# Task 18: Generate and Train the First Baseline

- Status: Completed
- Completed: 2026-09-02
- Checkpoint commit: Not created; no commit was requested.

## Outcome

- Generated the immutable `baseline-3900` run with seed 42, 3,900 images and
  labels, and all 98 supplied backgrounds.
- Generalized the manual review gate to accept exactly 100 overlays from a
  larger validated dataset while preserving the Task 16 contract.
- Added a baseline-specific grouped split configuration targeting the approved
  3,000/600/300 allocation. The realized split is 3,019/569/312 images across
  75/15/8 mutually disjoint background groups.
- Archived the grouped split report with training artifacts.
- Trained YOLO26n for 100 epochs on Apple MPS and evaluated `best.pt` on the
  untouched synthetic test split.
- Fixed failure-gallery generation for model boxes that extend slightly beyond
  normalized image edges, then generated the required 50-image gallery.

## Verification

- Dataset validation: 3,900 images, 3,900 labels, 1,958 objects per class, 977
  negative images, zero errors, and zero warnings.
- Manual review: 100 overlays reviewed; zero wrong classes, loose boxes, missing
  objects, invisible objects, or texture failures; six intentional edge crops
  and one implausibly heavy overlap were recorded.
- Grouped split: 3,019 train, 569 validation, and 312 test images; all 98
  backgrounds appear in exactly one partition.
- Training: 100 epochs completed; epoch 92 reached validation mAP50 0.9948 and
  mAP50-95 0.9853. Best and last checkpoints, resolved configuration,
  environment report, metrics, plots, and archived split report exist.
- Synthetic test: precision 0.9950, recall 0.9846, mAP50 0.9915, and mAP50-95
  0.9836. All three classes have nonzero true positives and complete per-class
  metrics.
- Per-class test mAP50-95: Pikachu 0.9807, Charmander 0.9873, Squirtle 0.9827.
- Confusion matrix: no inter-class confusion. The evaluation report records
  Pikachu 158 TP/3 FP/1 FN, Charmander 161 TP/6 FP/1 FN, and Squirtle 155 TP/4
  FP/2 FN.
- Default-threshold failure analysis: 472 matched objects, six misses, seven
  duplicate/localization errors, zero class confusions, zero genuine background
  false positives, and zero edge-cropped misses. The smallest missed object
  occupied 1.5% of the image.
- Failure gallery: 50 ranked overlays plus `failures.json`, confusion matrix,
  normalized confusion matrix, and synthetic report exist beneath the baseline
  run.
- `uv run pytest -q`: 136 passed.
- `uv run ruff check .`: all checks passed.
- Editor diagnostics: no errors in touched Python files.

PyTorch warned that one MPS accumulation operation lacks a deterministic
implementation. The seed, inputs, split, configuration, and artifacts are
recorded, but exact bitwise reproduction on MPS is not guaranteed.

## Baseline Decision

- Missed detections are rare and concentrate in crowded or poorly localized
  scenes, with one clearly small Squirtle case.
- There is no measured inter-class confusion and no genuine background
  hallucination at the standard confidence threshold.
- Cropping, rotation, and individual background identity are not supported as
  dominant limitations by the current failures.
- Do not expand the synthetic dataset until real-world validation identifies a
  concrete domain-gap limitation.

## Deferred Gates

None.

## Next Task

Task 19 builds the real-world validation set and runs controlled trials.
