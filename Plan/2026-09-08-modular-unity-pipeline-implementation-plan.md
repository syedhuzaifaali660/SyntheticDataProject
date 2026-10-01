# Modular Unity Synthetic Data Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Use superpowers:subagent-driven-development only if delegation is separately authorized. Steps use checkbox syntax for tracking.

**Goal:** Make a new detection category configurable through Unity's Inspector and consumable by the existing Python application without editing category-specific code.

**Architecture:** Dataset projects own class identities and model variants; presets own reusable generation defaults. A resolver creates an immutable configuration for the existing recipe/build/project/capture/write pipeline. Versioned taxonomy and run metadata supply Python's validation, splitting, training, evaluation, and inference boundaries.

**Tech Stack:** Unity 6000.3.8f1, URP 17.3.0, glTFast 6.20.0, C#, Unity Editor/NUnit tests, Python 3.12, uv, pytest, Ruff, PyYAML, installed Ultralytics/PyTorch.

**Spec:** [Modular Unity pipeline design](2026-09-08-modular-unity-pipeline-design.md)

**Status:** Proposed plan, not an execution record. All checkboxes intentionally remain open. First-release assumption: fixed poses; animation sampling follows later unless requested.

## Global constraints

- Retain Unity `6000.3.8f1`, URP `17.3.0`, glTFast `6.20.0`, and Python `>=3.12,<3.13` with the current lockfile.
- Keep `Python-ModelTraining/src/pokemon_detector`, `data/generated`, `data/prepared`, `runs`, and `models` as the active layout.
- Preserve both accepted Pokémon datasets, prepared splits, training runs, checkpoints, source models, and all unrelated working-tree changes.
- Retain normalized five-column YOLO labels, native image aspect ratios, background-isolated splits, and validation before training.
- Keep imported model-child transforms/materials and unit-scale `ModelHolder` wrappers; apply capture variation to instance roots.
- Snapshot settings before capture; all sampling belongs in deterministic recipes. Use a recorded sampler version.
- New category support must include Python overlays, evaluation, and webcam inference, not only Unity generation.
- Do not revive the removed real-data annotation/trial workflow or change iOS model handling in this scope.
- The new schema must never interpret a missing or malformed taxonomy as the legacy Pokémon mapping.
- Use unique run directories and retain failed/cancelled evidence. No automatic resume, overwrite, large training run, or checkpoint replacement.

## Delivery order and file boundaries

Before the first implementation edit, record scoped Git state, fresh Python/Unity baseline results where runnable, and retained artifact hashes outside the accepted run directories. Record environmental failures separately. The existing workspace has substantial unrelated changes; use scoped changes and preserve them throughout. Task 7 repeats the preservation comparison before migrating the existing scene.

Implement in the order below; the final migration depends on working compatibility readers and a proven new capture path. The milestones are independently testable, but only milestone 7 establishes end-to-end completion.

Paths in the lists below are relative to the workspace root. `U` means `Unity-SyntheticDataGenrator/Assets/SyntheticData`; `P` means `Python-ModelTraining`. These abbreviations refer to paths, not new directories.

| Milestone | Deliverable | Main risk resolved |
| --- | --- | --- |
| 1 | Shared taxonomy/schema and legacy readers | Unity/Python label disagreement |
| 2 | Dataset assets and model preparation | Manual prefab setup and fixed catalog |
| 3 | Configurable deterministic sampling and placement | Hidden defaults, hard-coded profiles, geometry failure |
| 4 | Project-driven rig, preview, capture lifecycle | Scene dependence, cancellation, incomplete output |
| 5 | Unified Inspector and presets | Usability and persistence |
| 6 | Generic Python workflow and checkpoint selection | Hidden downstream Pokémon assumptions |
| 7 | Pokémon migration and second-category acceptance | Regression and misleading success claims |

### Task 1: Define the dataset contract and compatibility boundary

