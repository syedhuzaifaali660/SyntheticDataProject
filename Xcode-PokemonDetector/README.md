# Pokémon Detector: iOS App

This iPhone app uses the rear camera and Core ML to detect Pikachu, Charmander,
and Squirtle in live video. It draws detection boxes, confidence labels, and an
inference FPS counter over the camera preview.

## Requirements

- macOS with Xcode 26.6 or later
- iOS 17 or later for a physical iPhone
- Camera access to run live detection on a device

The app can be built for the iOS Simulator, but live camera inference requires
a physical iPhone with a camera. The Xcode project targets iPhone (not iPad).

## Open and run

1. Open `PokemonDetector/PokemonDetector.xcodeproj` in Xcode.
2. Select the `PokemonDetector` scheme and an iPhone device or simulator.
3. Choose your development team in Signing & Capabilities if Xcode asks.
4. Build and run. On a physical device, allow camera access when prompted.

## Included models

The app bundles two trained Core ML model packages in
`PokemonDetector/`:

- `best.mlpackage` — shown in the app as **Baseline 3900**
- `MixedDevice6000.mlpackage` — shown as **Mixed Device 6000** and selected by
  default

Use the buttons along the bottom of the camera view to switch models. The
overlay also shows the selected model. Both packages are tracked in Git as
backups; other model weights and training artifacts are excluded.

## Tests and troubleshooting

The `PokemonDetector/Tests/` directory contains XCTest source files, but the
checked-in Xcode project currently defines only the app target and does not
configure a test target. The tests therefore are not runnable as a suite from a
fresh checkout until a test target is added. If Xcode cannot load a model,
confirm that the corresponding `.mlpackage` folder exists beside the Swift
sources and that the package was not removed from the project checkout. On a
device, check **Settings > Privacy & Security > Camera** if camera access was
denied.

## Rights and licensing

The app and its trained models recognize Pokémon characters, which are third
party intellectual property. Check the applicable rights and model terms before
redistributing the app or its models. The repository has no project `LICENSE`
file yet, and it does not grant general reuse permission.
