# Three-Pokemon Live Detector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reproducible Unity-to-Python pipeline that trains a three-class Pokémon detector and runs it against the MacBook webcam in real time.

**Architecture:** Unity 6.3 URP imports the supplied GLB models, creates deterministic randomized scenes, captures RGB frames, and writes YOLO labels plus a manifest. Python 3.12 validates and partitions those runs by background group, fine-tunes YOLO26n through Apple MPS, evaluates synthetic and real samples separately, and provides an OpenCV webcam application.

**Tech Stack:** Unity 6000.3.8f1, URP 17.3.0, C# edit/play-mode tests, Unity glTFast, Python 3.12 managed by `uv`, Ultralytics YOLO26n, PyTorch MPS, OpenCV, Pillow, PyYAML, NumPy, pytest, and Ruff.

**Spec:** `Plan/2026-08-29-pokemon-live-detector-design.md`

## Global Constraints

- Keep the Unity editor version at `6000.3.8f1` and URP at `17.3.0` during the first complete implementation.
- Use the fixed class order `0=pikachu`, `1=charmander`, `2=squirtle` everywhere.
- Preserve the supplied Pokémon colors and meshes; do not recolor or deform them.
- Use direct YOLO text annotations; do not introduce TFRecord, TensorFlow 1.x, or protocol buffers.
- Use deterministic seeds and record the resolved generation and training configuration for every run.
- Keep background files isolated by dataset split; no background may appear in more than one split.
- Require a passing dataset validation report before training.
- Keep synthetic metrics and real-world metrics separate.
- Begin with `yolo26n.pt`; change model size only after measured evidence.
- Target at least 15 FPS on the current Apple M4 Pro laptop.
- Do not download Pokémon images automatically; real-validation inputs are supplied manually by the user.
- Treat `Unity-SyntheticDataGenrator/Library`, generated datasets, training runs, and local virtual environments as disposable outputs.

---

## Chunk 1: Establish a Reproducible Project Baseline

### Task 1: Version control, ignores, and workspace documentation

**Files:**
- Create: `.gitignore`
- Create: `README.md`
- Create: `Python-ModelTraining/data/generated/.gitkeep`
- Create: `Python-ModelTraining/data/processed/.gitkeep`
- Create: `Python-ModelTraining/data/real_validation/.gitkeep`
- Create: `Python-ModelTraining/runs/.gitkeep`

**Interfaces:**
- Consumes: Existing Unity project and empty `Python-ModelTraining` directory.
- Produces: A clean repository boundary and documented entry points used by every later task.

- [ ] **Step 1: Initialize Git at the workspace root**

Run:

```bash
git init
git branch -M main
```

Expected: `git status --short` runs without “not a git repository.”

- [ ] **Step 2: Add ignores for generated state**

Create `.gitignore` with:

```gitignore
.DS_Store

# Unity generated state
Unity-SyntheticDataGenrator/Library/
Unity-SyntheticDataGenrator/Temp/
Unity-SyntheticDataGenrator/Logs/
Unity-SyntheticDataGenrator/obj/
Unity-SyntheticDataGenrator/UserSettings/
Unity-SyntheticDataGenrator/*.csproj
Unity-SyntheticDataGenrator/*.sln
Unity-SyntheticDataGenrator/*.slnx

# Python generated state
Python-ModelTraining/.venv/
Python-ModelTraining/.pytest_cache/
Python-ModelTraining/.ruff_cache/
Python-ModelTraining/**/__pycache__/
Python-ModelTraining/**/*.pyc
Python-ModelTraining/data/generated/*
!Python-ModelTraining/data/generated/.gitkeep
Python-ModelTraining/data/processed/*
!Python-ModelTraining/data/processed/.gitkeep
Python-ModelTraining/data/real_validation/*
!Python-ModelTraining/data/real_validation/.gitkeep
Python-ModelTraining/runs/*
!Python-ModelTraining/runs/.gitkeep
```

- [ ] **Step 3: Document the two application boundaries**

Create `README.md` containing:

```markdown
# Synthetic Pokémon Detector

This workspace contains two cooperating applications:

- `Unity-SyntheticDataGenrator`: generates labeled synthetic images.
- `Python-ModelTraining`: validates data, trains YOLO26n, evaluates results, and runs webcam inference.

Read `Plan/2026-08-29-pokemon-live-detector-design.md` for the approved design and `Plan/2026-08-29-pokemon-live-detector-implementation-plan.md` for execution steps.

Generated datasets and model runs are local artifacts and are not committed.
```

- [ ] **Step 4: Verify ignored files and retained inputs**

Run:

```bash
git check-ignore Unity-SyntheticDataGenrator/Library Python-ModelTraining/data/generated/sample.jpg
git check-ignore -q Unity-SyntheticDataGenrator/Assets/Data/Models/Pokemon/pikachu.glb; test $? -eq 1
```

Expected: generated paths are ignored and the supplied model is not ignored.

- [ ] **Step 5: Commit the project baseline**

```bash
git add .gitignore README.md Plan Python-ModelTraining/data Python-ModelTraining/runs
git commit -m "chore: establish pokemon detector project baseline"
```

**Chunk gate:** `git status --short` contains only pre-existing Unity files and supplied assets that have not yet been deliberately staged.

---

## Chunk 2: Make the Pokémon Assets Unity-Ready

### Task 2: Install glTFast and verify all GLB imports

**Files:**
- Modify: `Unity-SyntheticDataGenrator/Packages/manifest.json`
- Modify: `Unity-SyntheticDataGenrator/Packages/packages-lock.json`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/PokemonAssetImportTests.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/SyntheticData.EditModeTests.asmdef`

**Interfaces:**
- Consumes: `Assets/Data/Models/Pokemon/pikachu.glb`, `charmander.glb`, and `squirtle.glb`.
- Produces: Three editor-imported model assets loadable through `AssetDatabase`.

- [ ] **Step 1: Create the edit-mode test assembly**

Create `SyntheticData.EditModeTests.asmdef`:

```json
{
  "name": "SyntheticData.EditModeTests",
  "rootNamespace": "SyntheticData.Tests",
  "references": [],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": false
}
```

- [ ] **Step 2: Write the failing import test**

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SyntheticData.Tests
{
    public sealed class PokemonAssetImportTests
    {
        [TestCase("pikachu")]
        [TestCase("charmander")]
        [TestCase("squirtle")]
        public void GlbModelImportsAsGameObject(string modelName)
        {
            var path = $"Assets/Data/Models/Pokemon/{modelName}.glb";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(model, Is.Not.Null, $"GLB failed to import: {path}");
            Assert.That(model.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        }
    }
}
```

- [ ] **Step 3: Run the test and confirm the missing-import failure**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath Unity-SyntheticDataGenrator \
  -runTests -testPlatform EditMode \
  -testFilter SyntheticData.Tests.PokemonAssetImportTests \
  -testResults /tmp/pokemon-import-tests.xml