**Files:**
- Create `U/Runtime/Domain/ClassDefinition.cs` — serialized class identity row.
- Create `U/Runtime/Domain/TaxonomySnapshot.cs` — immutable class lookup and hash.
- Create `U/Runtime/Output/DatasetContract.cs` — versioned run metadata DTOs.
- Create `P/src/pokemon_detector/dataset/taxonomy.py` — strict identity validation and lookup.
- Create `P/src/pokemon_detector/dataset/contract.py` — schema selection and contract loading.
- Create `P/src/pokemon_detector/dataset/legacy_contract.py` — frozen Pokémon compatibility policy.
- Create `Contracts/synthetic-dataset-v2.md` — language-neutral field and canonical-hash specification.
- Create `Contracts/fixtures/taxonomy-two-classes.json` and `Contracts/fixtures/taxonomy-legacy.json`.
- Create `U/Tests/EditMode/TaxonomyTests.cs` and `P/tests/dataset/test_taxonomy.py`, `test_contract.py`.

**Interfaces:**
- C#: `TaxonomySnapshot.GetClass(int id)` returns a `ClassDefinition`; `TaxonomySnapshot.Hash` exposes the identity hash.
- Python: `Taxonomy.load(path: Path) -> Taxonomy`; `Taxonomy.require_class(class_id: int, class_name: str | None = None) -> None`; `Taxonomy.names -> dict[int, str]`.
- Python: `load_dataset_contract(run_dir: Path) -> DatasetContract` produces taxonomy, schema version, reference input size, expected count, and quality policy.
- No current runtime enum is removed in this task.

- [ ] Write the version-2 field specification and shared JSON fixtures first. Use explicit IDs `0=syringe`, `1=vial` with stable keys, distinct display names and colors. Include dataset ID/revision and expected identity hash. Define canonical identity bytes as UTF-8 JSON of the ordered `{id,key,name}` list with specified escaping, no whitespace/BOM, and SHA-256. Both languages must match literal bytes and hash.
- [ ] Add failing tests for the fixture and for dense IDs, duplicate names/keys, negative IDs, boolean/fractional IDs, unknown versions, missing version-2 taxonomy, and mismatched names/hash. Demonstrate that list display order cannot change stored IDs.

Representative Python assertions after loading the shared fixture:

```python
assert taxonomy.names == {0: "syringe", 1: "vial"}
taxonomy.require_class(1, "vial")
with pytest.raises(ValueError):
    taxonomy.require_class(1, "syringe")
with pytest.raises((TypeError, ValueError)):
    taxonomy.require_class(True)
```

- [ ] Run the new Python tests and Unity `TaxonomyTests`; confirm failures reflect absent/new behavior, not an environment startup failure.
- [ ] Implement strict taxonomy and contract parsing, including an explicit unversioned legacy path. A legacy fixture contains the actual older config/manifest shape and exact three-class mapping. A version-2 declaration always requires its metadata.
- [ ] Run focused tests and current domain/parser tests. Verify Python can inspect the two retained generated runs without writing to them.
- [ ] Review and commit only the task's new files and tests if committing is part of the execution session. Record actual results in `TaskCompleted/task-2026-09-08-modular-pipeline-contract.md` only after the milestone passes.

### Task 2: Add dataset projects, reusable presets, and model preparation

**Files:**
- Create `U/Runtime/Authoring/DatasetProject.cs`, `ObjectDefinition.cs`, `GenerationPreset.cs`, `BackgroundSet.cs`.
- Create `U/Runtime/Authoring/GenerationSettings.cs` — serializable camera/object/light/background/quality groups.
- Create `U/Runtime/Authoring/GenerationOverrides.cs` — explicit per-group override flags and values.
- Create `U/Runtime/Generation/ResolvedGenerationConfig.cs`, `GenerationConfigResolver.cs`.
- Create `U/Editor/Authoring/ModelPreparationService.cs`, `DatasetAssetValidator.cs`.
- Create `U/Tests/EditMode/DatasetProjectTests.cs`, `ModelPreparationTests.cs`, `GenerationConfigResolverTests.cs`.
- Reference, without rewriting, `U/Prefabs/{Pikachu,Charmander,Squirtle}.prefab` and the installed importer assets.

**Interfaces:**
- `GenerationConfigResolver.Resolve(DatasetProject project, CaptureRunOptions options) -> ResolvedGenerationConfig` deep-copies and validates inputs.
- `CaptureRunOptions` is defined here with run ID, output root, frame count, and seed; it does not own generation ranges.
- `ModelPreparationService.Prepare(GameObject source, string outputFolder) -> ObjectDefinition` creates a controlled wrapper and stable variant ID.
- `DatasetAssetValidator.Validate(DatasetProject project)` returns issues containing severity, asset path, field path, and repair message.

