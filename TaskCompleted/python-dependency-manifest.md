# Python Dependency Manifest

- Status: Completed
- Completed: 2026-08-29

## Outcome

Exported `Python-ModelTraining/requirements.txt` from the authoritative `uv.lock`. It contains exact runtime, training, webcam, and development dependency versions, including platform markers for Linux-only CUDA packages.

## Verification

- `uv export` resolved 71 packages.
- `uv run pytest -q`: 8 tests passed.
- `uv run ruff check .`: all checks passed.

## Reproduction

Regenerate the file from `Python-ModelTraining` with:

```bash
uv export --all-groups --no-hashes --no-emit-project --output-file requirements.txt
```
