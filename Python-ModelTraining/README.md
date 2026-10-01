# Pokemon Detector: Python Operations

This application validates Unity captures, creates background-grouped dataset
splits, trains and evaluates YOLO26n, and runs the accepted detector against the
webcam. Run all commands in this guide from `Python-ModelTraining`.

## Environment Setup

The project requires Python 3.12 and uses `uv` for its environment and lockfile.

```bash
cd Python-ModelTraining
uv sync
uv run python --version
```

The reported Python version must be at least 3.12 and lower than 3.13.

## Accepted Synthetic Baseline

The accepted baseline uses these local artifacts:

- Generated run: `data/generated/baseline-3900`
- Prepared split: `data/prepared/baseline-3900`
- Dataset YAML: `data/prepared/baseline-3900/dataset.yaml`
- Split report: `data/prepared/baseline-3900/split-report.json`
- Training configuration: `configs/training/train-baseline.yaml`
- Best checkpoint: `runs/baseline-3900/weights/best.pt`
- Synthetic test report:
  `runs/baseline-3900/evaluation/test/report.json`

The generated datasets, prepared datasets, run outputs, checkpoints, and local
environment are intentionally ignored by Git. Preserve an accepted local run if
you need to compare regenerated reports with it.

These paths describe artifacts from the original development workspace. They
are not included in a fresh clone, so commands below that use `baseline-3900`
or `mixed-device-6000-640-occlusion-safe-0907` require you to recreate those
runs and splits first, or obtain them from a separate trusted artifact backup.
To create new data, first supply the Unity backgrounds and Pokémon models listed
in the Unity guide, then generate a run and validate it before splitting or
training. The commands that assume the accepted local artifacts are examples of
that recorded workflow, not a downloadable dataset bundled with this repo.

## Validate a Unity Run

For mixed-device capture with a live counter and a retained Unity log, run:

```bash
bash scripts/capture_mixed_device.sh mixed-device-6000-640-NEW 6000
```

Use a unique ID. The script refuses existing directories and reports Unity's
exit status; 100 passing smoke frames do not prove a complete production run.
Validate the full run with explicit count and distribution gates:

```bash
uv run python -m pokemon_detector.cli.validate \
  data/generated/mixed-device-6000-640-NEW --expected-frame-count 6000 --overlay-count 100 \
  --profile-balanced
```

Do not train until all gates pass, overlays are reviewed, and the exact
background-isolated split has been verified.

Validate the accepted generated run and regenerate its deterministic sample of
100 overlays:

```bash
uv run python -m pokemon_detector.cli.validate \
  data/generated/baseline-3900 \
  --overlay-count 100 \
  --seed 42
```

The command writes `validation-report.json` and, when validation passes,
`validation-overlays/` beneath the run directory. Do not train from a run whose
report contains errors. Review every selected overlay before accepting a new
dataset.

`--profile-balanced` is opt-in for generated mixed-device runs. With
`--overlay-count 100`, it deterministically selects exactly 20 frames from each
of Square, IPhonePortrait, IPhoneLandscape, Webcam, and LaptopWindow. Its report
also proves representation of low-light, Small, Large, near-edge, and negative
frames. A box is near-edge when any normalized YOLO edge is within `0.05` of an
image boundary.

Record the accepted 100-overlay review after inspecting the images:

```bash
uv run python -m pokemon_detector.cli.visual_review \
  data/generated/baseline-3900 \
  --wrong-class 0 \
  --loose-box 0 \
  --clipped-box 6 \
  --missing-object 0 \
  --invisible-object 0 \
  --texture-failure 0 \
  --implausible-scene 1
```

Use the counts actually observed for a new run. The values above reproduce the
recorded baseline review, including six intentional edge crops and one heavily
overlapped scene. The command writes `visual-review-report.json`.

## Create the Background-Grouped Split

For the accepted mixed-device run, create the exact `4,800 / 900 / 300`
background-isolated split with:

```bash
uv run python -m pokemon_detector.cli.split \
  data/generated/mixed-device-6000-640-final-0907 \
  data/prepared/mixed-device-6000-640-final-0907 \
  --config configs/dataset/dataset-split-mixed-device-6000.yaml
```

