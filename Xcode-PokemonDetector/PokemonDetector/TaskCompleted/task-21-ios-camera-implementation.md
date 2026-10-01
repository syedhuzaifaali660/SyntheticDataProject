# Task 21 — iOS Camera Detector Implementation

## Completed

- Added on-device Core ML inference for the bundled `best.mlpackage` model.
- Added output parsing for Pikachu, Charmander, and Squirtle with a 50% confidence threshold.
- Added rear-camera authorization, capture, single-flight frame processing, status messages, preview, and detection overlay.
- Set the Xcode project deployment target to iOS 17.0.

## Verification

- Parser test harness: PASS.
- Core ML output-reader test harness: PASS.
- Xcode iOS simulator build with signing disabled: PASS (`BUILD SUCCEEDED`).

## Manual Follow-up Required

Run the app on a connected iPhone, grant camera permission, and verify all three Pokémon plus a no-target scene. The simulator build cannot validate a live camera.
