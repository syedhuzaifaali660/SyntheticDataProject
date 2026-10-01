# Synthetic Data Generator: Unity Operations

This Unity 6000.3.8f1 project generates deterministic 640x640 RGB images, YOLO
labels, a JSON Lines manifest, and the resolved run configuration used by the
Python detector pipeline.

## Open and Inspect the Capture Scene

1. Open `Unity-SyntheticDataGenrator` in Unity 6000.3.8f1.
2. Open `Assets/SyntheticData/Scenes/PokemonCapture.unity`.
3. Confirm the scene contains the capture camera, background, lighting,
   `GeneratedObjects`, and capture controller objects.
4. Select the capture controller and inspect its references before generating.
5. Inspect `Assets/SyntheticData/Config/BaselineGeneration.asset` for the
   resolved generation ranges and validation limits.

The baseline asset uses seed 42 and 640x640 output. Background images and
Pokemon 3D models are local-only assets excluded from Git. Supply compatible
assets under `Assets/Data/Background/` and `Assets/Data/Models/` (including the
expected Pokemon model files) before capturing a run. A sampled `FrameRecipe`
contains all randomness needed for one frame: its derived seed, background,
Pokemon classes and transforms, camera field of view, and lighting values.
Scene objects must not sample additional randomness independently.

## Generate a Smoke Run in the Editor

1. Open **Synthetic Data > Capture Run**.
2. Set **Run ID** to a new identifier such as `smoke-20-local`.
3. Confirm **Output Root** points to the workspace's
   `Python-ModelTraining/data/generated` directory.
4. Set **Frame Count** to `20` and **Seed** to `42`.
5. Click **Generate**.

Unity enters Play Mode, captures the requested frames, and returns to Edit Mode.
The output is written beneath `<output-root>/<run-id>` and contains:

```text
<run-id>/
  images/
  labels/
  manifest.jsonl
  run-config.json
```

Every run ID must be a safe, single directory name. Separators, dot segments,
invalid filename characters, and paths that escape the output root are rejected.
Use a unique run ID: the generator rejects an existing run directory so prior
evidence cannot be overwritten accidentally.

## Generate a Smoke Run in Batch Mode

Run this command from the workspace root, changing the run ID when that output
already exists:

```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath Unity-SyntheticDataGenrator \
  -executeMethod SyntheticData.Editor.CaptureRunCommand.Run \
  -captureRunId smoke-20-local \
  -captureFrameCount 20 \
  -captureSeed 42 \
  -captureOutputRoot "$PWD/Python-ModelTraining/data/generated"
```

Do not add `-nographics` to a capture command. Camera capture without graphics
produces uniform gray PNGs on this project; the Python validator rejects them
with `UNIFORM_IMAGE`. The `-nographics` option is used only for automated Unity
tests below.

## Inspect Output and Rejections

During generation, each candidate object is projected to a screen-space box.
Frames can be rejected for invalid, too-small, excessively cropped, or otherwise
unacceptable boxes and are retried up to the configuration's
`MaximumFrameRetries`, currently 20.

Mixed-device runs also reject a frame when two projected boxes overlap at least
95% of the smaller box. This prevents labels for Pokemon that are effectively
hidden behind a larger foreground Pokemon while retaining ordinary partial
occlusion as useful training variation.

Rejections are retained in memory while the run is active. If all retries for a
frame are exhausted, Unity logs an exception containing the frame index, last
attempt, derived seed, and rejection reason. In the editor, inspect the Console.
In batch mode, retain standard output and error from the Unity process; the
command exits with status 1 on failure. The generator does not currently write a
separate rejection-log file.

After a successful smoke run, inspect `run-config.json` and `manifest.jsonl`,
then validate the output from `Python-ModelTraining`:

```bash
uv run python -m pokemon_detector.cli.validate \
  data/generated/smoke-20-local \
  --overlay-count 20 \
  --seed 42
```

Review every image in `validation-overlays/`; a machine-valid dataset is not
accepted until its visible Pokemon, class labels, boxes, textures, crops, and
negative frames have been checked.

## Run Unity Tests

Close the Unity editor first so each command gets a clean project launch. Run
these commands from the workspace root.

Edit Mode:

```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath Unity-SyntheticDataGenrator \
  -runTests -testPlatform EditMode \
  -testResults /tmp/pokemon-editmode.xml
```

Play Mode, using a standalone macOS test player so camera rendering uses the
same graphics path as generation:

```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath Unity-SyntheticDataGenrator \
  -runTests -testPlatform StandaloneOSX \
  -buildPlayerPath /tmp/pokemon-playmode-player.app \
  -testResults /tmp/pokemon-playmode.xml \
  -deviceLogs /tmp/pokemon-playmode-device-logs
```

Do not add `-quit` to Test Runner commands. Unity exits automatically after the
suite; with Unity 6000.3.8f1, `-quit` closes the editor before tests run. Do not
use `-nographics` for Play Mode because its capture smoke test renders a camera.
The in-editor command-line Play Mode transition can stall before test execution
in this project, while the standalone player route is verified. Both processes
must exit successfully and their XML reports must contain zero failed tests.
The Task 20 verification passed 77 Edit Mode and 11 Play Mode tests.

## Continue in Python

Use `Python-ModelTraining/README.md` from the workspace root for validation,
manual visual review, background-grouped splitting, training, evaluation, and
webcam operation.
