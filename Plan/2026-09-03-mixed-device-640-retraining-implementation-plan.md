# Mixed-Device 640 Retraining Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create, validate, train, and deploy a 6,000-frame mixed-device Pokémon dataset while retaining a fixed 640×640 YOLO/Core ML model input.

**Architecture:** Unity deterministically chooses a capture profile, native dimensions, lighting band, and object-size band per frame. It writes the native image, normalized YOLO labels, and enriched manifest. Python validates distribution gates, creates background-grouped splits, and trains YOLO26n at 640 with physical batch 64. The iOS app letterboxes full camera frames to 640 and maps boxes through the inverse transform.

**Tech Stack:** Unity 6000.3.8f1 / C# NUnit, Python 3.12 with `uv`, Ultralytics YOLO26n / PyTorch MPS, Swift / Core ML / AVFoundation.

**Spec:** `Plan/2026-09-03-mixed-device-640-retraining-design.md`

## Global Constraints

### September 7 implementation audit

Earlier completion statements overstated the verified state. Tasks 1–3 required a mesh-based placement correction: profile margins alone do not contain full meshes, and guessed scales repeatedly exhausted retries. The revised builder fits actual rotated geometry, preserves requested strata through retries, and applies mixed lighting/background settings through the live runner.

Task 4 is complete: exact frame-count and profile/lighting/size/class distribution gates are implemented, and the mixed-device splitter now assigns whole background groups to exact 4,800/900/300 image totals. Legacy split configs retain their ratio-based behavior.

Task 5 is complete: the accepted occlusion-safe dataset has dedicated smoke/full configurations, and a one-epoch physical-batch-64 MPS smoke run completed successfully in 0.061 hours with about 18.6 GB peak reported GPU/unified memory. Task 6 remains incomplete: its Swift test contains incorrect expected coordinates, and production rendering does not yet composite explicit RGB-114 padding. A simulator build alone did not verify those requirements. Task 7 full training and same-split synthetic baseline comparison are complete; Core ML export remains blocked until real iPhone captures are available and the promotion gate passes.

Canonical Python paths are `app/pokemon_detector`, `datasets/generated`, `datasets/prepared`, `experiments`, and `configs/{dataset,training,inference}`. Older paths in historical file lists below are superseded by these locations.

- Preserve all existing models, generated runs, and user changes; never overwrite `baseline-3900`.
- Keep class IDs fixed at `0=pikachu`, `1=charmander`, `2=squirtle`.
- Save native mixed-aspect PNGs; do not stretch source images to square.
- Train at `image_size: 640`, `batch: 64`, `epochs: 100`, `device: mps`.
- Use deterministic Unity recipes and record profile, resolution, lighting, and object-size metadata.
- Keep train/validation/test backgrounds disjoint.
- Create a `TaskCompleted/*.md` record only after each independently verified implementation task.

---

### Task 1: Add deterministic capture-profile and band contracts

**Files:**
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/CaptureProfile.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/LetterboxTransform.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/GenerationConfig.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/FrameRecipe.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/FrameRecipeSampler.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/FrameRecipeSamplerTests.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/LetterboxTransformTests.cs`

**Interfaces:**
- Produces `CaptureProfile`, `CaptureProfileCatalog`, `ObjectSizeBand`, `LightingBand`, and `LetterboxTransform`.
- `FrameRecipe` gains `CaptureProfile`, `OutputWidth`, `OutputHeight`, `ObjectRecipe.SizeBand`, and `LightRecipe.Band`.
- `FrameRecipeSampler.Sample(frameIndex, attemptIndex)` chooses all new values deterministically.

- [x] **Step 1: Write failing deterministic sampling tests**

Add tests that assert a default mixed-device configuration produces one of the documented profile names, a listed width/height pair, a valid object-size band, and a valid lighting band. Add a same-seed/same-frame test that compares JSON output exactly.

- [x] **Step 2: Write failing letterbox tests**

Assert that `LetterboxTransform.Create(360, 780, 640)` returns a scale of `640 / 780`, horizontal padding greater than zero, vertical padding zero, and that converting a normalized source rectangle to model pixels and back produces the original rectangle within `0.0001`.

- [x] **Step 3: Run the focused edit-mode tests and verify RED**

Run Unity batch-mode EditMode tests for `FrameRecipeSamplerTests` and `LetterboxTransformTests`. Expected: compilation/test failure because the new contracts do not exist.

- [x] **Step 4: Implement the contracts and validation**

Create profile definitions for Square, iPhonePortrait, iPhoneLandscape, Webcam, and LaptopWindow with the exact resolutions and weights from the design. Validate that profile weights total 1, dimensions are positive, and each size/lighting band has a valid range. Implement deterministic selection using isolated random streams so adding a profile-related field does not change class balancing.

- [x] **Step 5: Run focused and full Unity edit-mode tests**

Expected: all recipe and letterbox tests pass; all existing edit-mode tests remain green.

- [x] **Step 6: Commit and record completion**

Commit only Task 1 source/tests/meta files. Create `TaskCompleted/task-23-mixed-device-generation-contracts.md` with commands and passing results.

### Task 2: Capture native profile dimensions and enriched manifests

**Files:**
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Capture/CaptureSceneRunner.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Capture/SyntheticCaptureController.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Scene/FrameSceneBuilder.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Output/ManifestRecord.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Output/AtomicCaptureWriter.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/OutputWriterTests.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/PlayMode/CaptureSmokeTests.cs`

