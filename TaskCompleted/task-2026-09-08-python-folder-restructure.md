# Python folder restructure

## Completed

- Kept the top-level `Python-ModelTraining` directory name unchanged.
- Replaced the `src/pokemon_detector` symlink and duplicate `app` hierarchy with one
  physical `src/pokemon_detector` package.
- Standardized project-owned configuration paths around `pyproject.toml`; explicit CLI
  overrides remain relative to the caller's current working directory.
- Consolidated retained artifacts under `data/generated`, `data/prepared`, `runs`, and
  `models` without copying or merging the accepted artifact trees.
- Repaired active YAML, JSON, Bash, Python, Unity, and Markdown references to the new
  paths. Historical records under `Plan`, `TaskCompleted`, and nested Xcode planning
  directories were not rewritten.
- Removed `datasets/real` and the abandoned annotation, real-evaluation, and controlled
  trial modules, CLIs, tests, and documentation. Ordinary webcam inference and optional
  low-confidence frame capture remain available.
- Added shared project-path tests and a regression test for unreadable images when an
  optional Pillow image plugin is unavailable.

## Final structure

```text
Python-ModelTraining/
├── configs/
│   ├── dataset/
│   ├── inference/
│   └── training/
├── data/
│   ├── generated/
│   └── prepared/
├── models/
│   └── yolo26n.pt
├── runs/
├── scripts/
├── src/
│   └── pokemon_detector/
│       ├── cli/
│       ├── dataset/
│       ├── evaluation/
│       ├── inference/
│       ├── training/
│       ├── domain.py
│       └── project_paths.py
├── tests/
│   ├── dataset/
│   ├── evaluation/
│   ├── inference/
│   └── training/
├── pyproject.toml
├── requirements.txt
└── uv.lock
```

## Artifact preservation

- Retained generated dataset files: 20,019.
- Retained prepared dataset files: 19,866.
- Retained run files: 227.
- Approximate retained sizes: generated 5.6 GB, prepared 5.5 GB, runs 150 MB.
- Pretrained model SHA-256:
  `9b09cc8bf347f0fc8a5f7657480587f25db09b34bf33b0652110fb03a8ad4fef`.
- Baseline `last.pt` SHA-256:
  `3d55ec804b581857c63b9dcc31313cca99a224c248e336bb7a01d50a764f801b`.
- Baseline `best.pt` SHA-256:
  `90efa949b98fcbb09749d1bf76de0c9702cb701777aea9b7bfb50ea022bd00ff`.
- Mixed-device `last.pt` SHA-256:
  `140f8b794282614ef8ae959ad03604630b362be136b3b95e84365f7d29125e42`.
- Mixed-device `best.pt` SHA-256:
  `cf85e32b7246cde114b01a2d35b3811518e54926c101bb384f73fd206c1a24c1`.

All retained counts and hashes exactly match the pre-migration evidence. The generated,
prepared, run, and model artifact locations are ignored by Git.

## Verification

- Full Ruff check: passed.
- Full non-slow Python suite: 143 passed with 87% total coverage.
- Physical package import: resolved from `src/pokemon_detector/__init__.py`.
- Remaining CLI smoke checks: validate, split, train, evaluate, and webcam all exited 0.
- Removed-module smoke check: annotation and controlled-trial modules are unavailable.
- Active obsolete-path scan: no matches in Python source, tests, configs, scripts,
  active READMEs, or the Unity capture integration.
- Operational training and webcam configurations resolve to existing canonical model,
  dataset, run, and checkpoint paths.
- Git whitespace check: passed.

## Unity verification limitation

The Unity production default and its Edit Mode assertion both target
`Python-ModelTraining/data/generated`. The targeted Edit Mode test was attempted twice,
but this host's Unity Licensing Client never exposed its IPC channel; Unity stopped before
test discovery with `Licensing initialization failed` and
`com.unity.editor.headless was not found`. The runner did not execute any Unity assertion
or produce an XML test report. Re-run `CaptureRunRequestValidationTests` after the local
Unity license is available.

## Preservation notes

- Existing unrelated dirty-working-tree changes were left in place and were not bundled
  into an implementation commit.
- The pre-existing modified annotation-test diff was saved before its approved removal at
  `/tmp/python-folder-restructure-annotation.patch`.
- Design commit: `7bd67d6`.
- Implementation-plan commit: `0fdc3c8`.
