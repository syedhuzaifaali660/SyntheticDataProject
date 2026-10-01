# Task 4: Deterministic Generation Contracts

- Status: Completed
- Completed: 2026-08-29
- Commit: `f3aa6a7`

## Outcome

Added a serialized Unity generation configuration, deterministic per-frame and per-attempt seeds, complete frame recipes, a `System.Random`-backed random source, and a frame sampler for the three Pokémon classes. Recipes now contain background selection and augmentation, object classes and transforms, normalized separated centers, camera FOV, and point/ambient light values so later scene code does not sample hidden randomness.

The retry overload keeps output-frame object count and class scheduling stable while producing deterministic alternative scene values for rejected captures. Class IDs use shuffled round-robin cycles, keeping cumulative counts within one object of each other.

## Verification

- The initial planned tests failed because the generation contract types did not exist, then passed after implementation.
- Review-driven tests failed before retry and background contracts were added, then passed after the fixes.
- The final isolated Unity 6000.3.8f1 EditMode run passed all 22 tests with 0 failures and no C# compiler warnings or errors.
- Tests cover repeatability, call-order independence, Unity global-random isolation, value ranges, center separation including the supported `0.8` boundary, class balance, deterministic retries, background values, invalid/non-finite settings, and mid-run config mutation.
- An independent code review found no remaining critical or important issues after the follow-up fixes.