**Interfaces:**
- `SyntheticCaptureController.TryCreateArtifact` projects, captures, and serializes at `recipe.OutputWidth` and `recipe.OutputHeight`.
- `ManifestRecord` serializes capture profile, source dimensions/aspect, letterbox scale/padding, lighting band, and per-object model-input dimensions.

- [x] **Step 1: Write failing manifest serialization tests**

Construct a `ManifestRecord` containing an iPhone portrait recipe and assert its JSON contains `capture_profile`, native dimensions, `letterbox_scale`, `lighting_band`, `size_band`, and non-zero model-input box dimensions.

- [x] **Step 2: Write failing native-size capture smoke test**

Configure one frame for every capture profile, generate it through the controller, and assert PNG dimensions equal the recipe dimensions while YOLO coordinates remain normalized and finite.

- [x] **Step 3: Run focused tests and verify RED**

Expected: tests fail because capture and manifest code still use global `config.Width` and `config.Height`.

- [x] **Step 4: Implement per-recipe dimensions and metadata**

Pass recipe dimensions into `FrameSceneBuilder`, `MeshBoundsProjector`, and `CameraCaptureService`. Build the same `LetterboxTransform` used by training metadata. Reject any object whose final 640-letterbox box does not satisfy the selected size band. Preserve bounded retries and atomic image/label/manifest writes.

- [x] **Step 5: Verify Unity capture behavior**

Run focused EditMode plus PlayMode smoke tests. Run Unity CLI in an isolated temporary copy to reload the scene and capture five profile samples; inspect their image dimensions and overlays.

- [x] **Step 6: Commit and record completion**

Commit only Task 2 source/tests/meta files. Create `TaskCompleted/task-24-native-profile-capture-and-manifest.md`.

### Task 3: Implement lighting and projected-size strata

**Files:**
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/GenerationConfig.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/FrameRecipeSampler.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Scene/FrameSceneBuilder.cs`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/FrameRecipeSamplerTests.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/GenerationConfigMixedDeviceTests.cs`

**Interfaces:**
- Produces low/normal/bright lighting recipes at `20/60/20` weight and object bands at `30/50/20` object weight.
- Config validates all scalar ranges from the approved design.

- [x] **Step 1: Write failing distribution and validation tests**

Sample 10,000 deterministic recipes and assert profile, lighting, and object-band proportions fall within stated tolerances. Assert invalid profile weights, invalid light ranges, and invalid size-band pixel ranges throw clear validation errors.

- [x] **Step 2: Run tests and verify RED**

Expected: distribution assertions fail because current sampler uses uniform light and hard-coded object scales.

- [x] **Step 3: Implement stratum sampling**

Use separate deterministic random streams for profile, lighting, and object size. Widen background brightness/contrast/blur and configure `FrameSceneBuilder` from the selected lighting recipe without changing Pokémon material colors or ModelHolder scales.

- [x] **Step 4: Verify strata**

Run the focused tests and all EditMode tests. Save a 100-frame manual review run, inspect overlays across every lighting band and device profile, and document the result.

- [x] **Step 5: Commit and record completion**

Commit only Task 3 source/tests/meta files. Create `TaskCompleted/task-25-lighting-and-size-strata.md`.

### Task 4: Validate mixed-device manifests and dataset distributions in Python

Status: **Complete** (verified 2026-09-07)

