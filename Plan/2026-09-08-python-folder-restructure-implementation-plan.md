# Python Folder Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mixed Python layout with one canonical `src`, `data`, `runs`, and `models` structure while preserving accepted synthetic artifacts and removing the abandoned real-world workflow.

**Architecture:** The import package becomes a physical `src/pokemon_detector` package. Project-owned YAML paths resolve from the directory containing `pyproject.toml`, while CLI overrides remain current-working-directory relative. Large local artifacts move by same-filesystem rename into ignored canonical directories.

**Tech Stack:** Python 3.12, uv, Hatchling, pytest, Ruff, PyYAML, Ultralytics YOLO, Bash, Unity Edit Mode tests

**Spec:** `Plan/2026-09-08-python-folder-restructure-design.md`

## Global Constraints

- Keep the top-level `Python-ModelTraining` directory name unchanged.
- Preserve `cli`, `dataset`, `evaluation`, `inference`, and `training` package boundaries.
- Preserve both accepted generated datasets, prepared datasets, experiments, and all `.pt` files.
- Remove `datasets/real` and all annotation, real-evaluation, and controlled-trial functionality.
- Keep ordinary webcam inference and low-confidence frame capture.
- Do not rewrite historical files beneath `Plan`, `TaskCompleted`, or nested Xcode plan directories.
- Do not add large datasets, run outputs, or model weights to Git.
- Preserve all unrelated pre-existing working-tree changes.

---

### Task 1: Capture preflight evidence

**Files:**
- Read: `Python-ModelTraining/data`, `datasets`, `experiments`, `app`, and `src`
- Temporary backup: `/tmp/python-folder-restructure-annotation.patch`

**Interfaces:**
- Consumes: Current working tree and local accepted artifacts.
- Produces: Baseline test result, artifact counts, checkpoint hashes, and a recovery patch for the modified annotation test.

- [ ] **Step 1: Record the focused working-tree state**

Run `git status --short -- Python-ModelTraining .gitignore README.md` and retain the output. Expected: existing visual-review, split-report, `.gitignore`, artifact-cleanup, and annotation-test changes are visible before migration work begins.

- [ ] **Step 2: Save the modified annotation test diff outside the repository**

```bash
git diff --output=/tmp/python-folder-restructure-annotation.patch -- Python-ModelTraining/tests/evaluation/test_annotation.py
test -s /tmp/python-folder-restructure-annotation.patch
```

Expected: the recovery patch exists and is non-empty.

- [ ] **Step 3: Record retained artifact counts and sizes**

Run:

```bash
find Python-ModelTraining/datasets/generated/baseline-3900 Python-ModelTraining/datasets/generated/mixed-device-6000-640-occlusion-safe-0907 -type f | wc -l
find Python-ModelTraining/datasets/prepared/baseline-3900 Python-ModelTraining/datasets/prepared/mixed-device-6000-640-occlusion-safe-0907 -type f | wc -l
find Python-ModelTraining/experiments/baseline-3900 Python-ModelTraining/experiments/mixed-device-6000-640-occlusion-safe-0907 -type f | wc -l
du -sh Python-ModelTraining/datasets/generated Python-ModelTraining/datasets/prepared Python-ModelTraining/experiments
```

Expected current totals:

```text
generated datasets: 20019 files
prepared datasets: 19866 files
experiments: 227 files
```

- [ ] **Step 4: Record every retained PyTorch hash**

```bash
find Python-ModelTraining/experiments -type f -name '*.pt' -exec shasum -a 256 {} \;
shasum -a 256 Python-ModelTraining/yolo26n.pt
```

Expected: every `.pt` file and the pretrained model produce a SHA-256 value.

- [ ] **Step 5: Run the pre-migration Python gate**

```bash
cd Python-ModelTraining
uv run ruff check .
uv run pytest -m "not slow" --cov=pokemon_detector --cov-report=term-missing
```

Expected: record the actual baseline; distinguish any pre-existing failure from migration regressions.

---

### Task 2: Replace the package symlink with a physical src-layout package

