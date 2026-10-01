# Task 6: Background Catalog and Frame Scene Builder

- Status: Completed
- Completed: 2026-08-29
- Commit: `b597871`

## Outcome

Added validated catalogs for the three Pokémon prefabs and background textures, plus a deterministic `FrameSceneBuilder` that constructs each synthetic frame from a `FrameRecipe`. Generated Pokémon are parented under `GeneratedObjects`, retain their class labels and shared mesh-vertex caches, and use the configured output aspect so normalized centers do not depend on the Unity Game view.

`SpawnedFrame.Clear` restores the original camera FOV, background material properties, point-light transform/intensity/temperature, and ambient intensity, then removes only the roots created for that frame. Failed builds also roll back scene state and generated objects.

## Verification

- Initial RED: scene-builder tests failed to compile because the Task 6 runtime types did not exist.
- Review regression RED: the mismatched camera/output-aspect test failed before the placement correction.
- Focused GREEN: all 5 `FrameSceneBuilderTests` passed.
- Final GREEN: the fresh isolated Unity 6000.3.8f1 EditMode run passed all 36 tests with 0 failures, 0 skipped tests, and no C# compiler warnings or errors.
- Independent scoped re-review confirmed the geometry fix and found no remaining Task 6 blocker.

## Follow-up gates

- Task 8 will verify deferred `Object.Destroy` cleanup at the end of a PlayMode frame.
- Task 9 will supply and render-test the shader that consumes the background augmentation properties, and will validate all three production Pokémon prefabs in the authored capture scene.
