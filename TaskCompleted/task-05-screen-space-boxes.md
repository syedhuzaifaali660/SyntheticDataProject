# Task 5: Cached Screen-Space Boxes and YOLO Normalization

- Status: Completed
- Completed: 2026-08-29
- Commit: `626809b`

## Outcome

Added cached local-vertex extraction for static and skinned meshes, deterministic camera projection into caller-requested output dimensions, finite and clamped pixel bounds, crop/minimum-size rejection, and normalized YOLO boxes. Pixel bounds remain in Unity's bottom-left convention; `ToYolo` performs the single top-left image Y conversion.

Vertex caches use weak per-target ownership so destroyed generated objects are not retained. `GLTFAST_KEEP_MESH_DATA` is persisted for the Standalone target in `ProjectSettings.asset`, keeping imported glTF mesh vertices readable for annotation. Skinned-mesh temporary objects use exception-safe, PlayMode-aware cleanup.

## Verification

- The initial tests failed because the label/projection types did not exist, then passed after implementation.
- A final review found unreadable production glTF meshes and camera/output resolution coupling; both defects received regression tests and were fixed.
- The fresh isolated Unity 6000.3.8f1 EditMode run passed all 31 tests with 0 failures and no C# compiler warnings or errors.
- The suite projects the real Pikachu prefab, projects an `800x400` output independently of a `1000x1000` camera target, rejects a cube cropped beyond 25%, verifies asymmetric YOLO Y conversion, and confirms identical bounds across consecutive unchanged calls.
- The scoped re-review marked every critical, important, and fix-wave finding addressed with no new critical or important issue.
