# Task 15: Real-Time Webcam Inference

- Status: Implementation completed; real-camera smoke test deferred until a project checkpoint exists
- Completed: 2026-09-02
- Implementation commit: `9247644`
- Review-hardening commit: `626b1a8`
- Checkpoint-alignment commit: `fff19cb`

## Outcome

Delivered the testable live webcam application for the three-Pokemon detector:

- Added the planned `configs/webcam.yaml` defaults for checkpoint, camera, confidence, image size, device, and optional review-frame saving.
- Added a caller-independent configuration loader that resolves relative paths beneath the Python project root.
- Added an OpenCV frame source with requested dimensions, unavailable-camera handling, idempotent release, and exception-safe post-open initialization.
- Added an Ultralytics adapter using `predict(frame, conf=..., imgsz=..., device=..., verbose=False)` and the fixed Pikachu, Charmander, Squirtle taxonomy.
- Added safe floor/ceil conversion and frame clamping for real subpixel prediction boxes.
- Added the live loop with class-colored boxes, class names, confidence, rolling FPS, inference milliseconds, and `q`/Escape exit controls.
- Added stable exit codes: missing checkpoint `2`, unavailable camera `3`, and live inference/backend failure `4`.
- Added opt-in review-frame saving below confidence `0.65`, with timestamp, predicted class, and confidence in each filename.
- Added cleanup guarantees for source and display on normal exit, immediate end-of-stream, keyboard exit, and inference failure.

## Test-first and review hardening

- The first FPS test failed because the inference package did not exist, then passed after the minimal rolling counter implementation.
- The webcam boundary tests failed because the CLI and webcam modules did not exist, then passed after implementation.
- Mutation checks proved disabled saving, exact overlay text, and the confidence-threshold boundary can each catch their corresponding production regression.
- Independent review found camera-constructor cleanup, uncreated-window cleanup, subpixel-box conversion, mutation coverage, and path/default-test weaknesses.
- Each finding received a focused failing regression before its fix; scoped re-review found all six addressed and no new Critical or Important regressions.

## Verification

- `uv run pytest tests/inference -v`: 23 passed.
- `uv run pytest -q`: 126 passed.
- `uv run ruff format --check ...`: eight scoped files formatted.
- `uv run ruff check .`: all checks passed.
- `uv lock --check`: current; 71 packages resolved.
- Independent scoped re-review: spec compliance PASS and task quality APPROVED.

## Deferred operational gate

The real-camera smoke test remains open because the repository does not yet contain the trained three-Pokemon `best.pt`. Run it after the staged dataset and baseline-training tasks produce that checkpoint:

```bash
cd Python-ModelTraining
uv run python -m pokemon_detector.cli.webcam --config configs/webcam.yaml
```

Expected: the camera opens, overlays update, `q` exits cleanly, and the terminal returns to the prompt.

## Next task

Task 16 generates, validates, and visually proves the 100-image synthetic review dataset before the 300-image training pilot.
