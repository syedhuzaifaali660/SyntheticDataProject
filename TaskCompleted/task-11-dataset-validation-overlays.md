# Task 11: Dataset Validation and Overlays

- Status: Completed
- Completed: 2026-08-31
- Implementation commit: `28de133`
- Review-hardening commit: `5b8751f`

## Outcome

Delivered the Python dataset-quality gate for Unity-generated YOLO runs:

- Strict YOLO label parsing for the fixed Pikachu, Charmander, and Squirtle taxonomy.
- Structural validation of image/label pairs, image readability, manifest membership and dimensions, class IDs, normalized boxes, object counts, and intentional negative frames.
- Deterministic class-colored Pillow overlays with class names and normalized box values.
- A `python -m pokemon_detector.cli.validate` entry point that writes `validation-report.json` and exits successfully only for a passing run.
- Explicit mapping from Unity manifest `width`/`height` fields to Python domain names.
- Signed 32-bit Unity seed compatibility in `ManifestRecord`.

## Review hardening

- Converted malformed manifest JSON, invalid UTF-8 labels, and malformed or incomplete run configuration into stable machine-readable validation issues instead of tracebacks.
- Added symmetric detection for manifest records that have no image/label files.
- Added a missing-class balance warning for the fixed three-class taxonomy.
- Narrowed edge compatibility to the exact half-unit-at-six-decimals quantization boundary produced by Unity labels; larger or non-six-decimal out-of-image boxes remain invalid.
- Independent scoped re-review found all five blocking findings addressed with no new Critical or Important breakage.

## Smoke-run result

`Python-ModelTraining/data/generated/smoke-20` validated with:

- 20 readable PNG images.
- 20 paired YOLO label files.
- 29 annotated objects: Pikachu 9, Charmander 10, Squirtle 10.
- 6 valid negative images.
- 0 validation errors and 0 warnings.
- 20 deterministic overlay images.

All 20 overlays were reviewed in two contact sheets. Positive boxes aligned with the visible Pokemon and class identities; negative frames contained no annotations.

## Verification

- `uv lock --check`: current; 71 packages resolved.
- `uv run pytest -q`: 39 passed.
- `uv run ruff check .`: all checks passed.
- `uv run python -m pokemon_detector.cli.validate data/generated/smoke-20 --overlay-count 20`: exit 0.
- `validation-report.json`: `passed=true`, 20 images, 20 labels, 6 negatives, 29 objects, 0 errors, 0 warnings, 20 overlays.

## Next task

Task 12 builds deterministic train/validation/test partitions grouped by background ID so visually related synthetic scenes cannot leak across splits.
