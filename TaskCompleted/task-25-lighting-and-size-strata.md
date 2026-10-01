# Task 25: Lighting and Object-Size Strata

## Completed

- Mixed-device recipes now sample low, normal, and bright lighting bands at 20/60/20 weights.
- Mixed-device object recipes now sample small, medium, and large scale/distance bands at 30/50/20 weights.
- Background brightness, contrast, blur, and temperature ranges are widened for mixed-device runs.
- The controller rejects projected boxes that do not match their selected 640-input size band.
- Baseline-mode sampling remains compatible with the previous ranges.

## Verification

- Confirmed RED on the new distribution test before implementation.
- Mixed-device lighting/scale sampler tests: 16 passed, 0 failed.
- Full Unity EditMode suite: 83 passed, 0 failed.