**Files:**
- Modify: `Python-ModelTraining/src/pokemon_detector/domain.py`
- Modify: `Python-ModelTraining/src/pokemon_detector/dataset/validator.py`
- Modify: `Python-ModelTraining/src/pokemon_detector/cli/validate.py`
- Modify: `Python-ModelTraining/src/pokemon_detector/dataset/splitter.py`
- Modify: `Python-ModelTraining/tests/dataset/test_validator.py`
- Modify: `Python-ModelTraining/tests/dataset/test_splitter.py`
- Create: `Python-ModelTraining/configs/dataset-split-mixed-device-6000.yaml`

**Interfaces:**
- Python manifest parsing accepts both old records and enriched records.
- `ValidationReport` includes profile, lighting, and object-size distribution summaries and errors when required gates fail.

- [x] **Step 1: Write failing compatibility and distribution tests**

Use literal old and enriched manifest JSON fixtures. Assert old records still parse, valid mixed records yield summaries, and skewed profile/lighting/object-size data produces gate-specific errors.

- [x] **Step 2: Run focused pytest and verify RED**

Run `uv run pytest tests/dataset/test_validator.py tests/dataset/test_splitter.py -q`. Expected: failures because enriched metadata is not represented.

- [x] **Step 3: Implement optional metadata and validation gates**

Add typed optional fields, compute exact counts/percentages, and enforce the design thresholds only when the run declares mixed-device metadata. Keep standard validation behavior unchanged for old baseline runs.

- [x] **Step 4: Configure exact 4,800/900/300 split**

Add the split YAML with target counts and background-group assignment. Ensure the split report proves no background leakage.

- [x] **Step 5: Verify Python quality gates**

Run focused tests and the full Python test suite. Validate a temporary mixed-profile fixture and inspect its JSON report.

- [x] **Step 6: Commit and record completion**

Commit only Task 4 code/tests/config files. Create `TaskCompleted/task-26-mixed-device-dataset-validation.md`.

### Task 5: Add the 640/batch-64 training configuration and smoke gate

**Files:**
- Create: `Python-ModelTraining/configs/train-mixed-device-6000-640.yaml`
- Modify: `Python-ModelTraining/tests/training/test_config.py`
- Modify: `Python-ModelTraining/tests/training/test_runner.py`
- Modify: `Python-ModelTraining/README.md`

**Interfaces:**
- `TrainingConfig.load(configs/train-mixed-device-6000-640.yaml)` returns image size 640, batch 64, epochs 100, patience 20, MPS device, and run name `mixed-device-6000-640`.

- [x] **Step 1: Write failing configuration tests**

Assert the new YAML loads exact approved values and its dataset path must contain a passing validation report before the runner calls Ultralytics.

- [x] **Step 2: Run tests and verify RED**

Expected: failure because the new configuration file is absent.

- [x] **Step 3: Add training configuration and documented commands**

Set `model: yolo26n.pt`, `epochs: 100`, `patience: 20`, `image_size: 640`, `batch: 64`, `workers: 4`, `device: mps`, `seed: 42`, and `nominal_batch_size: 64`. Document validation, split, smoke training, full training, evaluation, and Core ML export commands.

- [x] **Step 4: Verify configuration and a short smoke run**

Run configuration tests. After the 6,000-frame run has passed validation, train a short smoke run with a unique run name before authorizing the 100-epoch job. Record actual MPS memory/epoch time; stop and report if batch 64 causes an error.

- [x] **Step 5: Commit and record completion**

Commit only Task 5 source/tests/docs/config files. Create `TaskCompleted/task-27-mixed-device-training-config.md`.

### Task 6: Align iOS preprocessing with Ultralytics letterboxing

**Files:**
- Create: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/LetterboxTransform.swift`
- Modify: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/PokemonDetector.swift`
- Modify: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/DetectionOverlay.swift`
- Modify: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/CameraManager.swift`
- Create: `Xcode-PokemonDetector/PokemonDetector/Tests/LetterboxTransformTests.swift`
- Modify: `Xcode-PokemonDetector/PokemonDetector/TaskCompleted/README.md`

**Interfaces:**
- `LetterboxTransform(sourceSize:targetSize:)` prepares a 640 model buffer with equal X/Y scaling and 114-gray padding.
- `modelRectToSource(_:)` maps a normalized YOLO output rectangle to normalized camera coordinates.

- [ ] **Step 1: Write failing Swift geometry tests**

Assert portrait and landscape source rectangles round-trip through letterbox mapping within `0.0001`, and assert portrait input receives horizontal rather than vertical padding.