- [ ] Write failing tests using temporary asset folders and procedural geometry for one-class/multiple-variant and four-class projects. Verify imported source transforms/materials are unchanged, model wrappers are centered/grounded, and known Pokémon holder rotations survive migration.
- [ ] Write resolver tests for preset inheritance, explicitly disabled override, model-only placement override, and post-resolution edits. The effective snapshot must not change when its source asset changes.

Representative resolver test invariant:

```csharp
var resolved = GenerationConfigResolver.Resolve(project, options);
var capturedSeed = resolved.Seed;
project.Preset.Settings.Camera.MinimumFov = 55f;
Assert.That(resolved.Seed, Is.EqualTo(capturedSeed));
Assert.That(resolved.Camera.MinimumFov, Is.EqualTo(originalMinimumFov));
```

`project`, `options`, and `originalMinimumFov` are initialized by the test with a fresh asset and a known 40-degree minimum before resolution.

- [ ] Run the three new Edit Mode fixtures to establish failures.
- [ ] Implement assets/resolver and safe model-wrapper creation. Explicitly select included renderers/one LOD, detect unsupported scripts/deformation, preserve materials, and handle readable static and fixed-pose skinned geometry. Do not attach class-specific labels to the imported source.
- [ ] Make class appending stable and reordering cosmetic. Track the last captured identity snapshot so machine-name changes/deletions after capture require a new revision. Reject a project with a class that has no valid variant.
- [ ] Run the new fixtures plus existing prefab/import tests, reopen saved assets, and verify serialization. Record this milestone only with actual passing evidence.

### Task 3: Generalize sampling, camera controls, and geometry fitting

**Files:**
- Modify `U/Runtime/Generation/FrameRecipe.cs`, `FrameRecipeSampler.cs`, `CaptureProfile.cs`, `GenerationConfig.cs`.
- Create `U/Runtime/Generation/ObjectSelectionSampler.cs`, `CameraRecipeSampler.cs`, `AppearanceRecipeSampler.cs`.
- Create `U/Runtime/Generation/ObjectPlacementSampler.cs`, `ResolvedFrame.cs`.
- Modify `U/Runtime/Scene/ProjectedObjectPlacement.cs`, `FrameSceneBuilder.cs`, `SpawnedFrame.cs`.
- Modify `U/Runtime/Labels/MeshVertexCache.cs`, `MeshBoundsProjector.cs` for explicit renderer selection and fixed-pose consistency.
- Modify `U/Tests/EditMode/FrameRecipeSamplerTests.cs`, `FrameSceneBuilderTests.cs`, `MeshBoundsProjectorTests.cs`.
- Create `U/Tests/EditMode/GenericPlacementTests.cs`, `CameraRecipeTests.cs`.

**Interfaces:**
- `FrameRecipeSampler(ResolvedGenerationConfig config).Sample(int frameIndex, int attemptIndex) -> FrameRecipe`.
- `ObjectRecipe` adds `InstanceId`, `VariantId`, target model-pixel size, placement mode, and requested transforms.
- `FrameRecipe` gains camera pose/clipping and reference model size.
- `FrameSceneBuilder.Build(FrameRecipe recipe)` returns the spawned frame and its `ResolvedFrame`, including final poses/boxes, without modifying the input recipe.
- All samplers consume named, versioned random streams from the existing seeded random abstraction.

