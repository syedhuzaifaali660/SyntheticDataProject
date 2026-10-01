# Three-Pokemon Live Detector Design

**Date:** 2026-08-29  
**Status:** Approved in conversation  
**Workspace:** `/Users/syedhuzaifaali/Desktop/Projects/SyntheticDataProject`

## Goal

Build a reproducible, end-to-end computer-vision project that uses Unity to generate automatically labeled synthetic images of Pikachu, Charmander, and Squirtle, trains a compact detector in Python, and identifies those three classes from the MacBook webcam in real time.

## Success Criteria

- Unity imports and renders all three supplied `.glb` models correctly in URP.
- Every generated RGB image has a valid YOLO annotation and manifest record.
- Dataset validation finds no missing pairs, invalid class IDs, or out-of-range boxes.
- Synthetic train, validation, and test partitions do not share background images.
- The training pipeline runs locally on Apple Metal/MPS and produces a best checkpoint.
- The live webcam application displays boxes, class names, confidence, inference time, and FPS.
- Live inference maintains at least 15 FPS on the current M4 Pro MacBook.
- In controlled live trials, each class is identified in at least 8 of 10 trials.
- A two-minute negative-background trial produces no persistent false detection lasting five consecutive frames.

## Architecture

The Unity 6.3 URP project owns asset import, scene randomization, image capture, and direct YOLO annotation export. The Python project owns dataset validation, background-grouped partitioning, YOLO26n fine-tuning, synthetic and real-world evaluation, and OpenCV webcam inference. Unity writes versioned generation runs beneath `Python-ModelTraining/data/generated`; Python never reads Unity's `Library` or temporary editor state.

The pipeline intentionally avoids the video's Python 3.5, TensorFlow 1.8, TFRecord conversion, fixed 20,000-step training loop, and long experiments without real-world validation.

## Fixed Class Taxonomy

| ID | Machine name | Display name |
|---:|---|---|
| 0 | `pikachu` | Pikachu |
| 1 | `charmander` | Charmander |
| 2 | `squirtle` | Squirtle |

Class IDs are immutable after the first generated dataset. Unity configuration, manifests, `dataset.yaml`, evaluation reports, and webcam inference must use this order.

## Unity Responsibilities

### Asset preparation

- Install Unity glTFast by package name `com.unity.cloud.gltfast` and commit the resolved package lock.
- Import the supplied `.glb` files as editor assets.
- Wrap each imported model in a prefab with a stable pivot, forward direction, unit-scale `ModelHolder`, natural model scale, and `PokemonLabel` component.
- Preserve the supplied textures and identity-defining colors.

### Scene generation

Each generated frame uses a deterministic seed and contains zero to three labeled objects. The generator randomizes:

- background selection, crop, scale, brightness, contrast, blur, and mild color temperature;
- class-balanced object selection;
- plausible position, distance, scale, and rotation;
- camera field of view within a configured range;
- ambient and point-light intensity, direction, and temperature;
- controlled partial cropping and occlusion;
- optional mild sensor noise and JPEG compression in a targeted expansion, only when baseline webcam failures show that camera artifacts are a measured limitation.

The generator does not recolor Pokémon, deform meshes, or use unrestricted rotations.

### Annotation

The bounding-box projector caches local mesh vertices once, transforms them to camera space per capture, and calculates a tight screen-space rectangle from visible projected vertices. Frames are rejected when the box is invalid, smaller than the configured minimum, or more heavily cropped than the configured maximum.

Unity writes annotations directly in normalized YOLO form:

```text
class_id center_x center_y width height
```

Each run also contains `manifest.jsonl` with the run ID, frame ID, seed, split hint, background ID, image size, object classes, transforms, and boxes.

## Python Responsibilities

### Environment

- Use `uv` with Python 3.12.
- Use a project-local `.venv`.
- Lock dependencies in `uv.lock`.
- Use Ultralytics YOLO26n, PyTorch/MPS, OpenCV, Pillow, PyYAML, NumPy, and pytest.

### Dataset quality