- [ ] **Step 2: Run Swift test executable and verify RED**

Expected: compile failure because `LetterboxTransform` does not exist.

- [ ] **Step 3: Implement 640 letterbox preprocessing**

Replace square cropping in `PokemonDetector.preparedInput` with equal-scale rendering onto a `640x640` BGRA buffer filled with RGB 114. Map parsed model boxes through the inverse transform. Keep `SquareCropTransform` only until no production consumer remains, then remove it in the same task with its obsolete test.

- [ ] **Step 4: Verify build and device geometry**

Run standalone Swift tests and `xcodebuild` for the iOS 17 simulator target. Reload the Xcode scene/project, install on the physical phone, and compare portrait/landscape boxes against a known rectangular target.

- [ ] **Step 5: Commit and record completion**

Commit only Task 6 files inside the nested Xcode repository. Create `TaskCompleted/task-28-ios-letterbox-preprocessing.md` there.

### Task 7: Generate, train, evaluate, export, and promote the new model

**Files:**
- Create: `TaskCompleted/task-29-mixed-device-6000-run.md`
- Create: `Python-ModelTraining/data/generated/mixed-device-6000-640/` (ignored artifact)
- Create: `Python-ModelTraining/data/processed/mixed-device-6000-640/` (ignored artifact)
- Create: `Python-ModelTraining/runs/mixed-device-6000-640/` (ignored artifact)
- Modify: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/best.mlpackage` only after promotion

- [x] **Step 1: Generate 6,000 frames through Unity**

Open `PokemonCapture`, set run ID `mixed-device-6000-640`, frame count `6000`, seed `42`, and generate. Do not reuse or delete any older run directory.

Accepted replacement run: `mixed-device-6000-640-occlusion-safe-0907`. A unique suffix was required because older partial and verification runs were intentionally preserved. It contains 6,000 image/label/manifest triples, rejects near-total projected-box containment, and passed the mixed-device distribution gates.

- [x] **Step 2: Validate, split, and visually review**

Run the documented Python validation, split, and overlay commands. Do not continue until all design gates pass and at least 100 overlays have been reviewed.

Validation passed with zero errors and zero containment violations. The exact 4,800/900/300 background-isolated split is complete. All 100 profile-balanced overlays were reviewed (20 per profile); the formal review gate passed with every defect count at zero.

- [x] **Step 3: Run training smoke then full 100-epoch job**

Run the smoke config first. If it passes and batch 64 is stable, run the approved full config. Preserve resolved configs and reports in the run directory.

The 100-epoch native-MPS run completed in 5.638 hours with exit code 0. The run preserves
both checkpoints, resolved configuration, environment, split report, metrics CSV, and plots.
Final best-checkpoint validation on the 900-image validation split reached 0.995 mAP50 and
0.978 mAP50-95.

- [ ] **Step 4: Evaluate against baseline and real iPhone inputs**

Generate synthetic metrics, real-world metrics, and failure galleries for both checkpoints. Record per-class and device-profile comparisons in the completion record.

Synthetic comparison is complete on the same untouched 300-image occlusion-safe test split.
The new checkpoint reached 0.9947 mAP50 and 0.9756 mAP50-95 versus 0.9773 and 0.9209 for
the old baseline. It reduced test false negatives from 20 to 3 and improved mAP50-95 for
every capture profile. `datasets/real` contains zero images, so real iPhone comparison and
the promotion decision remain pending.

- [ ] **Step 5: Export and install only the promoted model**

Export `best.pt` to Core ML at 640, replace the Xcode model package only if the promotion gate passes, build, and test it on the phone in portrait and landscape.

- [ ] **Step 6: Record completion without deleting old artifacts**

Create `TaskCompleted/task-29-mixed-device-6000-run.md` with the exact run locations, versions, metrics, FPS, and the promotion decision.

## Plan Review

- Spec coverage: Tasks 1-3 implement Unity profiles, dimensions, lighting, scale bands, and metadata; Task 4 validates distributions and splits; Task 5 trains at 640/batch-64/100 epochs; Task 6 aligns iOS geometry; Task 7 operates and evaluates the full run.
- Placeholder scan: no deferred implementation placeholders; human-run generation and physical-device tests are explicitly bounded in Task 7.
- Interface consistency: all tasks use `FrameRecipe` native dimensions, `LetterboxTransform`, enriched manifest fields, and the exact mixed-device run ID.