- [ ] Add failing cases for 1/2/4 classes, two variants of one class, four feasible objects in one frame, two instances of the same class, and a custom portrait profile. Hold class/variant/profile/lighting/size strata fixed across retries.
- [ ] Add camera tests with nonzero translation and yaw/pitch/roll: verify object projection against the resolved camera pose and complete restoration afterward. Verify a camera rotation changes the relative view, and a fixed rotation range produces the requested angle exactly.
- [ ] Add thin-object tests: a long narrow procedural cuboid should either satisfy both longest-side and native short-side rules or fail with the precise infeasible constraint. Add a fixed-pose skinned test with non-unit imported child scale to catch double-scaling.
- [ ] Run sampler, placement, projector, and builder fixtures; verify expected failures.
- [ ] Replace `ClassCount=3`, `{0,1,2}`, and the three-center fallback with taxonomy-driven cycles and a bounded layout search that supports configurable counts. Keep equal class balance independent of variant counts. A placement budget exhaustion must reject with context, never reduce the requested object count silently.
- [ ] Read profile/lighting/size weights and ranges from the resolved settings. Move target-side randomness out of `ProjectedObjectPlacement.Fit`; the fitter consumes the recipe's explicit target. Replace fixed `640` calculations with the resolved reference size.
- [ ] Implement the two placement modes from the design. Validate finite positive ranges, near/far clipping, profile dimensions/weights, empty catalogs, min/max ordering, and impossible margin/size combinations before capture.
- [ ] Run focused and complete Edit Mode tests, including deterministic repeated sampling. Record the new sampler version and explain that old seed sequences are preserved as historical artifacts rather than promised identical under a new algorithm.

### Task 4: Connect a generic rig to preview and reliable run output

**Files:**
- Create `U/Runtime/Capture/CaptureRig.cs`, `CaptureRunSession.cs`, `CaptureRunResult.cs`.
- Modify `U/Runtime/Capture/CaptureSceneRunner.cs`, `SyntheticCaptureController.cs`.
- Modify `U/Runtime/Output/ManifestRecord.cs`, `AtomicCaptureWriter.cs`.
- Create `U/Runtime/Output/RunMetadataWriter.cs` — config, taxonomy, status, and rejection logging.
- Create `U/Runtime/Domain/GeneratedObjectLabel.cs` — runtime identity set from the project.
- Modify `U/Runtime/Validation/BoundsOverlay.cs` to receive taxonomy/colors.
- Create `U/Editor/CapturePreviewService.cs`, `U/Prefabs/GenericCaptureRig.prefab`.
- Modify `U/Tests/EditMode/OutputWriterTests.cs`, `BoundsOverlayTests.cs`.
- Modify `U/Tests/PlayMode/SyntheticCaptureControllerTests.cs`, `CaptureSmokeTests.cs`.

**Interfaces:**
- `CaptureRunSession` owns the resolved snapshot, progress, cancellation, and one terminal `CaptureRunResult` with completed/cancelled/failed state and cause.
- `CapturePreviewService` runs one sampled recipe through the same rig/build/project/render path, returns RGB/overlay information, then restores the scene; it does not create a run directory.
- `RunMetadataWriter` writes version-2 headers once and commits terminal status only after completion checks.

- [ ] Add failing integration tests for arbitrary class name/color, native profile dimensions, correct camera pose metadata, variant IDs, no overlay pixels in captured RGB, and no scene/global state leaks.
- [ ] Add failure tests for stop during capture, writer exception, retry exhaustion, existing output path, and output count mismatch. Check `run-status.json` cannot become completed in these cases.
- [ ] Run the focused output/Edit Mode tests and graphics-backed Play Mode fixtures.
- [ ] Wire the new resolved config/catalog into the existing capture pipeline. Replace enum conversions in builder/controller/overlay with the explicit taxonomy and generated instance identity. Keep old runner input supported through a compatibility adapter until migration.
- [ ] Write `taxonomy.json` and versioned config before frame output; preserve atomic PNG/label/manifest handling. Persist rejection details with bounded retention plus total counts. Make controller exceptions propagate to a failed result so the editor cannot display false success.
- [ ] Snapshot/restore camera transform and clipping in addition to existing FOV/aspect state. Ensure preview/cancel/failure destroy only owned objects and dispose render textures.
- [ ] Generate a small version-2 non-Pokémon fixture through the rig, inspect all overlays and native dimensions, then run the contract reader from Task 1. Full Python validation follows Task 6; do not claim it here.

### Task 5: Build the Inspector workflow and reusable defaults

**Files:**
- Create `U/Editor/Inspectors/DatasetProjectEditor.cs`, `ObjectDefinitionEditor.cs`, `GenerationSettingsDrawer.cs`.
- Create `U/Editor/Authoring/DatasetProjectWizard.cs`, `DatasetProjectPreferences.cs`.
- Modify `U/Editor/CaptureRunWindow.cs`, `CaptureRunCommand.cs`.
- Create `U/Editor/PythonCommandBuilder.cs`.
- Create `U/Presets/GeneralObjects.asset`, `PokemonBaseline.asset`, `PokemonMixedDevice.asset` plus metadata files.
- Modify `U/Tests/EditMode/CaptureRunRequestValidationTests.cs`.
- Create `U/Tests/EditMode/EditorAuthoringTests.cs`, `PythonCommandBuilderTests.cs`.

