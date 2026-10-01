# Task 2026-09-07: Mixed-device capture repair

## Outcome

Audited the mixed-device retraining plan and repaired the Unity generation step that repeatedly stopped after 42 or 174 frames. The failure came from sampling approximate world-space scale and position values, then repeatedly rejecting real projected meshes that missed the requested 640-model-pixel size band or viewport bounds.

Mixed-device captures now fit the complete rotated mesh to the selected Small, Medium, or Large projected-size band and keep the full box inside the selected device profile. Profile, resolution, lighting band, and size band no longer change during a retry. The live capture runner applies the mixed-device settings, the background quad fills portrait and landscape viewports while preserving background aspect ratio, and the capture script reports progress while Unity is running. Existing runs and trained models were preserved.

The accepted repaired run is `Python-ModelTraining/datasets/generated/mixed-device-6000-640-final-0907`. It contains 6,000 images, 6,000 labels, and 6,000 manifest records and occupies approximately 3.7 GB.

## Verification

- Unity Edit Mode: 85 passed, 0 failed, including all 6,000 deterministic recipes against the real Pikachu, Charmander, and Squirtle prefabs.
- Python: Ruff passed for the changed validation files; 146 tests passed.
- Unity capture: 6,000 of 6,000 frames generated; Unity exited with status 0. Log: `/tmp/mixed-device-6000-640-final-0907.TI9QhD`.
- Dataset validation: passed with zero errors and zero warnings. Counts are 3,032 Pikachu, 3,031 Charmander, 3,031 Squirtle, and 1,490 negative images.
- Profiles: 958 Square, 1,540 iPhone portrait, 1,124 iPhone landscape, 1,217 Webcam, and 1,161 Laptop window frames.
- Lighting: 1,223 Low, 3,557 Normal, and 1,220 Bright frames.
- Object size: 2,747 Small, 4,535 Medium, and 1,812 Large objects. Longest model-input sides range from 16.04 to 431.83 pixels; all 9,094 objects satisfy their requested band.
- Independent geometry check: zero boxes cross an image boundary; closest edge is approximately 2.47% of the frame.
- One hundred deterministic validation overlays were rendered. Spot checks across square, portrait, landscape, laptop, bright, normal, and low-light samples confirm that corrected backgrounds fill the native viewport and boxes follow the rendered mesh.

## Deferred gates

Task 7 is not complete. The exact background-isolated 4,800/900/300 split is not implemented yet, and all 100 overlays still require the plan's full human review with at least 20 examples per profile. The batch-64 MPS smoke run, 100-epoch training, comparative evaluation, Core ML export, and physical iPhone promotion test also remain pending. The iOS letterbox task still needs explicit RGB-114 padding and corrected standalone geometry tests.

The earlier `mixed-device-6000-640-verified-0907` run is retained as diagnostic evidence but should not be used for training because it predates the background-fill correction.

## Next task

Complete Task 4 Step 4: implement and verify an exact 4,800/900/300 background-isolated split, then complete Task 7 Step 2's 100-overlay review before any training starts.
