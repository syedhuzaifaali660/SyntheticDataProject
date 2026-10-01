# iOS Two-Model Selector Design

## Goal

Keep the existing `baseline-3900` model and the new
`mixed-device-6000-640-occlusion-safe-0907` model in the iOS app, with two
buttons that switch the active detector without restarting the camera.

## Approved interaction

- Show `Baseline 3900` and `Mixed Device 6000` as two persistent controls.
- Launch with `Mixed Device 6000` selected.
- Highlight the active model and display its short name beside the inference FPS.
- On selection, finish the current inference, clear stale detections, load or reuse
  the selected detector, then continue the same live camera session.
- Disable both buttons while a model is being loaded and show a useful failure
  message if loading fails.

## Model ownership

- Preserve `Python-ModelTraining/runs/baseline-3900/weights/best.pt`.
- Preserve
  `Python-ModelTraining/runs/mixed-device-6000-640-occlusion-safe-0907/weights/best.pt`.
- Keep the already embedded `best.mlpackage` as the baseline Core ML resource.
- Export and embed the new package as `MixedDevice6000.mlpackage`; never overwrite
  the baseline package.

## Runtime boundary

`DetectorModel` owns stable labels and bundle resource names. `PokemonDetector`
loads a compiled Core ML resource generically, discovers its image input and
`[1, 300, 6]` multi-array output, and exposes the existing detection interface.
`CameraManager` owns the active selection and a detector cache. All swaps occur on
the serial frame-processing queue so a detector cannot change during prediction.

## Verification

- A standalone Swift test verifies the two stable model identities and the new
  default.
- Existing standalone geometry, parser, camera, zoom, and FPS tests remain green.
- A signing-disabled iOS Simulator build proves both packages compile and embed.
- Physical-iPhone testing remains required to compare FPS and real-camera quality.