**Interfaces:**
- The Inspector owns authoring, not sampling or geometry. It calls Tasks 2–4 services.
- Editor and batch capture resolve the same `DatasetProject` and `CaptureRunOptions`.
- New batch option: `-captureProject <Unity asset path>` selects the project. Existing Pokémon CLI behavior remains available during migration.
- `PythonCommandBuilder` produces shell-quoted commands with explicit run/config paths; it never executes a command.

- [ ] Add failing tests for project creation, drag-to-existing-class versus add-class, asset Undo/persistence, preset reset, Save As Preset isolation, and project selection after a Play Mode/domain reload.
- [ ] Add request/command tests for paths containing spaces, quotes, and shell metacharacters. Verify that a shell argument encoder handles literal values and the preview uses exactly the requested output path.
- [ ] Run the new Editor fixtures and current request-validation tests.
- [ ] Build the Inspector layout from the design using serialized properties. Provide inline variant settings and validation issues with object selection/repair links. Generate IDs automatically and show them read-only.
- [ ] Implement drag-and-drop model/folder handling through `ModelPreparationService`; batch operations must report per-asset errors and retain successful asset references. Texture folder drops populate stable background groups.
- [ ] Add base camera capture, per-axis range controls, units, advanced foldouts, inherited-value indicators, Save As Preset, and personal default preference. No preset is silently modified when a project is edited.
- [ ] Remove the literal `PokemonCapture` scene-name gate from the new project path. Use the generic rig or a validated assigned rig. Persist project GUID and the requested snapshot across Play Mode so the intended dataset starts after reload.
- [ ] Add Preview/Next/Smoke/Generate/Stop and progress. Exit Play Mode only if this tool entered it, and restore the prior editor state. The existing capture window becomes a shortcut to the same session services.
- [ ] Perform a manual editor walkthrough from a new project asset to a smoke run. Measure elapsed setup time and count required steps. Target under five minutes after assets are imported for a simple two-class dataset; report the observed time rather than asserting this target untested.

### Task 6: Generalize the complete Python workflow

**Files:**
- Modify `P/src/pokemon_detector/domain.py`.
- Modify `P/src/pokemon_detector/dataset/{parser,validator,overlay,splitter}.py`.
- Modify `P/src/pokemon_detector/cli/{validate,split,visual_review,train,evaluate,webcam}.py` as needed for contract/config loading.
- Modify `P/src/pokemon_detector/training/{config,runner}.py`.
- Modify `P/src/pokemon_detector/evaluation/{metrics,evaluator,gallery}.py`.
- Modify `P/src/pokemon_detector/inference/{detector,webcam}.py`.
- Create `P/src/pokemon_detector/training/model_contract.py` — checkpoint task/names/provenance handling.
- Modify `P/tests/test_domain.py` and affected tests beneath `tests/{dataset,training,evaluation,inference}`.
- Create `P/tests/test_generic_pipeline.py`, `P/configs/training/train-generic-smoke.yaml`.
- Modify `P/configs/classes.yaml` to identify it as a legacy preset, not the global taxonomy source.

**Interfaces:**
- `parse_yolo_label(path: Path, *, taxonomy: Taxonomy) -> list[YoloBox]` is explicit about its class set; legacy callers obtain a taxonomy from the legacy contract.
- `validate_run` retains its public run-directory entry point and loads the applicable contract internally.
- Split, overlay, evaluation, gallery, and metrics functions receive or load the same contract rather than constructing global mappings.
- `TrainingConfig.model` remains a resolved local path but is no longer restricted to the basename `yolo26n.pt`.
- `model_contract.py` validates a detection task and compares checkpoint class names with the destination taxonomy at evaluation/inference time; fresh training uses the dataset's destination names.