Python validates image-label pairing, image readability, class IDs, normalized coordinates, positive box area, class balance, and manifest consistency. It creates overlay samples and a machine-readable validation report before a run can be used for training.

Dataset partitions are assigned by background group, not by randomly shuffling frames. This prevents near-identical synthetic scenes from leaking across train and validation.

### Training

Training starts from `yolo26n.pt` and follows gates:

1. Overfit a tiny dataset to prove labels and learning behavior.
2. Run a short 5-10 epoch pipeline smoke test.
3. Train the first 3,000-image baseline with early stopping.
4. Evaluate per-class precision, recall, mAP, confusion, and failure examples.
5. Expand toward 5,000-6,000 images only when results justify it.

Every run saves its resolved configuration, random seed, environment versions, metrics, and best checkpoint.

### Real-world evaluation

Synthetic validation is reported separately from real-world validation. Because physical plush toys are not available, the first real set can use webcam captures of Pokémon cards, printed images, posters, book covers, and images displayed on another screen at varied angles, scales, and lighting.

If synthetic-only performance is inadequate, a small real fine-tuning partition may be added. A separate real test partition remains untouched so improvement is measured honestly.

### Webcam application

The live application opens a configurable camera, performs letterboxed inference, draws detections, computes rolling FPS, and optionally saves low-confidence frames. It exits cleanly on `q` or Escape and reports actionable errors for missing cameras or checkpoints.

## Dataset Stages

| Stage | Size | Purpose | Gate |
|---|---:|---|---|
| Technical smoke | 20 frames | Confirm files and annotations | Zero validation errors |
| Visual review | 100 frames | Review box placement and diversity | Overlay review accepted |
| Training pilot | 300 frames | Prove Python training path | Tiny-set overfit and smoke training pass |
| Baseline | About 3,900 total | First measured detector | Synthetic and real reports produced |
| Expansion | Up to 6,000 total | Target measured weaknesses | Only run after baseline review |

The baseline allocation is approximately 3,000 training, 600 validation, and 300 test images, while maintaining background-group isolation.

## Error Handling

- Unity fails a capture run before writing images when the output directory, taxonomy, prefab list, or background list is invalid.
- Individual invalid frames are logged with seed and rejection reason, then retried up to a configured limit.
- Atomic temporary files are renamed only after image and annotation writes succeed.
- Python validation exits non-zero on structural errors and distinguishes warnings such as class imbalance.
- Training refuses to start without a passing validation report for the selected dataset run.
- Webcam inference fails with a clear message when no camera or checkpoint is available.

## Testing Strategy

### Unity

- Edit-mode tests cover class mapping, deterministic sampling, projection math, YOLO normalization, manifest serialization, and atomic output naming.
- A play-mode smoke test captures a small deterministic run and checks file counts.
- Overlay images provide visual verification of boxes against rendered Pokémon.

### Python

- Unit tests cover label parsing, validation errors, background-grouped splits, configuration loading, metric summaries, and rolling FPS.
- Integration tests use temporary fake datasets and a fake frame source.
- A training smoke test uses a tiny fixture and short run; it is marked separately from fast unit tests.

## Out of Scope for the First Complete Version

- Detecting Pokémon beyond the three fixed classes.
- Mobile, iOS, Android, or Unity runtime deployment.
- Instance segmentation, pose estimation, or tracking IDs.
- Cloud training orchestration.
- Photorealistic plush-toy reconstruction.
- Automatic downloading of copyrighted Pokémon imagery.
- A graphical training dashboard.

## Key Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Downloaded 3D models do not resemble real targets closely enough | Measure against a separate real validation set early; add limited real fine-tuning only if needed |
| Only 98 backgrounds cause memorization | Group backgrounds by split and augment crop, exposure, blur, and scale |
| Loose or incorrect boxes poison training | Unit-test projection and visually inspect 100 overlays before baseline generation |
| Long training hides pipeline mistakes | Require tiny-set overfit and short smoke training first |
| Unity package incompatibility | Use Unity 6-compatible glTFast only for asset import; keep capture/labeling code project-owned |
| Webcam demo is slow | Begin with YOLO26n; measure PyTorch/MPS first and consider Core ML only after profiling |
