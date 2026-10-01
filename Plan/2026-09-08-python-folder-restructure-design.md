# Python Folder Restructure Design

**Date:** 2026-09-08

**Status:** Approved in chat; awaiting written-spec review

## Purpose

Make `Python-ModelTraining` predictable to navigate by adopting a conventional Python and
machine-learning layout, removing obsolete compatibility paths, and deleting the abandoned
real-world dataset workflow. The migration must preserve both accepted synthetic datasets,
both accepted experiment directories, all trained checkpoints, and unrelated uncommitted work.

## Current Problems

- The importable package physically lives under `app/pokemon_detector`, while
  `src/pokemon_detector` is only a symlink to it.
- Dataset artifacts live under `datasets`, experiment artifacts live under `experiments`, and
  the pretrained `yolo26n.pt` model is loose in the project root.
- Deleted compatibility paths under `data` and `runs` still appear in Git state and active
  ignore rules.
- Active source, tests, configs, shell scripts, Unity integration checks, documentation, and
  retained artifact metadata contain a mixture of old and current path conventions.
- The unused `datasets/real` scaffold and its annotation, validation, evaluation, and controlled
  trial code add concepts that the project will no longer use.

## Goals

- Keep the top-level directory name `Python-ModelTraining` unchanged.
- Use `src/pokemon_detector` as the physical package location, with no compatibility symlink.
- Give source code, tests, configuration, scripts, datasets, runs, and models one obvious home.
- Preserve the useful internal package boundaries: `cli`, `dataset`, `evaluation`, `inference`,
  and `training`.
- Move all retained local artifacts into the new canonical paths without copying their large
  image collections.
- Remove the real-data folder and all code and tests dedicated to the abandoned real-world
  collection, annotation, evaluation, and controlled-trial workflow.
- Keep ordinary webcam inference.
- Make project-owned configuration paths consistently relative to `Python-ModelTraining`.
- Preserve historical records under `Plan` and `TaskCompleted` without rewriting their paths.

## Non-Goals

- Renaming `Python-ModelTraining` or either sibling Unity/Xcode project.
- Flattening the internal `pokemon_detector` package.
- Changing the synthetic-data schema, split algorithm, training behavior, detector behavior,
  or accepted model metrics.
- Reintroducing or migrating any deleted smoke, pilot, or obsolete datasets.
- Rewriting historical implementation plans or completion records.
- Committing large generated datasets, experiment outputs, or model weights to Git.

## Target Structure

```text
Python-ModelTraining/
├── src/
│   └── pokemon_detector/
│       ├── cli/
│       ├── dataset/
│       ├── evaluation/
│       ├── inference/
│       ├── training/
│       └── domain.py
├── tests/
│   ├── dataset/
│   ├── evaluation/
│   ├── inference/
│   └── training/
├── configs/
│   ├── dataset/
│   ├── inference/
│   ├── training/
│   └── classes.yaml
├── scripts/
├── data/
│   ├── generated/
│   └── prepared/
├── runs/
│   └── <experiment-name>/
├── models/
│   └── yolo26n.pt
├── pyproject.toml
├── uv.lock
├── requirements.txt
└── README.md
```

### Directory Responsibilities

- `src/pokemon_detector`: importable application code only.
- `tests`: tests that mirror the package's domain-oriented subpackages.
- `configs`: versioned dataset-split, training, inference, and class settings.
- `scripts`: operating-system entry points that coordinate Python with external tools such as
  Unity.
- `data/generated`: raw Unity output, including images, labels, manifests, and validation
  evidence.
- `data/prepared`: background-isolated train/validation/test splits and their dataset metadata.
- `runs`: one self-contained training/evaluation directory per experiment.
- `models`: pretrained input models; initially `models/yolo26n.pt`.

There is no `data/real` directory in the target structure.

## Canonical Path Mapping

| Current path | Target path |
| --- | --- |
| `app/pokemon_detector` | `src/pokemon_detector` |
| `datasets/generated` | `data/generated` |
| `datasets/prepared` | `data/prepared` |
| `experiments` | `runs` |
| `yolo26n.pt` | `models/yolo26n.pt` |
| `datasets/real` | Removed |
| Existing `src/pokemon_detector` symlink | Removed and replaced by the physical package |

## Removed Real-World Workflow

The implementation will remove these dedicated modules:

- `pokemon_detector.cli.annotate`
- `pokemon_detector.cli.trials`
- `pokemon_detector.evaluation.annotation`
- `pokemon_detector.inference.trials`

It will also remove their dedicated tests and remove real-dataset branches from the dataset
validator, evaluator, and failure-gallery safeguards. The evaluation CLI will no longer accept
`--kind`; evaluation will support only canonical prepared synthetic datasets and will continue
to write `synthetic-report.json` beneath a `synthetic/<split>` output directory. The ordinary
`pokemon_detector.cli.webcam` feature, low-confidence review-frame option, and generic webcam
inference implementation remain supported.

The currently modified `tests/evaluation/test_annotation.py` will be deleted because its entire
feature is being removed. Before deletion, its uncommitted diff will be saved outside the
repository as a temporary recovery patch.

## Path Resolution Contract