- [ ] Extend temporary dataset fixtures to schema version 2 with 1, 2, and 4 classes. Keep literal legacy fixtures. Add a `syringe/vial` dataset fixture with sufficient background groups for the current splitter and both classes represented in training.
- [ ] Add failing cases at every downstream boundary: class ID 3, single-class metrics, a class with zero predictions, arbitrary names/colors, wrong checkpoint taxonomy, unknown predicted class, and mismatched sidecar metadata.
- [ ] Add separate validation cases for a square-only run with an expected count, custom capture profiles/weights, insufficient production class coverage, and a partial/cancelled run. A count check must not impose the old five-profile policy.
- [ ] Add split/provenance tests: preserve source taxonomy/hash, reject incompatible dataset revisions, fail missing training-class coverage, and reject stale version-2 validation evidence when metadata/labels/frame membership change. Bind the report to actual input digests rather than trusting a copied `passed: true` flag.
- [ ] Run focused pytest suites and confirm new behavior fails before edits.
- [ ] Decouple geometry dataclasses from `PokemonClass`, then require taxonomy membership/name checks at each input boundary. Keep strict integer validation before conversions and retain six-decimal edge-rounding behavior.
- [ ] Update all class iteration, names, colors, profiles, review quotas, and distribution gates to use resolved metadata. Keep the fixed legacy policies only in the legacy reader.
- [ ] Generate `dataset.yaml` from taxonomy names, copy the contract beside it, and archive it with training runs. Add a taxonomy/checkpoint sidecar and verify the trained model names before declaring training output usable.
- [ ] Permit local compatible `.pt` detection checkpoints, fail missing files and unsupported tasks clearly, retain MPS/CPU configuration, and preserve the default YOLO26n path. Do not auto-download a new model or silently change batch/device.
- [ ] Create the generic smoke configuration below. The CLI supplies the dataset and unique run name. Validate the configured reference model size against the dataset contract. Batch 8 is a proposed lightweight starting point, not the existing mixed-device production batch contract.

```yaml
model: models/yolo26n.pt
epochs: 1
patience: 1
image_size: 640
batch: 8
nominal_batch_size: 64
workers: 2
device: mps
seed: 42
training_augmentation: true
runs_dir: runs
```

- [ ] Run generic pipeline tests with fake YOLO and frame source; then run Ruff and the complete non-slow suite. Confirm the actual Task 4 version-2 run validates and splits correctly.

Representative acceptance assertions in `test_generic_pipeline.py`:

```python
assert generated_contract.taxonomy.names == {0: "syringe", 1: "vial"}
assert prepared_yaml["names"] == generated_contract.taxonomy.names
assert training_call["data"] == str(prepared_dataset_yaml)
assert [item.class_name for item in evaluation.per_class] == ["syringe", "vial"]
assert displayed_detection.class_name == "vial"
```

The integration fixture constructs a version-2 run in a temporary `data/generated` path, creates actual non-uniform PNGs and labels across at least ten background groups, splits into temporary `data/prepared`, and supplies fake training/evaluation/inference adapters that emit known results. Existing fake adapters can be generalized in place; do not test real training by fabricating a checkpoint file.

### Task 7: Migrate Pokémon and prove a new category end to end

**Files:**
- Create `U/Editor/Authoring/PokemonProjectMigration.cs`.
- Create `U/Projects/Pokemon/PokemonDataset.asset` and referenced preset/background/model assets with `.meta` files.
- Modify `U/Scenes/PokemonCapture.unity` only after migration verification; keep the original scene/prefab identities where possible.
- Add `P/scripts/capture_dataset.sh`; keep `capture_mixed_device.sh` as an explicit compatibility wrapper.
- Modify root `README.md`, `U/README.md`, and `P/README.md`.
- Create `TaskCompleted/task-2026-09-08-modular-unity-pipeline.md` after final verification.

**Interfaces:**
- The migration utility converts the existing labeled catalog and settings to the new assets, preserving `0=pikachu`, `1=charmander`, `2=squirtle` and known model transforms.
- The generic shell wrapper forwards dataset project selection and run options to the new batch entry point.

