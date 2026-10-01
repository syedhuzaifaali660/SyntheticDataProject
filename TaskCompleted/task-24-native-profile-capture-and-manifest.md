# Task 24: Native Profile Capture and Manifest Metadata

## Completed

- Capture recipes now select native profile dimensions for scene aspect, projection, and PNG output.
- Capture runs can enable mixed profiles in the Unity window or with `-captureMixedDeviceProfiles`.
- Manifests record capture profile, source dimensions/aspect, letterbox scale/padding, lighting band, object size band, and projected 640-model box dimensions.
- Existing baseline constructors remain backward-compatible and old runs/models were not changed.

## Verification

- Confirmed manifest test RED before adding the metadata fields.
- Full Unity EditMode suite: 82 passed, 0 failed.
- Graphics-backed Unity smoke run `mixed-device-task2-smoke-graphics`: 5 native frames, 5 labels, 5 manifest records.
- Verified output dimensions: `780x360`, `1280x800`, `640x480`, `540x1170`, and `640x640`.
- Python validation passed with zero errors and five overlays generated.

## Important CLI Note

Use Unity batch mode without `-nographics` for image capture. `-nographics` produces flat 205-gray frames even though dimensions and manifests are valid; it is suitable only for non-rendering tests.