```

Expected: FAIL because Unity has no `.glb` importer.

- [ ] **Step 4: Install Unity glTFast by package name**

Open Unity Package Manager, choose **Add package by name**, enter `com.unity.cloud.gltfast`, and allow Unity 6000.3.8f1 to resolve the compatible package. Confirm `manifest.json` contains `com.unity.cloud.gltfast` and `packages-lock.json` contains its resolved version.

- [ ] **Step 5: Force a clean asset reimport and rerun tests**

Reimport the three GLB assets from the Unity Project window, then rerun the command from Step 3.

Expected: 3 tests PASS and all three assets expose at least one renderer.

- [ ] **Step 6: Commit the importer and regression test**

```bash
git add Unity-SyntheticDataGenrator/Packages Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests Unity-SyntheticDataGenrator/Assets/Data/Models/Pokemon/*.meta
git commit -m "build: import pokemon glb assets with glTFast"
```

### Task 3: Prepare unit-holder labeled prefabs

**Files:**
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/SyntheticData.Runtime.asmdef`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Domain/PokemonClass.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Domain/PokemonLabel.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Prefabs/Pikachu.prefab`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Prefabs/Charmander.prefab`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Prefabs/Squirtle.prefab`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/SyntheticData.EditModeTests.asmdef`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/PokemonPrefabTests.cs`

**Interfaces:**
- Produces: `PokemonLabel.ClassId`, `PokemonLabel.ClassName`, and three unit-holder prefabs used by frame generation.

- [ ] **Step 1: Create the runtime assembly and taxonomy enum**

```json
{
  "name": "SyntheticData.Runtime",
  "rootNamespace": "SyntheticData",
  "references": [],
  "autoReferenced": true
}
```

```csharp
namespace SyntheticData.Domain
{
    public enum PokemonClass
    {
        Pikachu = 0,
        Charmander = 1,
        Squirtle = 2
    }
}
```

- [ ] **Step 2: Write the failing prefab contract test**

Update the test assembly references to include `SyntheticData.Runtime`, then add:

```csharp
using NUnit.Framework;
using SyntheticData.Domain;
using UnityEditor;
using UnityEngine;

namespace SyntheticData.Tests
{
    public sealed class PokemonPrefabTests
    {
        [TestCase("Pikachu", PokemonClass.Pikachu)]
        [TestCase("Charmander", PokemonClass.Charmander)]
        [TestCase("Squirtle", PokemonClass.Squirtle)]
        public void PrefabHasStableLabelAndRenderers(string name, PokemonClass expected)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/SyntheticData/Prefabs/{name}.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<PokemonLabel>().PokemonClass, Is.EqualTo(expected));
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        }
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Expected: FAIL because prefabs and `PokemonLabel` do not exist.

- [ ] **Step 4: Implement the immutable label component**

```csharp
using UnityEngine;

namespace SyntheticData.Domain
{
    [DisallowMultipleComponent]
    public sealed class PokemonLabel : MonoBehaviour
    {
        [SerializeField] private PokemonClass pokemonClass;

        public PokemonClass PokemonClass => pokemonClass;
        public int ClassId => (int)pokemonClass;
        public string ClassName => pokemonClass.ToString().ToLowerInvariant();
    }
}
```

- [ ] **Step 5: Build unit-holder prefabs in the editor**

For each model, create an empty prefab root at origin, add `PokemonLabel`, and add a `ModelHolder` child whose local scale remains `(1,1,1)`. Place the imported GLB under that holder, preserve its model-specific scale, rotate it to face Unity `+Z`, and move the holder so the rendered bounds are centered on X/Z and grounded at Y=0. Save the three prefabs under `Assets/SyntheticData/Prefabs`.

- [ ] **Step 6: Inspect materials and bounds**

Open every prefab in Prefab Mode. Confirm textures are visible in URP, no renderer is disabled, no mesh bounds are zero, and the Scene gizmo shows the face pointing toward `+Z`.

- [ ] **Step 7: Rerun edit-mode tests**

Expected: all import and prefab tests PASS.

- [ ] **Step 8: Commit unit-holder prefabs**

```bash
git add Unity-SyntheticDataGenrator/Assets/SyntheticData
git commit -m "feat: add unit-holder labeled pokemon prefabs"
```

**Chunk gate:** A Unity scene can display all three correctly textured prefabs, and the six asset/prefab tests pass.

---

## Chunk 3: Define Deterministic Generation Contracts

### Task 4: Configuration, frame recipes, and deterministic sampling

**Files:**
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/GenerationConfig.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/FrameRecipe.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/IRandomSource.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/SeededRandomSource.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Generation/FrameRecipeSampler.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/FrameRecipeSamplerTests.cs`

**Interfaces:**
- Produces: `FrameRecipe FrameRecipeSampler.Sample(int frameIndex)`.
- `FrameRecipe` exposes seed, background index, object classes, transforms, camera FOV, and light values.

- [x] **Step 1: Write deterministic sampler tests**

```csharp
[Test]
public void SameSeedAndFrameProduceSameRecipe()
{
    var config = GenerationConfig.CreateTestDefault(seed: 42);
    var first = new FrameRecipeSampler(config).Sample(7);
    var second = new FrameRecipeSampler(config).Sample(7);
    Assert.That(second.ToJson(), Is.EqualTo(first.ToJson()));
}

[Test]
public void RecipeContainsAtMostThreeObjectsWithValidClassIds()
{
    var recipe = new FrameRecipeSampler(
        GenerationConfig.CreateTestDefault(seed: 42)).Sample(11);
    Assert.That(recipe.Objects.Count, Is.InRange(0, 3));
    Assert.That(recipe.Objects, Is.All.Matches<ObjectRecipe>(
        item => item.ClassId >= 0 && item.ClassId <= 2));
}
```

- [x] **Step 2: Run tests and confirm compile failure**

Expected: FAIL because generation contract types do not exist.

- [x] **Step 3: Implement configuration ranges**

`GenerationConfig` must serialize these defaults:

```csharp
public int Seed = 42;
public int Width = 640;
public int Height = 640;
public int MinimumObjects = 0;
public int MaximumObjects = 3;
public float MinimumFov = 40f;
public float MaximumFov = 75f;
public float MinimumDistance = 1.5f;
public float MaximumDistance = 5f;
public float MaximumYaw = 35f;
public float MaximumPitch = 20f;
public float MaximumRoll = 12f;
public float MinimumCenterSeparation = 0.18f;
public float MinimumBoxPixels = 24f;
public float MaximumCropFraction = 0.25f;
public int MaximumFrameRetries = 20;
```

Add `OnValidate()` assertions that minimums do not exceed maximums and image dimensions are positive.

- [x] **Step 4: Implement a seed-derived random source**

```csharp
public sealed class SeededRandomSource : IRandomSource
{
    private readonly System.Random random;

    public SeededRandomSource(int seed) => random = new System.Random(seed);
    public int Range(int minimum, int maximumExclusive) => random.Next(minimum, maximumExclusive);
    public float Range(float minimum, float maximum) =>
        minimum + (float)random.NextDouble() * (maximum - minimum);
}
```

Derive each frame seed from the global seed and frame index with a stable integer hash, rather than sharing Unity's global random state.

- [x] **Step 5: Implement recipe sampling**

Generate class IDs with a shuffled round-robin queue so class counts stay within one object of each other over complete three-object cycles. Keep normalized object centers at least `MinimumCenterSeparation` apart to prevent severe uncontrolled overlap. Store all sampled values in `FrameRecipe`; scene objects must never sample additional randomness independently.

- [x] **Step 6: Rerun sampler tests**

Expected: PASS on repeated test runs and after restarting Unity.

- [x] **Step 7: Commit deterministic generation contracts**

```bash
git add Unity-SyntheticDataGenrator/Assets/SyntheticData
git commit -m "feat: define deterministic synthetic frame recipes"
```

---

## Chunk 4: Produce Correct Screen-Space Boxes

### Task 5: Cached vertex projection and YOLO normalization

**Files:**
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Labels/PixelBounds.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Labels/YoloBox.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Labels/MeshVertexCache.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Labels/IBoundsProjector.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Labels/MeshBoundsProjector.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/YoloBoxTests.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/MeshBoundsProjectorTests.cs`

**Interfaces:**
- Produces: `bool TryProject(Camera camera, GameObject target, int width, int height, out PixelBounds bounds)`.
- Produces: `YoloBox PixelBounds.ToYolo(int classId, int width, int height)`.

- [x] **Step 1: Write normalization tests**

```csharp
[Test]
public void ConvertsPixelBoundsToNormalizedYoloBox()
{
    var bounds = new PixelBounds(160f, 160f, 480f, 480f);
    var box = bounds.ToYolo(classId: 2, width: 640, height: 640);
    Assert.That(box.ClassId, Is.EqualTo(2));
    Assert.That(box.CenterX, Is.EqualTo(0.5f).Within(0.0001f));
    Assert.That(box.CenterY, Is.EqualTo(0.5f).Within(0.0001f));
    Assert.That(box.Width, Is.EqualTo(0.5f).Within(0.0001f));
    Assert.That(box.Height, Is.EqualTo(0.5f).Within(0.0001f));
}

[Test]
public void ClampsPartiallyCroppedBoundsToImage()
{
    var clamped = new PixelBounds(-10f, 20f, 650f, 500f).Clamp(640, 640);
    Assert.That(clamped.MinX, Is.EqualTo(0f));
    Assert.That(clamped.MaxX, Is.EqualTo(640f));
}
```

- [x] **Step 2: Write a projection test with a procedural cube**

Create a camera at `(0,0,-5)` facing origin, create a Unity cube at origin, call `TryProject`, and assert the resulting box is non-empty, inside the image, and centered within 5% of `(0.5,0.5)`.

- [x] **Step 3: Run tests and confirm failure**

Expected: FAIL because projection types do not exist.

- [x] **Step 4: Implement cached local vertices**

`MeshVertexCache` must gather vertices from every `MeshFilter.sharedMesh` and baked vertices from `SkinnedMeshRenderer` once per prefab instance. Cache entries pair a renderer transform with its local vertex array; do not call `mesh.vertices` inside the capture loop.

- [x] **Step 5: Implement camera projection**

For every cached local vertex:

```csharp
var world = entry.Transform.TransformPoint(localVertex);
var screen = camera.WorldToScreenPoint(world);
if (screen.z <= camera.nearClipPlane) continue;
minX = Mathf.Min(minX, screen.x);
minY = Mathf.Min(minY, screen.y);
maxX = Mathf.Max(maxX, screen.x);
maxY = Mathf.Max(maxY, screen.y);
```

Return `false` when no vertex is in front of the near plane. Convert Unity's bottom-left screen origin consistently when writing overlays and labels.

- [x] **Step 6: Add box validity rules**

Reject boxes when clamped width or height is below `MinimumBoxPixels`, crop loss exceeds `MaximumCropFraction`, values are NaN/infinite, or normalized coordinates fall outside `[0,1]`.

- [x] **Step 7: Run projection and normalization tests**

Expected: PASS.

- [x] **Step 8: Commit projection code**

```bash
git add Unity-SyntheticDataGenrator/Assets/SyntheticData
git commit -m "feat: calculate cached screen-space pokemon boxes"
```

**Chunk gate:** Procedural-camera tests pass, and projected boxes are stable across two consecutive calls with unchanged transforms.

---

## Chunk 5: Build Scene Randomization and Capture Output

### Task 6: Background catalog and frame scene builder

**Files:**
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Scene/BackgroundCatalog.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Scene/PokemonPrefabCatalog.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Scene/FrameSceneBuilder.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Scene/SpawnedFrame.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/FrameSceneBuilderTests.cs`

**Interfaces:**
- Consumes: `FrameRecipe` and three prefab references.
- Produces: `SpawnedFrame Build(FrameRecipe recipe)` and `void Clear(SpawnedFrame frame)`.

- [x] **Step 1: Write a failing scene-builder test**

Create three primitive test prefabs in memory with distinct `PokemonLabel` values. Build a recipe containing class IDs `0,1,2`; assert the returned roots have matching labels and exact recipe transforms.

- [x] **Step 2: Implement serialized catalogs**

`PokemonPrefabCatalog.Get(PokemonClass)` must throw `InvalidOperationException` for missing or duplicate class entries. `BackgroundCatalog` must expose a stable sorted list of texture assets and a `BackgroundId` based on the asset filename without extension.

- [x] **Step 3: Implement frame construction**

Instantiate objects beneath a dedicated `GeneratedObjects` transform. Apply transforms, camera FOV, background material properties, and lighting only from `FrameRecipe`. Return every spawned root and its cached vertex data in `SpawnedFrame`.

- [x] **Step 4: Implement deterministic clearing**

Destroy only objects registered in `SpawnedFrame`; never call a broad scene search or delete user-authored scene objects.

- [x] **Step 5: Run edit-mode tests**

Expected: PASS, including a test that `Clear` removes all generated roots and preserves the camera/background/light objects.

- [x] **Step 6: Commit scene construction**

```bash
git add Unity-SyntheticDataGenrator/Assets/SyntheticData
git commit -m "feat: construct randomized pokemon capture scenes"
```

### Task 7: YOLO labels, manifest records, and atomic writes

**Files:**
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Output/YoloLabelFormatter.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Output/ManifestRecord.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Output/CapturePaths.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Output/AtomicCaptureWriter.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/OutputWriterTests.cs`

**Interfaces:**
- Produces: `string YoloLabelFormatter.Format(IReadOnlyList<YoloBox> boxes)`.
- Produces: `void AtomicCaptureWriter.Write(FrameArtifact artifact)`.
- Run layout: `<run>/images/frame_000001.png`, `<run>/labels/frame_000001.txt`, `<run>/manifest.jsonl`, `<run>/run-config.json`.

- [x] **Step 1: Write invariant-culture formatting tests**

```csharp
[Test]
public void FormatsOneYoloRecordWithSixDecimalPlaces()
{
    var text = YoloLabelFormatter.Format(new[] {
        new YoloBox(1, 0.5f, 0.25f, 0.125f, 0.75f)
    });
    Assert.That(text, Is.EqualTo("1 0.500000 0.250000 0.125000 0.750000\n"));
}
```

Set the current culture to `de-DE` in a second test and assert decimal points remain `.`.

- [x] **Step 2: Write atomic-pairing tests using a temporary directory**

Write a one-frame artifact and assert image, label, manifest, and config exist. Inject an image-write exception and assert neither final image nor final label exists.

- [x] **Step 3: Implement formatting and manifest DTOs**

Manifest JSON must include:

```json
{
  "run_id": "baseline-20260829-001",
  "frame_id": 1,
  "seed": 42,
  "background_id": "indoor_design_001",
  "width": 640,
  "height": 640,
  "objects": [{"class_id": 0, "class_name": "pikachu", "bbox": [0.5, 0.5, 0.2, 0.4]}]
}
```

- [x] **Step 4: Implement atomic writes**

Write `.tmp` image and label files in the destination directory, flush them, rename both to final names, then append one manifest line. On failure, delete every temporary file and any final image or label created by that call, while preserving files that existed before the call. Cleanup is best-effort and must not mask the original failure; rethrow with frame ID context.

- [x] **Step 5: Run output tests**

Expected: PASS on the default German timezone/locale and with explicit `de-DE` culture.

- [x] **Step 6: Commit output contracts**

```bash
git add Unity-SyntheticDataGenrator/Assets/SyntheticData
git commit -m "feat: write atomic YOLO captures and manifests"
```

### Task 8: Camera capture and orchestration

**Files:**
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Capture/ICameraCaptureService.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Capture/CameraCaptureService.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Capture/SyntheticCaptureController.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/PlayMode/SyntheticData.PlayModeTests.asmdef`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/PlayMode/CaptureSmokeTests.cs`

**Interfaces:**
- Produces: `byte[] CapturePng(Camera camera, int width, int height)`.
- Produces: `IEnumerator GenerateRun(string runId, int frameCount)`.

- [x] **Step 1: Write a camera capture test**

Render a red quad with a test camera at `64x64`, decode returned PNG bytes with `Texture2D.LoadImage`, and assert width, height, and center pixel red channel exceed `0.9`.

- [x] **Step 2: Implement isolated render-texture capture**

Save the camera's existing target texture and active render texture, assign a temporary `RenderTexture`, call `camera.Render()`, read pixels into a `Texture2D`, encode PNG, and restore both previous values in `finally`. Destroy temporary Unity objects after encoding.

- [x] **Step 3: Write a controller retry test**

Use fake scene builder, projector, capture service, and writer. Make the first projected frame invalid and the second valid. Assert one artifact is written, two recipes were attempted, and the rejection log records the first seed.

- [x] **Step 4: Implement the controller loop**

Map the zero-based `FrameRecipe.FrameIndex` to the one-based artifact/manifest ID with `frame_id = FrameIndex + 1`. The first generated recipe therefore writes `frame_000001.png`, `frame_000001.txt`, and manifest `frame_id: 1`; cover this boundary with an integration test.

For each desired frame:

```text
sample recipe -> build scene -> wait end of frame -> project boxes
-> reject or capture PNG -> atomic write -> clear generated scene
```

Stop the run with an actionable exception after `MaximumFrameRetries` for one frame. Always clear generated objects in `finally`.

- [x] **Step 5: Run edit-mode and play-mode tests**

Expected: all tests PASS and no temporary RenderTexture remains allocated after the capture test.

- [x] **Step 6: Commit capture orchestration**

```bash
git add Unity-SyntheticDataGenrator/Assets/SyntheticData
git commit -m "feat: orchestrate deterministic synthetic captures"
```

**Chunk gate:** A play-mode test produces a readable PNG and matching annotation without leaking scene objects or render textures.

---

## Chunk 6: Assemble and Visually Verify the Unity Generator

### Task 9: Capture scene, configuration asset, and overlay preview

**Files:**
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Scenes/PokemonCapture.unity`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Config/BaselineGeneration.asset`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Runtime/Validation/BoundsOverlay.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Editor/CaptureRunWindow.cs`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Editor/SyntheticData.Editor.asmdef`

**Interfaces:**
- Produces: An editor window accepting run ID, output root, frame count, and seed.
- Produces: A scene that can preview and generate the same recipe deterministically.

- [x] **Step 1: Create the capture scene hierarchy**

```text
PokemonCapture
├── CaptureCamera
├── BackgroundQuad
├── Lighting
│   ├── AmbientController
│   └── PointLight
├── GeneratedObjects
└── SyntheticCaptureController
```

Disable the template directional light. Assign the prepared prefabs and the 98 background textures to their catalogs.

- [x] **Step 2: Create baseline configuration**

Use `640x640`, seed `42`, zero-to-three objects, FOV `40-75`, yaw `±35`, pitch `±20`, roll `±12`, minimum box side `24px`, maximum crop `25%`, and maximum retries `20`.

- [x] **Step 3: Implement an editor-only bounds overlay**

Draw each projected box in Game view with class-specific colors: yellow for Pikachu, orange for Charmander, and blue for Squirtle. Display class name and normalized box values. The overlay is excluded from captured RGB by rendering after capture or through editor GUI only.

- [x] **Step 4: Implement the capture window validation**

Disable **Generate** unless the scene, config, three prefabs, at least one background, output root, non-empty run ID, and positive frame count are present. Reject a run ID containing path separators.

- [x] **Step 5: Generate the 20-frame technical smoke run**

Output to:

```text
Python-ModelTraining/data/generated/smoke-20/
```

Expected: 20 PNG files, 20 TXT files, 20 manifest lines, and one `run-config.json`.

- [x] **Step 6: Manually inspect all 20 overlays**

Confirm every box covers the correct visible Pokémon, labels match identity, empty frames contain empty label files, and no object is rendered pink, black, or textureless.

- [x] **Step 7: Run the complete Unity test suite**

```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath Unity-SyntheticDataGenrator \
  -runTests -testPlatform EditMode \
  -testResults /tmp/pokemon-editmode.xml

/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath Unity-SyntheticDataGenrator \
  -runTests -testPlatform PlayMode \
  -testResults /tmp/pokemon-playmode.xml
```

Expected: zero failed tests.

- [x] **Step 8: Commit the working Unity generator**

```bash
git add Unity-SyntheticDataGenrator/Assets/SyntheticData Unity-SyntheticDataGenrator/Packages
git commit -m "feat: deliver smoke-tested Unity pokemon generator"
```

**Chunk gate:** The 20-frame smoke run passes visual inspection and all Unity tests.

---

## Chunk 7: Create the Python Project and Dataset Quality Gate

### Task 10: Python 3.12 package scaffold and shared models

**Files:**
- Create: `Python-ModelTraining/pyproject.toml`
- Create: `Python-ModelTraining/.python-version`
- Create: `Python-ModelTraining/src/pokemon_detector/__init__.py`
- Create: `Python-ModelTraining/src/pokemon_detector/domain.py`
- Create: `Python-ModelTraining/configs/classes.yaml`
- Create: `Python-ModelTraining/tests/test_domain.py`
- Generate: `Python-ModelTraining/uv.lock`

**Interfaces:**
- Produces: `PokemonClass`, `YoloBox`, `ManifestObject`, and `ManifestRecord` dataclasses.
- Produces: importable Python domain types used by the module-based CLI commands in later tasks.

- [x] **Step 1: Create the project metadata**

```toml
[project]
name = "pokemon-detector"
version = "0.1.0"
requires-python = ">=3.12,<3.13"
dependencies = [
  "numpy>=2,<3",
  "opencv-python>=4.10,<5",
  "pillow>=11,<13",
  "pyyaml>=6,<7",
  "rich>=13,<15",
  "ultralytics>=8,<9"
]

[dependency-groups]
dev = ["pytest>=8,<10", "pytest-cov>=6,<8", "ruff>=0.12,<1"]

[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"

[tool.pytest.ini_options]
testpaths = ["tests"]
markers = ["slow: local training or full image integration tests"]

[tool.ruff]
line-length = 100
target-version = "py312"
```

- [x] **Step 2: Pin Python and create the environment**

```bash
cd Python-ModelTraining
uv python install 3.12
uv sync --dev
```

Expected: `uv run python --version` reports Python 3.12.x and `uv.lock` exists.

- [x] **Step 3: Write taxonomy and YOLO-box tests**

```python
def test_class_ids_are_stable() -> None:
    assert PokemonClass.PIKACHU.value == 0
    assert PokemonClass.CHARMANDER.value == 1
    assert PokemonClass.SQUIRTLE.value == 2


def test_yolo_box_rejects_out_of_range_coordinates() -> None:
    with pytest.raises(ValueError, match="between 0 and 1"):
        YoloBox(class_id=0, center_x=1.2, center_y=0.5, width=0.2, height=0.2)
```

- [x] **Step 4: Run tests and confirm import failure**

```bash
uv run pytest tests/test_domain.py -v
```

Expected: FAIL because `pokemon_detector.domain` does not exist.

- [x] **Step 5: Implement immutable dataclasses**

Use `enum.IntEnum` and frozen dataclasses. Validate class IDs, finite floats, `[0,1]` bounds, positive width/height, and box edges that remain inside the normalized image.

- [x] **Step 6: Add the shared class configuration**

```yaml
names:
  0: pikachu
  1: charmander
  2: squirtle
```

- [x] **Step 7: Run tests and lint**

```bash
uv run pytest -q
uv run ruff check .
```

Expected: PASS.

- [x] **Step 8: Commit the Python baseline**

```bash
git add Python-ModelTraining
git commit -m "build: scaffold reproducible python training project"
```

### Task 11: Dataset parser, validator, and overlay report

**Files:**
- Create: `Python-ModelTraining/src/pokemon_detector/dataset/__init__.py`
- Create: `Python-ModelTraining/src/pokemon_detector/dataset/parser.py`
- Create: `Python-ModelTraining/src/pokemon_detector/dataset/validator.py`
- Create: `Python-ModelTraining/src/pokemon_detector/dataset/overlay.py`
- Create: `Python-ModelTraining/src/pokemon_detector/cli/validate.py`
- Create: `Python-ModelTraining/tests/dataset/test_parser.py`
- Create: `Python-ModelTraining/tests/dataset/test_validator.py`
- Create: `Python-ModelTraining/tests/dataset/test_overlay.py`

**Interfaces:**
- Produces: `parse_yolo_label(path: Path) -> list[YoloBox]`.
- Produces: `validate_run(run_dir: Path) -> ValidationReport`.
- Produces: `render_overlays(run_dir: Path, output_dir: Path, limit: int, seed: int) -> list[Path]`.

- [x] **Step 1: Create parser tests for valid and invalid rows**

Cover empty negative labels, six decimal values, unknown class `3`, missing columns, non-numeric values, zero-area boxes, and boxes extending outside the image.

- [x] **Step 2: Create a validator fixture**

In `tmp_path`, create two 32x32 PNGs, two label files, two manifest lines, and a run config. Assert a valid fixture returns zero errors and exact class counts.

- [x] **Step 3: Add failing structural tests**

Assert error codes for `MISSING_LABEL`, `ORPHAN_LABEL`, `UNREADABLE_IMAGE`, `MANIFEST_FRAME_MISSING`, `INVALID_CLASS_ID`, and `INVALID_BOX`.

- [x] **Step 4: Run tests and confirm failure**

```bash
uv run pytest tests/dataset -v
```

- [x] **Step 5: Implement strict parsing and validation**

Return a report shaped as:

```python
@dataclass(frozen=True)
class ValidationReport:
    run_id: str
    image_count: int
    label_count: int
    object_count_by_class: dict[int, int]
    negative_image_count: int
    errors: tuple[ValidationIssue, ...]
    warnings: tuple[ValidationIssue, ...]

    @property
    def passed(self) -> bool:
        return not self.errors
```

The CLI writes `validation-report.json` and exits `0` only when `passed` is true.

- [x] **Step 6: Implement overlay rendering**

Use Pillow to draw class-colored rectangles and `class_name center_x center_y width height`. Select samples with `random.Random(seed)` and save them beneath `<run>/validation-overlays`.

- [x] **Step 7: Validate the Unity smoke run**

```bash
uv run python -m pokemon_detector.cli.validate \
  data/generated/smoke-20 \
  --overlay-count 20
```

Expected: exit `0`, zero errors, 20 overlay images.

- [x] **Step 8: Review overlays and commit**

After visual approval:

```bash
git add Python-ModelTraining
git commit -m "feat: validate synthetic YOLO datasets and overlays"
```

**Chunk gate:** The Unity smoke dataset passes automated validation and all 20 overlay images are visually correct.

---

## Chunk 8: Build Leakage-Safe Dataset Partitions

### Task 12: Background-grouped deterministic splitter

**Files:**
- Create: `Python-ModelTraining/src/pokemon_detector/dataset/splitter.py`
- Create: `Python-ModelTraining/src/pokemon_detector/cli/split.py`
- Create: `Python-ModelTraining/tests/dataset/test_splitter.py`
- Create: `Python-ModelTraining/configs/dataset-split.yaml`

**Interfaces:**
- Produces: `split_run(run_dir: Path, output_dir: Path, config: SplitConfig) -> SplitReport`.
- Output: `images/{train,val,test}`, `labels/{train,val,test}`, `dataset.yaml`, and `split-report.json`.

- [x] **Step 1: Write a leakage test**

Create a fixture with ten backgrounds and multiple frames per background. Split it and assert each background appears in exactly one of train, validation, or test.

- [x] **Step 2: Write reproducibility and ratio tests**

Assert seed `42` produces identical assignments on repeated calls. For 100 backgrounds, assert exact counts `70/20/10`.

- [x] **Step 3: Run tests and confirm failure**

```bash
uv run pytest tests/dataset/test_splitter.py -v
```

- [x] **Step 4: Implement grouped assignment**

Collect unique `background_id` values from the manifest, sort them, shuffle with `random.Random(seed)`, and allocate whole background groups using ratios `0.70/0.20/0.10`. Copy image-label pairs with `shutil.copy2`; never split a pair.

- [x] **Step 5: Write Ultralytics dataset configuration**

```yaml
path: /absolute/path/to/data/processed/baseline
train: images/train
val: images/val
test: images/test
names:
  0: pikachu
  1: charmander
  2: squirtle
```

The generated `path` must be resolved from the actual output directory, not hard-coded to a developer username.

- [x] **Step 6: Add refusal conditions**

Exit non-zero when input validation failed, fewer than ten distinct backgrounds exist, any source file is missing, or any background would cross splits.

- [x] **Step 7: Run tests and lint**

```bash
uv run pytest tests/dataset/test_splitter.py -v
uv run ruff check .
```

- [x] **Step 8: Commit grouped splitting**

```bash
git add Python-ModelTraining
git commit -m "feat: create background-isolated dataset splits"
```

**Chunk gate:** The split report proves zero shared background IDs between partitions.

---

## Chunk 9: Add Gated YOLO26n Training

### Task 13: Training configuration and runner

**Files:**
- Create: `Python-ModelTraining/configs/train-smoke.yaml`
- Create: `Python-ModelTraining/configs/train-baseline.yaml`
- Create: `Python-ModelTraining/src/pokemon_detector/training/config.py`
- Create: `Python-ModelTraining/src/pokemon_detector/training/runner.py`
- Create: `Python-ModelTraining/src/pokemon_detector/cli/train.py`
- Create: `Python-ModelTraining/tests/training/test_config.py`
- Create: `Python-ModelTraining/tests/training/test_runner.py`

**Interfaces:**
- Produces: `TrainingConfig.load(path: Path) -> TrainingConfig`.
- Produces: `Path train(config: TrainingConfig, yolo_factory: Callable = YOLO)`, returning the best checkpoint.

- [x] **Step 1: Define smoke and baseline configurations**

`train-smoke.yaml`:

```yaml
model: yolo26n.pt
epochs: 5
patience: 5
image_size: 640
batch: 8
workers: 2
device: mps
seed: 42
```

`train-baseline.yaml`:

```yaml
model: yolo26n.pt
epochs: 100
patience: 20
image_size: 640
batch: 16
workers: 4
device: mps
seed: 42
```

- [x] **Step 2: Write configuration validation tests**

Reject a non-YOLO26 model, epochs below `1`, patience above epochs, unsupported device, non-positive image size, and missing dataset path.

- [x] **Step 3: Write a mocked-runner test**

Use a fake YOLO object that records `.train()` arguments and creates `weights/best.pt`. Assert the runner passes exact configuration values and writes `resolved-config.yaml` plus `environment.json`.

- [x] **Step 4: Write a validation-gate test**

Point the runner at a dataset whose `validation-report.json` contains an error. Assert training raises `DatasetNotValidatedError` before constructing the YOLO model.

- [x] **Step 5: Implement the training runner**

Call:

```python
model = yolo_factory(config.model)
result = model.train(
    data=str(config.dataset_yaml),
    epochs=config.epochs,
    patience=config.patience,
    imgsz=config.image_size,
    batch=config.batch,
    workers=config.workers,
    device=config.device,
    seed=config.seed,
    deterministic=True,
    project=str(config.runs_dir),
    name=config.run_name,
)
```

Capture Python, platform, PyTorch, Ultralytics, and MPS availability in `environment.json`.

- [x] **Step 6: Run unit tests**

```bash
uv run pytest tests/training -v
```

Expected: PASS without downloading weights because the runner test uses a fake factory.

- [x] **Step 7: Commit the gated trainer**

```bash
git add Python-ModelTraining
git commit -m "feat: add validated YOLO26n training runner"
```

**Chunk gate:** Unit tests prove invalid datasets cannot start training and resolved configuration is saved for valid runs.

---

## Chunk 10: Measure Synthetic and Real Performance

### Task 14: Real-image annotation, evaluation reports, and failure gallery

**Files:**
- Create: `Python-ModelTraining/src/pokemon_detector/evaluation/metrics.py`
- Create: `Python-ModelTraining/src/pokemon_detector/evaluation/evaluator.py`
- Create: `Python-ModelTraining/src/pokemon_detector/evaluation/gallery.py`
- Create: `Python-ModelTraining/src/pokemon_detector/evaluation/annotation.py`
- Create: `Python-ModelTraining/src/pokemon_detector/cli/annotate.py`
- Create: `Python-ModelTraining/src/pokemon_detector/cli/evaluate.py`
- Create: `Python-ModelTraining/tests/evaluation/test_annotation.py`
- Create: `Python-ModelTraining/tests/evaluation/test_metrics.py`
- Create: `Python-ModelTraining/tests/evaluation/test_evaluator.py`

**Interfaces:**
- Produces: `EvaluationReport evaluate_dataset(checkpoint: Path, dataset_yaml: Path, split: str)`.
- Produces: `YoloBox pixel_rect_to_yolo(class_id: int, rect: PixelRect, image_size: tuple[int, int])`.
- Produces: separate `synthetic-report.json` and `real-report.json`.

- [x] **Step 1: Write per-class metric tests**

Given fixed true/predicted class records, assert precision, recall, F1, false positives, and false negatives for all three class IDs. Include a class with zero predictions and define its precision as `0.0` rather than NaN.

- [x] **Step 2: Write annotation conversion tests**

Convert a pixel rectangle `(160,160)-(480,480)` on a `640x640` image and assert normalized center `(0.5,0.5)` and size `(0.5,0.5)`. Reject zero-area rectangles and class IDs outside `0-2`.

- [x] **Step 3: Implement the real-image annotation CLI**

Use OpenCV to show one image at a time. Mouse drag defines a rectangle; keys `0`, `1`, and `2` assign Pikachu, Charmander, and Squirtle; `s` saves; `n` and `p` navigate; `u` removes the last box; `q` exits. Save one YOLO TXT file beside each image and never overwrite a non-empty label without the user pressing `s`.

- [x] **Step 4: Write report-separation tests**

Assert the evaluator refuses to label a report `real` when its dataset path is beneath `data/processed`, and refuses to label it `synthetic` when beneath `data/real_validation`.

- [x] **Step 5: Implement Ultralytics validation adapter**

Call `YOLO(checkpoint).val(data=..., split=...)`, extract overall and per-class box metrics, and serialize plain JSON values. Save confusion-matrix artifacts when Ultralytics produces them.

- [x] **Step 6: Implement the failure gallery**

Run predictions on evaluation images, rank false negatives and lowest-confidence correct detections, and save at most 50 annotated images plus `failures.json`. Never select training images for the real gallery.

- [x] **Step 7: Run evaluation tests**

```bash
uv run pytest tests/evaluation -v
```

- [x] **Step 8: Commit evaluation tooling**

```bash
git add Python-ModelTraining
git commit -m "feat: report synthetic and real detector metrics"
```

**Chunk gate:** Synthetic and real results have distinct files, dataset provenance, and per-class metrics.

---

## Chunk 11: Deliver Real-Time Webcam Inference

### Task 15: Testable webcam application

**Files:**
- Create: `Python-ModelTraining/src/pokemon_detector/inference/frame_source.py`
- Create: `Python-ModelTraining/src/pokemon_detector/inference/detector.py`
- Create: `Python-ModelTraining/src/pokemon_detector/inference/fps.py`
- Create: `Python-ModelTraining/src/pokemon_detector/inference/webcam.py`
- Create: `Python-ModelTraining/src/pokemon_detector/cli/webcam.py`
- Create: `Python-ModelTraining/configs/webcam.yaml`
- Create: `Python-ModelTraining/tests/inference/test_fps.py`
- Create: `Python-ModelTraining/tests/inference/test_webcam.py`

**Interfaces:**
- Produces: `OpenCVFrameSource(camera_index: int)` with `read()` and `close()`.
- Produces: `UltralyticsDetector(checkpoint: Path, confidence: float, device: str)` with `predict(frame)`.
- Produces: `run_webcam(config: WebcamConfig, source_factory, detector_factory) -> int`.

- [x] **Step 1: Define webcam defaults**

```yaml
checkpoint: runs/baseline-3900/weights/best.pt
camera_index: 0
confidence: 0.50
image_size: 640
device: mps
save_low_confidence: false
low_confidence_directory: runs/webcam-review
```

- [x] **Step 2: Write rolling-FPS tests with a fake clock**

Feed timestamps `0.0, 0.05, 0.10, 0.15` and assert rolling FPS is approximately `20`. Assert the first frame returns `0` without division by zero.

- [x] **Step 3: Write a webcam-loop test with fakes**

Use three NumPy frames followed by end-of-stream, a fake detector returning one Pikachu box, and a fake display. Assert three predictions, three displayed frames, one source close, and exit code `0`.

- [x] **Step 4: Write failure tests**

Assert missing checkpoint exits with code `2`, unavailable camera exits with code `3`, and inference exception closes the camera before returning code `4`.

- [x] **Step 5: Implement frame source and detector adapters**

Open `cv2.VideoCapture(camera_index)`, set requested dimensions when supported, and raise `CameraUnavailableError` when `isOpened()` is false. Use `YOLO(checkpoint).predict(frame, conf=..., imgsz=..., device=..., verbose=False)`.

- [x] **Step 6: Implement display and controls**

Draw rectangles, class names, confidence, rolling FPS, and inference milliseconds. Exit on `q` or Escape. Save low-confidence frames only when the configuration flag is true; include timestamp and predicted class in filenames.

- [x] **Step 7: Run automated tests**

```bash
uv run pytest tests/inference -v
uv run ruff check .
```

- [ ] **Step 8: Run a manual camera smoke test using a pretrained checkpoint**

Deferred until the staged training tasks produce the project `best.pt` checkpoint. The
automated camera, detector, display, and cleanup boundaries are complete; this checkbox
remains open until the real camera and project weights are available together.

After a project checkpoint exists:

```bash
uv run python -m pokemon_detector.cli.webcam --config configs/webcam.yaml
```

Expected: camera opens, overlay updates, `q` exits cleanly, and terminal returns to the prompt.

- [x] **Step 9: Commit webcam inference**

```bash
git add Python-ModelTraining
git commit -m "feat: add real-time pokemon webcam inference"
```

**Chunk gate:** Fake-camera tests pass and the real webcam opens and closes cleanly.

---

## Chunk 12: Execute the Staged Data and Training Gates

### Task 16: Generate and prove the 100-image visual-review dataset

**Files:**
- Generated: `Python-ModelTraining/data/generated/visual-review-100/`
- Generated: validation report and 100 overlays.

- [x] **Step 1: Generate exactly 100 frames with seed 42**

Use the Unity capture window and the approved baseline configuration.

- [x] **Step 2: Run validation and render all overlays**

```bash
cd Python-ModelTraining
uv run python -m pokemon_detector.cli.validate \
  data/generated/visual-review-100 --overlay-count 100 --seed 42
```

- [x] **Step 3: Review every overlay**

Record counts for wrong class, loose box, clipped box, invisible object, texture failure, and implausible scene. Gate passes only with zero wrong classes and zero boxes missing their object.

- [x] **Step 4: Correct Unity configuration or code when the gate fails**

Change one identified cause, rerun its targeted Unity tests, regenerate the 100-frame run under a new run ID, and repeat validation. Do not overwrite the failed run.

- [x] **Step 5: Save the accepted run ID in the project README**

Commit only the run ID and summary; generated images remain ignored.

### Task 17: Prove the 300-image training pilot

**Files:**
- Generated: `Python-ModelTraining/data/generated/training-pilot-300/`
- Generated: `Python-ModelTraining/data/processed/training-pilot-300/`
- Generated: `Python-ModelTraining/runs/training-pilot-300/`

- [x] **Step 1: Generate 300 validated frames**

Use at least 30 distinct backgrounds and seed `42`.

Accepted run: `training-pilot-300-v2`. The failed `training-pilot-300` null-render
run remains preserved.

- [x] **Step 2: Validate and split**

```bash
uv run python -m pokemon_detector.cli.validate data/generated/training-pilot-300 --overlay-count 50
uv run python -m pokemon_detector.cli.split \
  data/generated/training-pilot-300 \
  data/processed/training-pilot-300 \
  --config configs/dataset-split.yaml
```

Expected: both commands exit `0`; split report shows no background leakage.

- [x] **Step 3: Overfit a 20-image subset**

Create a training-only fixture from 20 images and run up to 100 epochs. Gate passes when training loss falls substantially and the model predicts those same 20 images with near-perfect recall. Failure means labels, class mapping, or training configuration must be fixed before proceeding.

- [x] **Step 4: Run the five-epoch smoke training**

```bash
uv run python -m pokemon_detector.cli.train \
  --dataset data/processed/training-pilot-300/dataset.yaml \
  --config configs/train-smoke.yaml \
  --run-name training-pilot-300
```

Expected: best checkpoint, resolved configuration, environment report, and metrics exist.

- [x] **Step 5: Run synthetic evaluation**

Evaluate validation and test splits. Gate passes when the command completes, metrics contain all three classes, and predictions are not empty for every class.

- [x] **Step 6: Commit pilot conclusions**

Add a concise measured-results section to `README.md` and commit it; do not commit weights or generated images.

### Task 18: Generate and train the first baseline

**Files:**
- Generated: baseline dataset, split, checkpoint, metrics, and galleries.
- Modify: `README.md`

- [x] **Step 1: Generate approximately 3,900 images**

Target about 3,000 train, 600 validation, and 300 test after grouped splitting. Ensure every supplied background is assigned to exactly one partition.

- [x] **Step 2: Validate before splitting**

Require zero structural errors, class object counts within 10% of each other, at least 10% negative frames, and visual approval of 100 overlays.

- [x] **Step 3: Create background-grouped splits**

Run the splitter and archive `split-report.json` with the training run.

- [x] **Step 4: Train the baseline**

```bash
uv run python -m pokemon_detector.cli.train \
  --dataset data/processed/baseline-3900/dataset.yaml \
  --config configs/train-baseline.yaml \
  --run-name baseline-3900
```

Allow early stopping; do not force all 100 epochs when validation stops improving.

- [x] **Step 5: Evaluate the synthetic test set**

Save per-class precision, recall, mAP50, mAP50-95, confusion matrix, and the 50-image failure gallery.

- [x] **Step 6: Record the baseline decision**

Document whether each class is limited by missed detections, confusion, scale, rotation, cropping, background, or false positives. Expand the dataset only to target a measured limitation.

**Chunk gate:** A reproducible best checkpoint and synthetic test report exist for all three classes.

---

## Chunk 13: Validate the Actual Webcam Use Case

### Task 19: Build the real-world validation set and run controlled trials

**Files:**
- User-supplied: `Python-ModelTraining/data/real_validation/images/`
- Create: `Python-ModelTraining/data/real_validation/README.md`
- Generated: real labels, dataset YAML, report, and failure gallery.

- [ ] **Step 1: Capture real-validation material**

Collect at least 30 examples per class and 30 negative images through the webcam. Use cards, prints, posters, books, or a second screen. Vary distance, angle, scale, lighting, and background. Keep source attribution or ownership notes in the real-validation README.

- [ ] **Step 2: Divide real validation from real test before tuning**

Assign 20 class examples to real validation and 10 to real test for each class. Keep all captures from one physical setup/session in one partition to reduce leakage.

- [ ] **Step 3: Label real images with the project annotation tool**

```bash
cd Python-ModelTraining
uv run python -m pokemon_detector.cli.annotate data/real_validation/images
uv run python -m pokemon_detector.cli.validate data/real_validation --overlay-count 120
```

Run the same Python validator and overlay renderer used for synthetic data. Gate passes only after every real overlay is reviewed.

- [ ] **Step 4: Evaluate the untouched baseline checkpoint**

Produce `real-report.json` and a real failure gallery without changing thresholds per class.

- [ ] **Step 5: Run ten controlled live trials per class**

For each trial, show the target at a new distance/angle/background for five seconds. Record whether the correct class remains above confidence `0.50`, the median confidence, incorrect class detections, and FPS.

- [ ] **Step 6: Run the negative live trial**

Point the camera around the normal workspace for two minutes with no target. Record any detection lasting five consecutive frames.

- [ ] **Step 7: Apply the evidence-based fallback only when needed**

If a class scores below 8/10 trials, first generate targeted synthetic views matching its failure gallery. If the second synthetic experiment still fails, fine-tune with the real-validation partition while keeping the real-test partition untouched.

- [ ] **Step 8: Re-evaluate against the untouched real test set**

Accept real fine-tuning only when it improves the failing class without materially reducing the other two classes or increasing persistent false positives.

- [ ] **Step 9: Commit the final measured results and configuration**

```bash
git add README.md Python-ModelTraining/configs Python-ModelTraining/data/real_validation/README.md
git commit -m "docs: record real-world pokemon detector evaluation"
```

**Chunk gate:** The webcam reaches at least 15 FPS, each class passes at least 8/10 controlled trials, and the negative test has no persistent false detection.

---

## Chunk 14: Final Reproducibility and Handoff

### Task 20: Full verification and operating guide

**Files:**
- Create: `Python-ModelTraining/README.md`
- Create: `Unity-SyntheticDataGenrator/Assets/SyntheticData/README.md`
- Modify: root `README.md`

**Interfaces:**
- Produces: Exact commands for generation, validation, splitting, training, evaluation, and webcam inference.

- [x] **Step 1: Document Unity operation**

Explain how to open `PokemonCapture.unity`, inspect a recipe, select the generation configuration, choose a unique run ID, set output to `Python-ModelTraining/data/generated`, run smoke generation, and interpret rejection logs.

- [x] **Step 2: Document Python operation**

Include exact `uv sync`, validation, split, train, evaluate, and webcam commands used by the accepted baseline.

- [x] **Step 3: Run all fast Python checks**

```bash
cd Python-ModelTraining
uv run ruff check .
uv run pytest -m "not slow" --cov=pokemon_detector --cov-report=term-missing
```

Expected: lint passes and all fast tests pass.

- [x] **Step 4: Run all Unity tests from a clean editor launch**

Run the edit-mode and play-mode commands from Task 9. Expected: zero failures.

- [x] **Step 5: Re-run the accepted dataset validation**

Expected: zero errors and the stored split report still shows no shared backgrounds.

- [ ] **Step 6: Re-run evaluation from the saved checkpoint**

Expected: synthetic and real reports regenerate without modifying source data and remain within normal floating-point tolerance of stored results.

- [ ] **Step 7: Run the final webcam demonstration**

Confirm camera opens, all three classes can be demonstrated, FPS is visible, low-confidence capture obeys configuration, and `q` exits cleanly.

- [x] **Step 8: Scan the repository for accidental outputs and secrets**

```bash
git status --short
git ls-files | rg 'Library/|\.venv/|data/generated/|data/processed/|runs/.+\.pt$'
rg -n --hidden -g '!*.meta' -g '!Plan/**' '(api[_-]?key|secret|password)\s*[:=]' .
```

Expected: generated state and checkpoints are not tracked; no credentials are present.

- [x] **Step 9: Commit final documentation**

```bash
git add README.md Python-ModelTraining/README.md Unity-SyntheticDataGenrator/Assets/SyntheticData/README.md
git commit -m "docs: add pokemon detector operating guide"
```

- [ ] **Step 10: Record the final verification evidence**

Append the exact Unity test counts, Python test counts, accepted dataset run ID, checkpoint path, synthetic metrics, real metrics, and measured webcam FPS to the root README.

**Final gate:** A new developer with the supplied `Assets/Data` inputs can follow the READMEs, regenerate a smoke dataset, run validation and tests, locate the accepted checkpoint, and start webcam inference without consulting chat history.