- Project-owned paths written in YAML are relative to the `Python-ModelTraining` root. Examples
  are `data/prepared/<run>/dataset.yaml`, `runs`, and `models/yolo26n.pt`.
- A shared project-path helper will find the nearest ancestor containing `pyproject.toml` and
  resolve project-owned relative paths against it. Training and inference configuration loaders
  will use this helper instead of relying on different counts of `..` components.
- Explicit paths supplied by a user on the command line remain relative to the caller's current
  working directory unless absolute.
- Python import names remain unchanged: callers continue to import `pokemon_detector`.
- Generated `dataset.yaml` files continue to contain an absolute canonical dataset root because
  Ultralytics consumes them outside the package's configuration loader.

## Active Reference Scope

The migration will update references in:

- `Python-ModelTraining` source code and tests.
- `pyproject.toml`, `.gitignore`, and all active YAML configuration.
- `scripts/capture_mixed_device.sh`.
- The current root README and `Python-ModelTraining/README.md`.
- Active Unity documentation and path-contract tests that point capture output into the Python
  project.
- Retained operational YAML/JSON metadata whose paths must resolve after the move, including
  dataset roots and experiment configuration paths.

References in `Plan`, `TaskCompleted`, and nested historical Xcode design/plan documents are
explicitly excluded. They remain evidence of the structure used when those records were
written.

## Artifact Preservation and Git Behavior

The retained artifacts are:

- Generated datasets `baseline-3900` and
  `mixed-device-6000-640-occlusion-safe-0907`.
- Prepared datasets with the same two run IDs.
- Experiment directories with the same two run IDs.
- Their checkpoints, evaluation reports, manifests, overlays, and supporting metadata.
- The pretrained `yolo26n.pt` model.

Before moving anything, implementation will record file counts and sizes for each retained
directory and SHA-256 hashes for pretrained and trained `.pt` files. Artifact directories will
be renamed on the same filesystem rather than copied. New `data`, `runs`, and `models` ignore
rules will prevent artifact content from being added to Git. Existing tracked artifact paths
will appear as removals while their moved local replacements remain ignored; this is intentional
artifact untracking, not data loss.

The pre-existing working-tree changes, including the visual-review fix, its tests, split-report
archiving tests, `.gitignore` edits, and already deleted obsolete artifacts, must remain intact.

## Migration Sequence

1. Capture the working-tree state, current fast-test result, retained artifact inventory, and
   checkpoint hashes.
2. Save a temporary recovery patch for modified files that the approved feature deletion will
   remove.
3. Remove the `src/pokemon_detector` symlink, move the physical package from `app` to `src`, and
   update the build configuration.
4. Remove `datasets/real`, the real-world modules, their tests, and real-only branches in shared
   components.
5. Move generated and prepared datasets, experiment directories, and the pretrained model to
   their canonical target paths.
6. Add the shared path-resolution helper and update all active references and operational
   metadata.
7. Run the full verification gates and compare the post-migration inventory with the preflight
   record.

If the migration is interrupted, the explicit source-to-target mapping and preflight inventory
allow each completed directory rename to be identified and reversed without overwriting an
existing path.

## Error Handling

- Refuse an artifact move when both its source and destination exist.
- Refuse to overwrite any existing run or dataset directory.
- Stop before source movement if a retained artifact inventory or checkpoint hash cannot be
  produced.
- Treat mismatched post-move counts or hashes as migration failure and move affected directories
  back before continuing.
- Preserve configuration errors as actionable `ValueError` messages containing the unresolved
  path.
- Do not silently accept the removed `datasets/real`, `data/real`, `experiments`, or `app`
  conventions through compatibility symlinks.

## Verification

The completed migration must satisfy all of these gates:

1. `src/pokemon_detector` is a physical directory and `app` no longer exists.
2. `data/generated`, `data/prepared`, `runs`, and `models/yolo26n.pt` exist; `datasets`,
   `experiments`, and `data/real` do not.
3. Pre/post file counts match for every retained dataset and run.
4. Pre/post SHA-256 values match for every retained `.pt` file.
5. `python -c` inspection confirms `pokemon_detector` imports from `src/pokemon_detector`.
6. Training and webcam configuration tests prove that project-relative paths resolve from the
   Python project root.
7. The remaining CLI help commands load successfully; removed annotation and trial modules are
   not importable.
8. Active-reference searches find no obsolete `app/`, `datasets/`, `experiments/`, real-data,
   or loose-root-model paths outside explicitly excluded historical records.
9. `uv run ruff check .` passes from `Python-ModelTraining`.
10. `uv run pytest -m "not slow" --cov=pokemon_detector --cov-report=term-missing` passes.
11. The Unity capture path contract points to `Python-ModelTraining/data/generated` and its
    targeted Edit Mode test passes.
12. Final Git inspection confirms that unrelated pre-existing modifications were preserved and
    generated artifacts were not added.

## Acceptance Criteria

The work is complete when the target tree is the only active structure, both accepted datasets
and experiments remain byte-for-byte intact for critical model files and count-equivalent for
all other files, current commands use the new paths, the real-world workflow is absent, active
documentation describes only the supported synthetic workflow, and every verification gate
passes.