The command refuses a non-empty destination. Its `split-report.json` must show
the exact counts and disjoint background ID lists before training.

```bash
uv run python -m pokemon_detector.cli.split \
  data/generated/baseline-3900 \
  data/prepared/baseline-3900 \
  --config configs/dataset/dataset-split-baseline.yaml
```

The accepted split contains 3,019 training, 569 validation, and 312 test images
across 75, 15, and 8 background groups. Inspect
`data/prepared/baseline-3900/split-report.json` and confirm no background ID is
shared between partitions before training.

Use a new generated run ID, prepared output directory, and training run name
for an experiment. Existing output directories are evidence and should not be
silently replaced.

## Train the Baseline

```bash
uv run python -m pokemon_detector.cli.train \
  --dataset data/prepared/baseline-3900/dataset.yaml \
  --config configs/training/train-baseline.yaml \
  --run-name baseline-3900
```

The accepted configuration starts with `yolo26n.pt`, trains for up to 100 epochs
at 640-pixel image size, uses seed 42, and selects Apple MPS. Training writes the
resolved configuration, environment metadata, metrics, and weights beneath
`runs/baseline-3900`.

The training configuration points to `models/yolo26n.pt`, which is excluded
from Git. Supply that starting checkpoint locally; obtaining it from an
upstream source requires network access unless you already have a copy. The
checked-in training configurations select MPS; use a compatible Apple Silicon
Mac for that setup, or change the `device` value to `cpu`. This project currently
accepts only `mps` and `cpu` as training devices.

MPS includes an accumulation operation without a deterministic implementation.
The inputs, split, seed, and configuration are reproducible, but checkpoint
bytes and final floating-point metrics are not guaranteed to be bit-identical.

## Train the Accepted Mixed-Device Dataset

Run the one-epoch, batch-64 MPS smoke test before starting the full run:

```bash
uv run python -m pokemon_detector.cli.train \
  --dataset data/prepared/mixed-device-6000-640-occlusion-safe-0907/dataset.yaml \
  --config configs/training/train-mixed-device-smoke-640.yaml \
  --run-name mixed-device-6000-640-occlusion-safe-0907-smoke
```

After the smoke run succeeds, start the 100-epoch training run:

```bash
uv run python -m pokemon_detector.cli.train \
  --dataset data/prepared/mixed-device-6000-640-occlusion-safe-0907/dataset.yaml \
  --config configs/training/train-mixed-device-6000-640.yaml \
  --run-name mixed-device-6000-640-occlusion-safe-0907
```

## Evaluate the Saved Checkpoint

Evaluate the untouched synthetic test partition:

```bash
uv run python -m pokemon_detector.cli.evaluate \
  --checkpoint runs/baseline-3900/weights/best.pt \
  --dataset data/prepared/baseline-3900/dataset.yaml \
  --split test
```

This regenerates the synthetic report, confusion matrices, failure analysis,
and 50-image failure gallery under
`runs/baseline-3900/evaluation/test`. The accepted test metrics are:

| Metric | Value |
| --- | ---: |
| Precision | 0.9950 |
| Recall | 0.9846 |
| mAP50 | 0.9915 |
| mAP50-95 | 0.9836 |

## Run Webcam Inference

`configs/inference/webcam.yaml` points to the accepted checkpoint and sets camera 0,
confidence 0.50, image size 640, and the MPS device.

The checkpoint and generated dataset referenced by this config are local-only
and are not in Git. After recreating or restoring the checkpoint, update the
config path if your run has a different name. Webcam inference also requires an
available camera and a desktop session that can show OpenCV's preview window.

```bash
uv run python -m pokemon_detector.cli.webcam \
  --config configs/inference/webcam.yaml
```

Press `q` or Escape to exit cleanly. The window shows detections and measured
FPS. To retain annotated review frames, set `save_low_confidence: true` in
`configs/inference/webcam.yaml`; detections below the internal 0.65 review threshold are
then written to the configured `low_confidence_directory`, currently
`runs/webcam-review`.

## Verification

Run the fast Task 20 Python gate:

```bash
uv run ruff check .
uv run pytest -m "not slow" --cov=pokemon_detector --cov-report=term-missing
```

Run the complete local suite when slow training and image integration tests are
required:

```bash
uv run pytest -q
```