**Files:**
- Remove: `Python-ModelTraining/src/pokemon_detector` symlink
- Move: `Python-ModelTraining/app/pokemon_detector` → `Python-ModelTraining/src/pokemon_detector`
- Modify: `Python-ModelTraining/pyproject.toml`
- Create: `Python-ModelTraining/tests/test_project_paths.py`
- Create: `Python-ModelTraining/src/pokemon_detector/project_paths.py`

**Interfaces:**
- Consumes: existing `pokemon_detector` package and Hatchling configuration.
- Produces: physical src-layout package plus `find_project_root(start: Path) -> Path` and `resolve_project_path(value: str | Path, *, start: Path) -> Path`.

- [ ] **Step 1: Write failing project-path tests**

```python
from pathlib import Path

import pytest

from pokemon_detector.project_paths import find_project_root, resolve_project_path


def test_find_project_root_walks_to_pyproject(tmp_path: Path) -> None:
    root = tmp_path / "project"
    nested = root / "configs" / "training"
    nested.mkdir(parents=True)
    (root / "pyproject.toml").write_text("[project]\nname='fixture'\n", encoding="utf-8")
    assert find_project_root(nested) == root.resolve()


def test_resolve_project_path_uses_project_root(tmp_path: Path) -> None:
    root = tmp_path / "project"
    start = root / "configs" / "training" / "train.yaml"
    start.parent.mkdir(parents=True)
    start.write_text("", encoding="utf-8")
    (root / "pyproject.toml").write_text("[project]\nname='fixture'\n", encoding="utf-8")
    assert resolve_project_path("models/yolo26n.pt", start=start) == (
        root / "models" / "yolo26n.pt"
    ).resolve()


def test_find_project_root_rejects_unowned_path(tmp_path: Path) -> None:
    with pytest.raises(ValueError, match="pyproject.toml"):
        find_project_root(tmp_path)
```

- [ ] **Step 2: Move the package and verify the new test fails**

Remove only the known symlink, move the physical package directory to `src`, set Hatchling's package path to `src/pokemon_detector`, then run `uv run pytest tests/test_project_paths.py -v`. Expected: FAIL because `pokemon_detector.project_paths` does not exist.

- [ ] **Step 3: Add the shared project-path helper**

```python
from pathlib import Path


def find_project_root(start: Path) -> Path:
    candidate = start.resolve()
    directory = candidate.parent if candidate.is_file() else candidate
    for parent in (directory, *directory.parents):
        if (parent / "pyproject.toml").is_file():
            return parent
    raise ValueError(f"unable to locate pyproject.toml from: {start}")


def resolve_project_path(value: str | Path, *, start: Path) -> Path:
    path = Path(value)
    if path.is_absolute():
        return path.resolve()
    return (find_project_root(start) / path).resolve()
```

- [ ] **Step 4: Run focused package tests**

```bash
uv run pytest tests/test_project_paths.py tests/test_domain.py -v
uv run python -c "import pathlib, pokemon_detector; print(pathlib.Path(pokemon_detector.__file__).resolve())"
```

Expected: tests pass and the import path contains `/src/pokemon_detector/`.

- [ ] **Step 5: Commit the src-layout package**

```bash
git add Python-ModelTraining/src Python-ModelTraining/app Python-ModelTraining/pyproject.toml Python-ModelTraining/tests/test_project_paths.py
git commit -m "refactor: adopt physical Python src layout"
```

---

### Task 3: Standardize configuration paths

**Files:**
- Modify: `Python-ModelTraining/src/pokemon_detector/training/config.py`
- Modify: `Python-ModelTraining/src/pokemon_detector/training/runner.py`
- Modify: `Python-ModelTraining/src/pokemon_detector/inference/webcam.py`
- Modify: `Python-ModelTraining/tests/training/test_config.py`
- Modify: `Python-ModelTraining/tests/training/test_runner.py`
- Modify: `Python-ModelTraining/tests/inference/test_webcam.py`
- Modify: `Python-ModelTraining/configs/training/*.yaml`
- Modify: `Python-ModelTraining/configs/inference/webcam.yaml`

**Interfaces:**
- Consumes: `resolve_project_path` from Task 2.
- Produces: `TrainingConfig.model: Path`, project-root-relative YAML paths, and current-working-directory-relative explicit CLI overrides.

- [ ] **Step 1: Write failing root-relative configuration tests**

Create a fixture root containing `pyproject.toml`, then add these assertions to the existing configuration tests:

```python
config = TrainingConfig.load(config_path)
assert config.model == (project_dir / "models" / "yolo26n.pt").resolve()
assert config.dataset_yaml == (project_dir / "data" / "prepared" / "example" / "dataset.yaml").resolve()
assert config.runs_dir == (project_dir / "runs").resolve()

webcam = WebcamConfig.load(webcam_config_path)
assert webcam.checkpoint == (
    project_dir / "runs" / "baseline-3900" / "weights" / "best.pt"
).resolve()
assert webcam.low_confidence_directory == (project_dir / "runs" / "webcam-review").resolve()
```

Retain the existing `monkeypatch.chdir(...)` coverage and assert that explicit dataset/run overrides resolve from that working directory.

- [ ] **Step 2: Run focused tests to verify failure**

Run `uv run pytest tests/training/test_config.py tests/training/test_runner.py tests/inference/test_webcam.py -v`. Expected: FAIL on the canonical-path and `TrainingConfig.model` assertions.

- [ ] **Step 3: Use the shared resolver in training and webcam configuration**

Implement these rules:

```python
model = resolve_project_path(payload["model"], start=config_path)
dataset_yaml = (
    resolve_project_path(dataset_value, start=config_path)
    if dataset_override is None
    else Path(dataset_value).resolve()
)
runs_dir = (
    resolve_project_path(runs_value, start=config_path)
    if runs_dir_override is None
    else Path(runs_value).resolve()
)
```

Require `model.name == "yolo26n.pt"`, serialize the resolved model path in `to_dict`, and pass `str(config.model)` to the YOLO constructor.

- [ ] **Step 4: Rewrite active YAML values**

```yaml
model: models/yolo26n.pt
runs_dir: runs
dataset_yaml: data/prepared/<accepted-run>/dataset.yaml
checkpoint: runs/baseline-3900/weights/best.pt
low_confidence_directory: runs/webcam-review
```

- [ ] **Step 5: Run focused tests and Ruff**

```bash
uv run pytest tests/training/test_config.py tests/training/test_runner.py tests/inference/test_webcam.py -v
uv run ruff check src tests
```

Expected: PASS.

- [ ] **Step 6: Commit configuration path standardization**

```bash
git add Python-ModelTraining/src Python-ModelTraining/tests Python-ModelTraining/configs
git commit -m "refactor: standardize Python project paths"
```

---

### Task 4: Remove the real-world dataset workflow

**Files:**
- Delete: `Python-ModelTraining/datasets/real/`
- Delete: `Python-ModelTraining/src/pokemon_detector/cli/annotate.py`
- Delete: `Python-ModelTraining/src/pokemon_detector/cli/trials.py`
- Delete: `Python-ModelTraining/src/pokemon_detector/evaluation/annotation.py`
- Delete: `Python-ModelTraining/src/pokemon_detector/inference/trials.py`
- Delete: `Python-ModelTraining/tests/evaluation/test_annotation.py`
- Delete: `Python-ModelTraining/tests/inference/test_trials.py`
- Modify: `Python-ModelTraining/src/pokemon_detector/dataset/validator.py`
- Modify: `Python-ModelTraining/src/pokemon_detector/evaluation/evaluator.py`
- Modify: `Python-ModelTraining/src/pokemon_detector/evaluation/gallery.py`
- Modify: `Python-ModelTraining/src/pokemon_detector/cli/evaluate.py`
- Modify: `Python-ModelTraining/tests/dataset/test_validator.py`
- Modify: `Python-ModelTraining/tests/evaluation/test_evaluator.py`

**Interfaces:**
- Consumes: existing synthetic validation and evaluation APIs.
- Produces: synthetic-only validation and evaluation with no annotation or controlled-trial entry points.

- [ ] **Step 1: Replace real-workflow tests with synthetic-only rejection tests**

Add these focused assertions, using the existing `write_dataset_yaml` and fake-YOLO helpers:

```python
def test_validate_run_without_generated_metadata_is_invalid(tmp_path: Path) -> None:
    run_dir = tmp_path / "run"
    run_dir.mkdir()

    report = validate_run(run_dir)

    assert not report.passed
    assert report.errors[0].code == "INVALID_MANIFEST"


def test_evaluate_dataset_rejects_path_outside_data_prepared(tmp_path: Path) -> None:
    dataset_yaml = write_dataset_yaml(tmp_path / "scratch" / "run")
    checkpoint = tmp_path / "best.pt"
    checkpoint.write_text("weights", encoding="utf-8")

    with pytest.raises(ValueError, match="data/prepared"):
        evaluate_dataset(
            checkpoint,
            dataset_yaml,
            "val",
            yolo_factory=lambda _: pytest.fail("must not construct YOLO"),
        )
```

Update remaining evaluation assertions so every report kind and filename is `synthetic` without a requested-kind argument.

- [ ] **Step 2: Run focused tests to verify failure**

Run `uv run pytest tests/dataset/test_validator.py tests/evaluation/test_evaluator.py -v`. Expected: FAIL while implicit real handling and `requested_kind` remain.

- [ ] **Step 3: Remove dedicated modules, tests, and the empty real scaffold**

Delete only the listed files. Remove now-empty directories under `datasets/real` without touching `datasets/generated` or `datasets/prepared`.

- [ ] **Step 4: Simplify shared validation and evaluation code**

- Missing `manifest.jsonl` or `run-config.json` produces `INVALID_MANIFEST` rather than invoking a real-data validator.
- `_infer_kind` becomes a synthetic provenance check requiring the `data/prepared` path pair.
- `evaluate_dataset` removes `requested_kind` and always writes the synthetic report form.
- The evaluation CLI removes `--kind`.
- The gallery removes the real-training-directory special case while keeping ordinary split-isolation safeguards.

- [ ] **Step 5: Run all Python tests and Ruff**

```bash
uv run ruff check src tests
uv run pytest -m "not slow" --cov=pokemon_detector --cov-report=term-missing
```

Expected: PASS with no collection references to deleted modules.

- [ ] **Step 6: Commit workflow removal**

```bash
git add -A Python-ModelTraining/src Python-ModelTraining/tests Python-ModelTraining/datasets/real
git commit -m "refactor: remove unused real dataset workflow"
```

---

### Task 5: Move retained artifacts into canonical directories

**Files:**
- Move: `Python-ModelTraining/datasets/generated` → `Python-ModelTraining/data/generated`
- Move: `Python-ModelTraining/datasets/prepared` → `Python-ModelTraining/data/prepared`
- Move: `Python-ModelTraining/experiments` → `Python-ModelTraining/runs`
- Move: `Python-ModelTraining/yolo26n.pt` → `Python-ModelTraining/models/yolo26n.pt`
- Modify: retained operational YAML/JSON metadata containing obsolete paths
- Modify: `.gitignore`

**Interfaces:**
- Consumes: preflight counts and SHA-256 values from Task 1.
- Produces: canonical ignored local artifacts with operational metadata pointing to existing paths.

- [ ] **Step 1: Check every destination before moving**

Run explicit `test ! -e` checks for `data`, `runs`, and `models/yolo26n.pt`. Refuse to continue if both a mapped source and destination exist.

- [ ] **Step 2: Update ignore rules before artifact movement**

Replace obsolete artifact rules with:

```gitignore
Python-ModelTraining/data/generated/
Python-ModelTraining/data/prepared/
Python-ModelTraining/runs/
Python-ModelTraining/models/*.pt
```

- [ ] **Step 3: Rename artifact directories on the same filesystem**

Create only the required parent directories, then use direct `mv` operations for the four canonical mappings. Do not copy, merge, overwrite, or delete the accepted artifact trees.

- [ ] **Step 4: Repair retained operational metadata**

Update path fields in retained `dataset.yaml`, `resolved-config.yaml`, `args.yaml`, run configuration, and report files from the previous names to `data`, `runs`, and `models`. Do not rewrite historical Markdown records.

- [ ] **Step 5: Verify counts and hashes immediately**

Repeat Task 1's count, size, and SHA-256 commands against target paths. Expected: counts and every `.pt` hash exactly match preflight evidence.

- [ ] **Step 6: Verify ignored artifact behavior**

```bash
git check-ignore Python-ModelTraining/data/generated/baseline-3900/images/frame_000001.png
git check-ignore Python-ModelTraining/runs/baseline-3900/weights/best.pt
git check-ignore Python-ModelTraining/models/yolo26n.pt
```

