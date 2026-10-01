# Python dataset and experiment cleanup

## Completed

- Retained the full `baseline-3900` generated dataset, prepared split, experiment,
  checkpoint, and evaluation artifacts.
- Retained the full accepted `mixed-device-6000-640-occlusion-safe-0907` generated
  dataset, prepared split, experiment, checkpoint, and evaluation artifacts.
- Retained `datasets/real` because it is required for pending iPhone validation.
- Permanently removed all other generated datasets, prepared datasets, smoke runs,
  pilot experiments, and the obsolete `data` and `runs` compatibility paths from
  `Python-ModelTraining`.
- Did not modify Unity, Xcode, Python source, configurations, or exports.

## Verification

- Retained generated image counts: baseline 3,900; current 6,000.
- Retained prepared image counts: baseline 3,900; current 6,000.
- Verified `experiments/baseline-3900/weights/best.pt` exists.
- Verified `experiments/mixed-device-6000-640-occlusion-safe-0907/weights/best.pt` exists.
- Exactly two generated dataset directories remain.
- Exactly two prepared dataset directories remain.
- Exactly two experiment directories remain.
- Legacy `data` and `runs` paths are absent.
- Approximate storage reclaimed: 12 GB.

## Recovery

The removed generated data and experiment directories were permanently deleted rather
than moved to Trash. They can only be recovered from an external backup or regenerated.
