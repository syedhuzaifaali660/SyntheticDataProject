# Task 20: Full Verification and Operating Guide

- Status: Saved with deferred gates
- Saved: 2026-09-02
- Unity verification fix commit: `49936e7`
- Operating-guide commit: `4d1cdec`

## Outcome

- Added exact Unity instructions for opening the capture scene, inspecting the
  baseline configuration, generating editor and batch smoke runs, interpreting
  rejections, validating output, and running Unity tests.
- Added exact Python 3.12 and `uv` instructions for validation, visual review,
  background-grouped splitting, training, synthetic evaluation, webcam
  inference, low-confidence capture, and controlled trials.
- Expanded the root handoff with the accepted `baseline-3900` dataset,
  checkpoint, synthetic metrics, reproducibility sequence, verification counts,
  and deferred real-world gate.
- Removed a stale missing scene from Unity build settings and retained the URP
  Unlit shader in standalone players so the complete Play Mode suite can run.
- Corrected the operating guide for Unity Test Framework 1.6: command-line tests
  must not use `-quit`, and Play Mode camera tests use a `StandaloneOSX` player.

## Verification

- `uv run ruff check .`: passed.
- `uv run pytest -m "not slow" --cov=pokemon_detector --cov-report=term-missing`:
  144 passed on Python 3.12.12 with 84% total coverage.
- Unity Edit Mode: 77 passed, 0 failed.
- Unity standalone Play Mode: 11 passed, 0 failed.
- Accepted run `baseline-3900`, seed 42: 3,900 images, 3,900 labels,
  5,874 objects, 977 negative images, zero errors, and zero warnings.
- Stored split: 3,019 train, 569 validation, and 312 test images across 75, 15,
  and 8 mutually disjoint background groups.
- Saved-checkpoint synthetic evaluation reproduced precision 0.9950, recall
  0.9846, mAP50 0.9915, and mAP50-95 0.9836.
- Tracked-file credential scan returned no matches. No generated dataset,
  virtual environment, Unity Library state, or checkpoint weight is tracked;
  only the intended generated/processed `.gitkeep` files matched the path scan.
- Documentation diagnostics and `git diff --check` passed before commit.

## Deferred Gates

- Task 19 remains paused by user decision. Real captures, labels, validation,
  real validation/test reports, failure galleries, ten controlled trials per
  class, and the two-minute negative trial do not exist yet.
- The final webcam demonstration was not completed. VS Code was not listed in
  macOS Camera privacy settings and OpenCV was denied camera access from its
  integrated terminal. Terminal.app is authorized, but the webcam command has
  not yet been demonstrated there.
- Camera opening, all three live classes, visible measured FPS, configured
  low-confidence capture, and clean `q`/Escape exit therefore remain unverified.
- No measured webcam FPS or real-world metrics are recorded. The project does
  not claim the plan's 15 FPS, 8/10 per-class trial, or persistent-false-positive
  acceptance thresholds.
- Task 20 step 6 remains partial: synthetic evaluation passed, but the real
  report cannot be regenerated while Task 19 is paused.
- Task 20 step 10 remains partial because real metrics and measured webcam FPS
  are unavailable. Consequently, the final end-to-end handoff gate is not
  claimed as fully passed.

## Next Task

Run the webcam command from authorized Terminal.app and record observed FPS,
three-class behavior, low-confidence capture, and clean exit. Resume Task 19
later if measured real-world acceptance evidence is required.
