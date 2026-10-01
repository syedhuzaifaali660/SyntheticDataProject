# Task 22: iOS Inference FPS Overlay

## Completed

- Added a smoothed FPS counter based on completed Core ML predictions.
- Published inference FPS from `CameraManager` without counting dropped camera frames.
- Added a readable top-left `FPS` badge below the iPhone safe area.
- Added a standalone regression test for FPS calculation and invalid timestamps.

## Files

- `PokemonDetector/InferenceFPSCounter.swift`
- `PokemonDetector/CameraManager.swift`
- `PokemonDetector/ContentView.swift`
- `Tests/InferenceFPSCounterTests.swift`

## Verification

- All seven standalone Swift test executables passed.
- The `PokemonDetector` iOS Simulator target built successfully with deployment target iOS 17.0.

