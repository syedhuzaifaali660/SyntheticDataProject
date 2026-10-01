# Task 23: Mixed-Device Generation Contracts

## Completed

- Added deterministic capture-profile definitions for square, iPhone portrait, iPhone landscape, webcam, and laptop-window output shapes.
- Added approved native resolution variants and profile weights.
- Added per-recipe output dimensions, lighting bands, and object-size bands.
- Added a reusable normalized-rectangle `LetterboxTransform` for the fixed 640 model input.
- Added regression tests for deterministic profile selection and portrait letterbox round-tripping.

## Verification

- Confirmed RED compilation failures before production types existed.
- `SyntheticData.Tests.FrameRecipeSamplerTests`: 15 passed, 0 failed.
- `SyntheticData.Tests.LetterboxTransformTests`: 1 passed, 0 failed.
- Full Unity EditMode suite: 80 passed, 0 failed.

## Unity CLI Note

Unity 6000.3.8f1 did not execute tests when `-quit` was supplied with `-runTests`. The verified commands omit `-quit`; Unity exits automatically after saving the XML result file.