- [ ] Establish fresh baselines before changing existing assets: scoped Git state, Python fast-test results, Unity XML results, and hashes of retained dataset/checkpoint files. Do not reset, stage wholesale, or bundle the current large unrelated artifact migration.
- [ ] Add and run a failing migration test that loads actual Pokémon prefabs and checks class order, holder transforms, background membership, and resolved legacy/mixed presets.
- [ ] Implement the additive migration utility. Re-run it to prove it does not create duplicate assets or alter existing mappings. Do not bulk-rename serialized C# types or regenerate `.meta` files.
- [ ] Run all Unity Edit Mode and graphics-backed standalone Play Mode tests. A licensing startup error is a blocked verification gate, not a test failure or pass.
- [ ] Use the Inspector to create a second category with at least two classes and two variants in one class. Use supplied assets if available; otherwise label procedural models as technical proxies. Generate a unique 20-frame smoke run and review every overlay.
- [ ] Generate a unique 100-frame review run with adequate backgrounds and policy-defined coverage. Validate and review it. Verify at least one custom profile and non-default camera pose, plus thin and fixed-pose skinned assets in dedicated focused captures.
- [ ] Split the accepted review run using background grouping. Verify disjoint groups, exact frame pairing, metadata inheritance, and required training-class coverage. Use ratio mode for the small pilot unless exact counts are feasible.
- [ ] Run a one-epoch local YOLO26n training smoke with a unique run name, followed by evaluation and webcam adapter checks for the new labels. Record actual environment, checkpoint existence, taxonomy, metrics, and errors. This proves pipeline operation, not useful real-world recognition.
- [ ] Revalidate copied/scratch views of legacy datasets and run legacy checkpoint name/compatibility checks. Do not regenerate reports inside retained accepted runs or overwrite their metadata. Compare retained hashes after the work.
- [ ] Update active operating guides with the new Inspector onboarding flow, canonical paths, snapshot/class revision rules, and current commands. Historical plans and completion records remain historical.
- [ ] Create completion evidence only for checks actually run. Remove old runtime enum/catalog dependencies only when all new and legacy gates pass; historical fixtures/compatibility code can retain the old mapping explicitly.

## Verification commands

Run Python commands from `Python-ModelTraining`:

```bash
uv run ruff check .
uv run pytest -m "not slow" --cov=pokemon_detector --cov-report=term-missing
uv run pytest tests/test_generic_pipeline.py -v
```

Run Unity commands from the workspace root with the editor closed or a deliberately isolated test checkout. Do not use `-quit` with Test Runner and do not use `-nographics` for rendering:

```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath Unity-SyntheticDataGenrator \
  -runTests -testPlatform EditMode \
  -testResults /tmp/modular-pipeline-editmode.xml

/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath Unity-SyntheticDataGenrator \
  -runTests -testPlatform StandaloneOSX \
  -buildPlayerPath /tmp/modular-pipeline-playmode.app \
  -testResults /tmp/modular-pipeline-playmode.xml
```

After implementation, the new Inspector generates these existing Python workflow commands with actual unique names. The following illustrates a proposed new run, not an artifact that exists today:

```bash
uv run python -m pokemon_detector.cli.validate \
  data/generated/medical-review-001 --overlay-count 100 --seed 42
uv run python -m pokemon_detector.cli.split \
  data/generated/medical-review-001 data/prepared/medical-review-001 \
  --config configs/dataset/dataset-split.yaml
uv run python -m pokemon_detector.cli.train \
  --dataset data/prepared/medical-review-001/dataset.yaml \
  --config configs/training/train-generic-smoke.yaml \
  --run-name medical-smoke-001
uv run python -m pokemon_detector.cli.evaluate \
  --checkpoint runs/medical-smoke-001/weights/best.pt \
  --dataset data/prepared/medical-review-001/dataset.yaml --split test
```

## Completion definition and plan review

The project is modular only when a new class set can travel from Inspector authoring through generated labels, Python training/evaluation, and displayed inference names with no source edits. A polished Inspector over a fixed Python enum does not satisfy the requirement.

Coverage mapping: Task 1 defines compatibility and identity; Task 2 establishes assets, defaults and preparation; Task 3 provides camera/pose/profile flexibility and deterministic geometry; Task 4 supplies preview/capture/output reliability; Task 5 delivers the user workflow; Task 6 removes downstream coupling; Task 7 proves migration and a second category.

Known execution prerequisites: a functioning local Unity license and graphics path for capture tests, and at least one suitable non-Pokémon source model for a domain-representative demonstration. These do not block contract/authoring implementation or procedural integration fixtures. The scope excludes animation until chosen, mobile generalization, and long production training.
