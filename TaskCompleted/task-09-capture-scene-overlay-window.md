# Task 9: Capture Scene, Overlay, and Capture Window

- Status: Completed
- Completed: 2026-08-30
- Implementation commit: `531c593`

## Outcome

Delivered the working Unity Pokemon synthetic-data generator scene and its editor workflow:

- Dedicated `PokemonCapture` scene with the exact camera, background, lighting, generated-object, and controller hierarchy.
- Baseline 640x640 generation configuration for seed 42, zero-to-three Pokemon, bounded camera/object variation, box/crop limits, and retry limits.
- Catalog wiring for Pikachu, Charmander, Squirtle, and 98 backgrounds.
- Custom URP background augmentation shader for crop, scale, brightness, contrast, blur, and color-temperature variation.
- Editor-only class-colored bounding-box preview that never enters captured RGB.
- Validated capture window for run ID, output root, frame count, seed, scene dependencies, prefabs, and backgrounds.
- Safe direct-child run directory policy that rejects separators, dot segments, invalid names, and path escape attempts.
- Authored `GeneratedObjects` container reuse with deterministic cleanup and no duplicate roots.

## Review hardening

- Corrected SkinnedMeshRenderer projection so baked vertices do not receive transform scale twice.
- Forced capture projection to use the configured output aspect and restored the camera afterward.
- Added automatic-aspect restoration with `Camera.ResetAspect()` so later Game-view or render-target resizing is not locked to the previous numeric aspect.
- Retained explicit numeric-aspect restoration for callers that intentionally use a manual override.
- Moved Squirtle's visual correction below the normalized prefab root; all Pokemon roots and all `ModelHolder` transforms remain at scale `(1, 1, 1)`.
- Ensured the preview survives one Game-view frame after RGB capture and is cleared before scene disposal.
- Added regression coverage for path traversal, camera aspect behavior, overlay cleanup, authored-container cleanup, background shader behavior, prefab roots, and skinned bounds.

## Smoke run

`Python-ModelTraining/data/generated/smoke-20` contains:

- 20 PNG images.
- 20 YOLO TXT label files.
- 20 manifest rows.
- One `run-config.json`.
- Zero temporary files.
- Six valid empty frames.
- Class object counts: Pikachu 9, Charmander 10, Squirtle 10.

All 20 annotated frames passed visual inspection: boxes matched visible Pokemon, class labels matched identity, empty labels were correct, and models rendered with readable materials/textures.

Two final same-runtime seed-42 repeats were byte-identical for every PNG, label, run config, and manifest content after excluding run ID. Across an Editor restart, labels and recipes remained identical; one PNG had only 17 channel values change, with maximum delta 6, reflecting minor GPU/runtime raster variance.

## Verification

- Complete Unity EditMode suite: 74 passed, 0 failed.
- Complete Unity PlayMode suite: 11 passed, 0 failed.
- Final Unity console: 0 errors/exceptions.
- Unity compilation status: idle/completed without errors.
- Unity CLI saved, reopened, and re-saved `Assets/SyntheticData/Scenes/PokemonCapture.unity`.
- Reloaded scene was active, loaded, clean, outside Play Mode, and validated with 98 backgrounds and zero generated children.
- Independent review completed with no remaining actionable issues.
- `git diff --cached --check` passed before commit.

## Asset note

The background file whose original name produced a duplicate extension-free ID was renamed locally to a unique name. The user's downloaded model and background source assets remain outside the Task 9 implementation commit.

## Next task

Continue with the next unfinished Python/dataset-quality task in the implementation plan, using the retained `smoke-20` run as the first real dataset input.
