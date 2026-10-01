# Task 14: Evaluation and Failure Gallery

- Status: Completed
- Completed: 2026-09-01
- Implementation commit: `357b244`
- Review-hardening commit: `1d20b69`

## Outcome

Delivered the real-image annotation and synthetic-versus-real evaluation boundary for the three-Pokemon detector:

- Added pixel-to-YOLO annotation conversion with fixed class validation and an OpenCV annotation workflow.
- Added explicit-save annotation behavior, current-image dimension handling, previous/next navigation, undo, and pending-drag cleanup.
- Added canonical dataset provenance checks that reject synthetic/real report mislabeling, training-split evaluation, path escapes, split overlap, and invalid taxonomy mappings before YOLO construction.
- Added isolated synthetic and real JSON reports with overall and per-class precision, recall, F1, true-positive, false-positive, and false-negative counts.
- Added Ultralytics validation adaptation and preservation of confusion-matrix artifacts when available.
- Added an annotated failure gallery that prioritizes false negatives, then low-confidence correct detections, uses IoU-first matching, cleans stale output, and is hard-capped at 50 images.

## Review hardening

- Resolved and validated YAML split paths before model creation, including relative, absolute, cross-provenance, overlapping, and symlink-equivalent cases.
- Corrected report completeness and confusion-matrix orientation for wrong-class and background outcomes.
- Replaced raw image copying with visible truth/prediction overlays and atomic output replacement.
- Changed matching from confidence-first to IoU-first so overlapping truths do not create artificial misses.
- Cleared pending annotation state on navigation and quit, and normalized against each current image's dimensions.
- Enforced exact integer taxonomy keys so YAML booleans cannot masquerade as class IDs.
- Expanded mutation-oriented coverage for the real GUI loop and gallery edge cases.

## Verification

- `uv run pytest tests/evaluation -v`: 30 passed.
- `uv run pytest -q`: 103 passed.
- `uv run ruff check .`: all checks passed.
- `uv lock --check`: current; 71 packages resolved.
- Independent scoped re-review: all seven findings addressed, no new Critical or Important regressions, spec compliance PASS, task quality APPROVED.
- Evaluation tests use fake model adapters and valid temporary images; no model weights were downloaded and no real YOLO evaluation was started.

## Next task

Task 15 adds the testable real-time webcam application, detector adapter, rolling FPS, overlays, and clean camera shutdown behavior.