Expected: all identify ignored files, and `git status` shows no added image or model artifacts.

- [ ] **Step 7: Commit canonical ignore and tracked artifact cleanup**

Stage `.gitignore` plus tracked removals from obsolete artifact locations, inspect the staged file list, then commit with `git commit -m "chore: move Python artifacts to canonical paths"`.

---

### Task 6: Update active integrations and documentation

**Files:**
- Modify: `Python-ModelTraining/scripts/capture_mixed_device.sh`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/README.md`
- Modify: `Unity-SyntheticDataGenrator/Assets/SyntheticData/Tests/EditMode/CaptureRunRequestValidationTests.cs`
- Modify: `Python-ModelTraining/README.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: final canonical paths from Task 5.
- Produces: active commands and Unity capture integration using `data/generated`, `data/prepared`, `runs`, and `models`.

- [ ] **Step 1: Update the Unity capture contract test first**

```csharp
Does.EndWith("/Python-ModelTraining/data/generated")
```

- [ ] **Step 2: Update capture script and active documentation**

Use `Python-ModelTraining/data/generated` as the Unity output root. Rewrite active commands and artifact lists to canonical paths, remove annotation/real-evaluation/trials sections, and retain ordinary webcam instructions.

- [ ] **Step 3: Search active files for obsolete references**

Search active source, tests, configs, scripts, root README, Python README, and active Unity files for `app/pokemon_detector`, `datasets/generated`, `datasets/prepared`, `datasets/real`, `experiments/`, `data/real`, `pokemon_detector.cli.annotate`, and `pokemon_detector.cli.trials`. Expected: no matches; historical directories are excluded.

- [ ] **Step 4: Run the targeted Unity Edit Mode path test**

Run the repository's documented Unity Edit Mode command filtered to `CaptureRunRequestValidationTests`. Expected: PASS.

- [ ] **Step 5: Commit active integration and documentation changes**

```bash
git add README.md Python-ModelTraining/README.md Python-ModelTraining/scripts Unity-SyntheticDataGenrator/Assets/SyntheticData
git commit -m "docs: align active workflows with Python layout"
```

---

### Task 7: Complete end-to-end verification

**Files:**
- Verify: active `Python-ModelTraining` project, Unity capture integration, and Git working tree
- Create: `TaskCompleted/task-2026-09-08-python-folder-restructure.md`

**Interfaces:**
- Consumes: completed Tasks 1–6.
- Produces: final evidence that the structure works and all retained artifacts survived.

- [ ] **Step 1: Verify the filesystem contract**

Assert that `src/pokemon_detector`, `data/generated`, `data/prepared`, `runs`, and `models/yolo26n.pt` exist. Assert that `app`, `datasets`, `experiments`, `data/real`, and the former symlink do not.

- [ ] **Step 2: Verify imports and remaining CLI entry points**

```bash
cd Python-ModelTraining
uv run python -c "import pathlib, pokemon_detector; assert '/src/pokemon_detector/' in pathlib.Path(pokemon_detector.__file__).resolve().as_posix()"
uv run python -m pokemon_detector.cli.validate --help
uv run python -m pokemon_detector.cli.split --help
uv run python -m pokemon_detector.cli.train --help
uv run python -m pokemon_detector.cli.evaluate --help
uv run python -m pokemon_detector.cli.webcam --help
```

Expected: every command exits zero.

- [ ] **Step 3: Run final Python quality gates**

```bash
uv run ruff check .
uv run pytest -m "not slow" --cov=pokemon_detector --cov-report=term-missing
```

Expected: PASS.

- [ ] **Step 4: Repeat active-reference, count, and hash gates**

Expected: no obsolete active references; accepted artifact counts and every `.pt` hash match Task 1.

- [ ] **Step 5: Inspect final Git state**

Confirm no ignored artifact was added, no historical record was rewritten, and unrelated pre-existing changes remain represented or were deliberately incorporated without loss.

- [ ] **Step 6: Record and commit completion**

Write the final tree, exact verification results, artifact-preservation evidence, and implementation commit IDs to the completion record, then commit only that record with `git commit -m "docs: record Python folder restructure"`.
