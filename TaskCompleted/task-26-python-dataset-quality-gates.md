# Task 26: Mixed-device dataset validation and exact split

## Outcome

Completed the Python quality gates for mixed-device manifests and added exact,
background-isolated image-count assignment. The mixed configuration now requests
4,800 training, 900 validation, and 300 test images explicitly. Legacy configs
without explicit targets retain their original ratio-based behavior.

The accepted source `mixed-device-6000-640-final-0907` was split into
`Python-ModelTraining/datasets/prepared/mixed-device-6000-640-final-0907`.
All 98 source backgrounds are assigned to exactly one partition.

## Verification

- TDD RED: the new exact-count and pre-copy validation tests failed against the
  ratio-based implementation (`2 failed, 14 passed`).
- Focused GREEN: 35 validator/splitter tests passed.
- Full Python suite: 148 tests passed.
- Ruff: all checks passed.
- Source validation: 6,000 images, 6,000 labels, 9,094 objects, zero errors, and
  zero warnings.
- Prepared-data validation: 6,000 readable images and 6,000 valid labels, with
  3,032 Pikachu, 3,031 Charmander, 3,031 Squirtle, and zero errors or warnings.
- Split counts: 4,800 train, 900 validation, and 300 test image/label pairs.
- Background isolation: 79 train, 14 validation, and 5 test background groups;
  zero overlap and 98 unique groups total.
- Real-manifest repeat check: seed 42 produced the same assignment twice.
- Implementation commit: `74ca89c` (`feat: split mixed dataset to exact counts`).
- Independent task review: specification PASS and code quality PASS, with no
  Critical or Important findings.

## Deferred gates

The 100 generated overlays still require full human review with at least 20 from
each capture profile. Training smoke, full training, evaluation, Core ML export,
and iPhone promotion are not part of this task.

Minor follow-ups recorded for final review are additional exact-mode determinism
and invalid/infeasible-target unit-test coverage, plus removal of a pre-existing
tautological leakage assertion. They do not affect the verified split.

## Next task

Complete Task 7 Step 2 by reviewing the required profile-balanced overlays. Then
run Task 5's batch-64 MPS smoke training gate before the full 100-epoch job.
