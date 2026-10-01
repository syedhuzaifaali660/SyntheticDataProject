# Pokémon Camera Detector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an iOS 17+ SwiftUI app that detects Pikachu, Charmander, and Squirtle from the rear camera using the bundled Core ML model.

**Architecture:** AVFoundation captures rear-camera frames and supplies them to a single-flight Core ML detector on a background queue. A SwiftUI overlay draws normalized detection rectangles over an aspect-fill preview.

**Tech Stack:** Swift 5, SwiftUI, AVFoundation, Core ML, XCTest, Xcode 26.

**Spec:** `docs/superpowers/specs/2026-09-03-pokemon-camera-detector-design.md`

## Global Constraints

- Support iOS 17.0+ with no server, Python runtime, or third-party mobile dependencies.
- Model input: 640 × 640 BGRA; output: `1 × 300 × 6` float array, rows `[x1, y1, x2, y2, confidence, class_id]`.
- Retain confidence `>= 0.50`; map IDs `0`, `1`, `2` to Pikachu, Charmander, Squirtle.
- Use the rear camera and one in-flight inference; preserve existing model and signing changes.

---

### Task 1: Detection Types and Parser

**Files:**
- Create: `PokemonDetector/Detection.swift`
- Create: `PokemonDetector/PokemonOutputParser.swift`
- Create: `PokemonDetectorTests/PokemonOutputParserTests.swift`
- Modify: `PokemonDetector.xcodeproj/project.pbxproj`

**Produces:** `Detection` with normalized `CGRect`, label, confidence; `PokemonOutputParser.parse(rows: [[Float]], confidenceThreshold: Float = 0.50) -> [Detection]`.

- [ ] Write failing XCTest cases for known class mapping, 640-pixel box normalization, low-confidence filtering, unknown-class filtering, and inverted-box filtering.
- [ ] Run `xcodebuild -project PokemonDetector.xcodeproj -scheme PokemonDetector -destination 'generic/platform=iOS Simulator' test`; confirm failure because parser symbols are absent.
- [ ] Add an XCTest target; implement `Detection` and parser using labels `[0: "Pikachu", 1: "Charmander", 2: "Squirtle"]`. Reject rows that are not six values, are under threshold, use unknown classes, or have nonpositive width/height.
- [ ] Re-run the test command and confirm PASS.
- [ ] Commit only parser/test/project changes with `feat: add pokemon output parser`.

### Task 2: Core ML Detector

**Files:**
- Create: `PokemonDetector/PokemonDetector.swift`
- Create: `PokemonDetectorTests/PokemonDetectorTests.swift`

**Consumes:** Task 1 parser. **Produces:** `PokemonDetector.detect(in: CVPixelBuffer) throws -> [Detection]` and internal multi-array row extraction.

- [ ] Write a failing XCTest that creates `MLMultiArray(shape: [1, 300, 6], dataType: .float32)` and asserts that a populated first row is returned exactly.
- [ ] Run the test command and confirm failure because row extraction is absent.
- [ ] Load generated class `best` with `MLModelConfiguration.computeUnits = .all`; resize camera buffers to 640 × 640 BGRA; call `prediction(image:)`; extract rows respecting the multi-array shape and strides; parse them through Task 1.
- [ ] Re-run all tests and confirm PASS.
- [ ] Commit detector/test changes with `feat: add core ml pokemon detector`.

### Task 3: Camera Lifecycle and Live Inference

**Files:**
- Create: `PokemonDetector/CameraManager.swift`
- Modify: `PokemonDetector.xcodeproj/project.pbxproj`

**Consumes:** Task 2 detector. **Produces:** `@MainActor CameraManager` with published detections, status text, `AVCaptureSession`, and `start()`.

- [ ] Extend the parser test with an assertion that IDs 0–2 produce exactly the three trained labels; first run it to confirm RED if incomplete.
- [ ] Configure authorization, the rear `AVCaptureDevice`, `AVCaptureVideoDataOutput`, and a dedicated sample-buffer queue. Set `alwaysDiscardsLateVideoFrames = true`.
- [ ] Guard an `isInferenceRunning` flag around detector calls; publish results and errors on the main actor. Expose denied, unavailable-camera, model-load, and inference failures as user-visible status text.
- [ ] Change Debug and Release `IPHONEOS_DEPLOYMENT_TARGET` to `17.0`; preserve the existing camera usage description.
- [ ] Run tests, then run `xcodebuild -project PokemonDetector.xcodeproj -scheme PokemonDetector -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' CODE_SIGNING_ALLOWED=NO build`; require `BUILD SUCCEEDED`.
- [ ] Commit with `feat: add live camera inference`.

### Task 4: Camera Preview and Overlay

**Files:**
- Create: `PokemonDetector/CameraPreview.swift`
- Create: `PokemonDetector/DetectionOverlay.swift`
- Modify: `PokemonDetector/ContentView.swift`

**Consumes:** Task 3 camera state. **Produces:** live `CameraPreview` and a label/box `DetectionOverlay`.

- [ ] Run the inverted-box parser test from Task 1 as the red case for overlay safety.
- [ ] Implement `CameraPreview` as `UIViewRepresentable` with `AVCaptureVideoPreviewLayer.videoGravity = .resizeAspectFill`.
- [ ] Implement `DetectionOverlay` to draw normalized valid boxes as green rounded borders with label and percentage confidence. Replace `Hello, world!` with the preview, overlay, and status message.
- [ ] Re-run tests and simulator build; require all tests PASS and `BUILD SUCCEEDED`.
- [ ] Commit with `feat: add detection camera interface`.

### Task 5: Physical iPhone Verification

**Files:**
- Create: `TaskCompleted/task-21-ios-camera-detector.md`

- [ ] In Xcode, select the signed physical iPhone, run the app, and grant camera permission.
- [ ] Test Pikachu, Charmander, Squirtle, then a no-target scene; confirm labels and no stale rectangle in the no-target case.
- [ ] Record device/iOS version, simulator build result, each target result, negative-scene result, and limitations in the TaskCompleted markdown file.
- [ ] Commit the record with `docs: record ios detector verification`.

## Self-Review

- Tasks 1–2 cover exact Core ML output decoding, labels, and tests; Task 3 covers authorization, errors, rear camera, performance, and iOS 17; Task 4 covers preview/overlay; Task 5 covers real-device validation.
- All interfaces are declared in the task that first produces them, and no network functionality is introduced.
