# Pokémon Camera Detector Design

## Goal

Replace the starter SwiftUI screen with an iOS 17+ app that uses the rear camera to identify the three Pokémon represented by the bundled Core ML model. Inference is performed entirely on the device; the app has no network or Python runtime dependency.

## Architecture

`ContentView` composes a camera preview, a detection overlay, and a small status view. It owns the observable application state and requests camera authorization.

`CameraManager` owns an `AVCaptureSession`, configures the rear camera and a video-data output, and forwards sample buffers on a dedicated serial queue. It is intentionally responsible only for camera lifecycle and frame delivery.

`PokemonDetector` loads the generated `best` Core ML model once and performs prediction work on a background queue. It accepts a camera frame, converts its pixel buffer to the model's 640-by-640 input, and returns parsed detections. It allows at most one inference at a time; subsequent frames are discarded until that inference finishes.

`Detection` is a small value type containing a normalized bounding rectangle, Pokémon label, confidence, and class identifier. `DetectionOverlay` transforms normalized rectangles into the rendered preview size and draws labels and boxes.

## Data Flow

1. The user grants camera permission.
2. `CameraManager` starts the rear camera and emits frames.
3. `PokemonDetector` runs the bundled `best.mlpackage` model off the main thread.
4. The detector interprets each output row as `[x1, y1, x2, y2, confidence, class_id]`, drops detections below 0.50 confidence, and maps the class IDs to Pikachu, Charmander, and Squirtle.
5. The detector publishes normalized detections to the main actor.
6. `DetectionOverlay` renders the latest result over the live preview.

## Display and Coordinate Rules

The model's input is fixed at 640 by 640. Frames use aspect-fill preview behavior. The implementation must convert model coordinates back to normalized preview coordinates and account for aspect-fill cropping so labels remain attached to their Pokémon. The first implementation is portrait-oriented and supports the app's existing portrait and landscape device orientations.

## Error Handling

The app shows an explanatory status message if camera access is denied, a rear camera is unavailable, the Core ML model cannot load, or inference fails. It does not crash due to an unavailable camera (including a simulator). It presents no detections when the model returns only low-confidence rows.

## Performance

Inference is not performed on the main thread. The camera output discards late frames and the detector accepts one frame at a time. This prioritizes a responsive camera preview over processing every captured frame.

## Testing and Verification

Unit tests will cover class-label mapping, confidence filtering, and model-output box decoding with synthetic rows. The target will compile for the iOS simulator with signing disabled. The final manual check will run on a physical iPhone because a simulator cannot validate a live camera.

## Out of Scope

This change does not add photo-library import, recording, App Store distribution, analytics, cloud inference, retraining, or additional Pokémon classes.
