# iOS Two-Model Selector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Embed the baseline and mixed-device Core ML models and switch between them from two buttons during a live iOS camera session.

**Architecture:** A small `DetectorModel` value defines stable UI and bundle identities. `PokemonDetector` loads either compiled resource through the generic Core ML API, while `CameraManager` serializes and caches model swaps on its existing frame queue. SwiftUI renders two mutually exclusive buttons and the active model beside FPS.

**Tech Stack:** Swift 5, SwiftUI, AVFoundation, Core ML, iOS 17, Ultralytics 8.4/Core ML Tools 9

**Spec:** `Plan/2026-09-08-ios-two-model-selector-design.md`

## Global Constraints

- Preserve both `.pt` checkpoints and the existing `best.mlpackage`.
- Use a fixed 640 × 640 input for both models.
- Launch with `Mixed Device 6000` selected.
- Switch models without restarting the camera session.
- Serialize detector swaps with live inference and cache each loaded detector.
- Do not claim physical-device promotion without a physical-device comparison.

---

### Task 1: Stable model identities

**Files:**
- Create: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/DetectorModel.swift`
- Create: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/DetectorSelectionState.swift`
- Test: `Xcode-PokemonDetector/PokemonDetector/Tests/DetectorModelTests.swift`

**Interfaces:**
- Produces: `enum DetectorModel: String, CaseIterable, Hashable`
- Produces: `DetectorModel.defaultSelection`, `displayName`, `shortName`, and `resourceName`
- Produces: `DetectorSelectionState` with distinct active/loading state and retryable failures

- [x] **Step 1: Write the failing standalone test**

Assert that exactly `.baseline3900` and `.mixedDevice6000` exist, bundle names are
`best` and `MixedDevice6000`, display names are unique, and the default is
`.mixedDevice6000`.

- [x] **Step 2: Run the test to verify it fails**

Run:
`swiftc PokemonDetector/DetectorModel.swift Tests/DetectorModelTests.swift -o /tmp/detector-model-tests`

Expected: FAIL because `DetectorModel.swift` does not exist.

- [x] **Step 3: Implement the model enum**

Create the two cases and computed properties required by the approved UI and Core
ML bundle lookup.

- [x] **Step 4: Run the standalone test**

Run the compiled executable and expect `DetectorModelTests passed`.

### Task 2: Preserve and export both Core ML packages

**Files:**
- Preserve: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/best.mlpackage`
- Create: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/MixedDevice6000.mlpackage`

**Interfaces:**
- Consumes: both retained PyTorch `best.pt` checkpoints
- Produces: Core ML image input and one `[1, 300, 6]` multi-array output per model

- [x] **Step 1: Record baseline checksums**

Hash the baseline package and both checkpoints before export.

- [x] **Step 2: Export the new checkpoint separately**

Run Ultralytics Core ML export with `imgsz=640` and `nms=False` against the mixed
checkpoint. Copy only the resulting package to `MixedDevice6000.mlpackage`.

- [x] **Step 3: Inspect both package contracts**

Use Core ML Tools to verify one image input and one `[1, 300, 6]` output for each
package. Recheck the baseline hash to prove it was not overwritten.

### Task 3: Runtime switching and two-button UI

**Files:**
- Modify: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/PokemonDetector.swift`
- Modify: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/CameraManager.swift`
- Modify: `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/ContentView.swift`

**Interfaces:**
- Consumes: `DetectorModel.defaultSelection` and `resourceName`
- Produces: `CameraManager.selectedModel`, `isSwitchingModel`, and `selectModel(_:)`

- [x] **Step 1: Replace the generated `best` wrapper dependency**

Load `resourceName.mlmodelc` from the app bundle with `MLModel`, discover the image
input and `[1, 300, 6]` output names, and keep the current preprocessing and parser.

- [x] **Step 2: Add serialized cached selection**

Initialize the mixed-device detector before starting the camera. On button taps,
queue selection after any active prediction, reuse cached detectors, clear old
detections, and publish success or failure on the main queue.

- [x] **Step 3: Add the two buttons**

Render `Baseline 3900` and `Mixed Device 6000` above the zoom controls, highlight
the active selection, disable controls while switching, and append the active short
name to the FPS badge.

- [x] **Step 4: Compile all standalone tests**

Run every `Tests/*Tests.swift` harness with its corresponding production sources.

### Task 4: Build, verify, and record completion

**Files:**
- Create: `TaskCompleted/task-2026-09-08-ios-two-model-selector.md`

**Interfaces:**
- Consumes: completed model resources, runtime switcher, and UI
- Produces: reproducible verification evidence and physical-device follow-up steps

- [x] **Step 1: Build for an iOS 17 Simulator**

Run `xcodebuild` with code signing disabled and require `BUILD SUCCEEDED`.

- [x] **Step 2: Verify both compiled resources are embedded**

Inspect the built `.app` for `best.mlmodelc` and `MixedDevice6000.mlmodelc`.

- [x] **Step 3: Review the diff and preserved hashes**

Confirm no checkpoint or baseline package was removed or overwritten and exclude
Xcode user-state files from the implementation.

- [x] **Step 4: Write the completion record**

Record exact model sources, package names, test/build commands, evidence, and the
remaining physical-iPhone comparison.
