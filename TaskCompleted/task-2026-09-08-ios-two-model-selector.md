# Task Completed: iOS Two-Model Selector

## Outcome

The iOS 17 Pokémon detector now embeds both trained models and provides two
persistent live-camera controls:

- `Baseline 3900`
- `Mixed Device 6000`

`Mixed Device 6000` is selected at launch. Tapping the other button waits for the
current inference boundary, loads or reuses the requested detector, clears stale
boxes, resets the displayed inference FPS, and continues the existing camera
session. Successfully loaded detectors remain cached for fast later switching.

The FPS badge includes the active model name. Buttons are disabled during initial
loading and switching, the active button is highlighted, and model-load failures
leave the previous detector selected.

## Preserved and created models

- Preserved baseline checkpoint:
  `Python-ModelTraining/runs/baseline-3900/weights/best.pt`
  (`90efa949b98fcbb09749d1bf76de0c9702cb701777aea9b7bfb50ea022bd00ff`)
- Preserved mixed-device checkpoint:
  `Python-ModelTraining/runs/mixed-device-6000-640-occlusion-safe-0907/weights/best.pt`
  (`cf85e32b7246cde114b01a2d35b3811518e54926c101bb384f73fd206c1a24c1`)
- Preserved existing baseline app resource:
  `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/best.mlpackage`
- Exported mixed-device package:
  `Python-ModelTraining/runs/mixed-device-6000-640-occlusion-safe-0907/weights/best.mlpackage`
- Added mixed-device app resource:
  `Xcode-PokemonDetector/PokemonDetector/PokemonDetector/MixedDevice6000.mlpackage`

The app copy and export copy of `MixedDevice6000` have matching manifest, graph,
and weight hashes. The recorded baseline package hashes remained unchanged before
and after export.

Both Core ML packages expose one 640 × 640 image input and one Float32
`[1, 300, 6]` detection output.

## Implementation

- Added `DetectorModel.swift` for stable model identities, UI labels, bundle names,
  and the mixed-device default.
- Added `DetectorSelectionState.swift` so loading and active models remain
  distinct and an initial load failure can be retried or replaced by the baseline.
- Added `DetectorSelectionState.swift` so initial loading has no false active model,
  a failed default load remains retryable, and a fallback model can activate while
  the already configured camera preview continues running.
- Replaced the generated one-model `best` dependency with a generic Core ML loader
  that validates each package contract at load time.
- Added serialized, cached model switching to `CameraManager`.
- Queue-ordered clearing prevents an in-flight result from the previous model from
  republishing stale boxes after a switch begins.
- Added two model buttons and the active-model FPS label to `ContentView`.
- Explicitly composites Ultralytics-compatible RGB-114 padding when letterboxing
  camera frames.
- Corrected the previously invalid portrait inverse-letterbox expectations and
  added landscape and padding tests.
- Wrapped retained Core Video frames in an explicitly sendable owner so the iOS
  builds no longer emit the non-Sendable capture warning.

## Verification

- Core ML export with Ultralytics 8.4.133 and Core ML Tools 9.0: passed.
- `DetectorModelTests` TDD red/green cycle: passed.
- Eight standalone Swift test executables: passed.
- Signing-disabled generic iOS Simulator build: passed.
- Signing-disabled generic physical-iPhone SDK build: passed.
- Both built `.app` bundles contain `best.mlmodelc` and
  `MixedDevice6000.mlmodelc`.

## Remaining device check

Install and run the app on the physical iPhone, switch between the two buttons on
the same views, and record detection quality and FPS for each model. This manual
comparison is required before declaring the mixed-device model physically promoted;
it does not block the completed two-model mobile workflow or either build.
