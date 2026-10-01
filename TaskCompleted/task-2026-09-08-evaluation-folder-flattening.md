# Evaluation folder flattening

## Completed

- Removed the redundant `synthetic` directory from generated evaluation output.
- Made a checkpoint run's normal output default to `evaluation/<split>`.
- Standardized output names as `report.json`, `confusion-matrix.png`,
  `confusion-matrix-normalized.png`, and `failures/`.
- Organized comparisons under `evaluation/comparisons/<model>/<split>`.
- Migrated the accepted baseline and mixed-device evaluation artifacts without changing
  their contents.
- Updated active root and Python documentation to the new report locations.

## Resulting structure

```text
runs/mixed-device-6000-640-occlusion-safe-0907/evaluation/
├── test/
│   ├── report.json
│   ├── confusion-matrix.png
│   ├── confusion-matrix-normalized.png
│   └── failures/
├── comparisons/
│   └── baseline/
│       └── test/
│           ├── report.json
│           ├── confusion-matrix.png
│           ├── confusion-matrix-normalized.png
│           └── failures/
└── profile-comparison.json
```

The baseline run now uses the same direct `evaluation/test` structure. Its existing
`failure-analysis.json` and `failures/review-sheets/` artifacts were retained.

## Preservation evidence

- Mixed-device current-model evaluation: 54 files before and after; aggregate content
  hash `083aec339220ddd914823c47202c5348211b9ed23e56fb7a13cc418dbea960ff`.
- Mixed-device baseline comparison: 54 files before and after; aggregate content hash
  `681d67ea31b80b3781e443c45ccb078aff8eff43c9772cb6f44c7a161969cd27`.
- Baseline-run evaluation: 60 files before and after; aggregate content hash
  `33e89d1dcee61dde5737b7a5a0ab3d24a0734203d565ae214a9e371d4bdaff9b`.

## Verification

- TDD path-contract check: two expected failures against the former nested layout.
- Focused evaluator suite: 20 passed.
- Full Ruff check: passed.
- Full non-slow Python suite: 144 passed with 87% total coverage.
- Active obsolete-name scan: no `evaluation/synthetic`, `synthetic-report.json`,
  `failure-gallery`, `new-model-on-occlusion-safe`, or `baseline-on-occlusion-safe`
  references remain outside historical records.
