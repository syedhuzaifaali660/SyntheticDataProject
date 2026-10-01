# Task 8: Camera Capture and Orchestration

- Status: Completed
- Completed: 2026-08-30
- Implementation commit: `596b2c8`

## Outcome

Added isolated camera-to-PNG capture and a deterministic coroutine controller that connects recipe sampling, scene construction, end-of-frame rendering, bounds projection, retry rejection, atomic output writing, and guaranteed scene cleanup.

Output identity is one-based (`FrameIndex + 1`), while retries preserve the zero-based frame index and increment the attempt. Rejections retain frame, attempt, seed, and reason. Any invalid object projection rejects the whole candidate; zero-object negative frames remain valid.

## Review hardening

- Corrected the capture target to use Unity's default sRGB-aware read/write behavior. A mid-tone regression proved the previous forced-linear path encoded `0.5` as approximately `0.216`.
- Added real render/project/capture/write integration coverage for `frame_000001.png`, `frame_000001.txt`, and manifest `frame_id: 1`.
- Correlated the YOLO annotation with real rendered red pixels.
- Verified capture and writer exceptions both clear generated scene state.
- Verified generated objects are destroyed and render-texture population returns to baseline.

## Verification

- Focused real-render and real-writer chunk gate: 1 passed, 0 failed.
- Complete Unity PlayMode suite: 10 passed, 0 failed.
- Complete Unity EditMode suite: 46 passed, 0 failed.
- Unity CLI saved, reopened, and re-saved `Assets/Scenes/SyntheticData.unity`; it was active, loaded, and clean.
- Independent review completed with all critical and important findings addressed.
- `git diff --check` passed.

## Next task

Task 9 creates the dedicated `PokemonCapture` scene, baseline generation configuration, editor-only bounds overlay, and capture-run Editor window.
