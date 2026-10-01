# Task 7: YOLO Labels, Manifests, and Atomic Capture Writes

- Status: Completed
- Completed: 2026-08-30
- Implementation commit: `39cc968`
- Hardening commit: `312ec47`
- Unity Pipeline tooling commit: `c28a58c`

## Outcome

Added invariant, six-decimal YOLO label formatting; one-based six-digit capture paths; serializable manifest records; immutable run configuration; and an `AtomicCaptureWriter` that writes paired PNG and label artifacts before appending newline-delimited manifest records.

Failure handling now removes every temporary file and only the final files created by the failing call. Pre-existing artifacts are preserved, cleanup is exhaustive and best-effort, and the original failure remains primary with frame-ID context.

The manifest contract uses the on-disk keys `run_id`, `frame_id`, `seed`, `split_hint`, `background_id`, `width`, `height`, and `objects`, including class, box, and transform metadata. Task 8 explicitly maps zero-based `FrameRecipe.FrameIndex` values to one-based output IDs.

## Verification

- Initial TDD RED: compilation failed because `SyntheticData.Output` and `FrameArtifact` did not exist.
- Review-fix RED: the expanded rollback test failed because the manifest append seam did not exist.
- Focused GREEN: all 10 `OutputWriterTests` passed.
- Fresh final GREEN: Unity CLI ran the isolated Unity 6000.3.8f1 EditMode suite with 46 passed, 0 failed, and 0 skipped tests.
- Independent re-review found no remaining critical, important, or minor Task 7 issues.
- `git diff --check` passed.

## Unity CLI checkpoint

Installed `com.unity.pipeline` `0.5.0-exp.1` in the Unity project. Through the official Unity CLI, `Assets/Scenes/SyntheticData.unity` was saved, reopened, saved again, and confirmed active, loaded, and clean.

## Next task

Task 8 adds render-texture camera capture and the controller that connects deterministic frame recipes, scene construction, YOLO projection, PNG capture, and the atomic writer.
