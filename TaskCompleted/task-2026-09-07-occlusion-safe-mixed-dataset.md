# Occlusion-safe mixed-device dataset gate

## Completed

- Diagnosed seven fully hidden labeled Pokemon in the first balanced 100-overlay review.
- Added a mixed-device-only capture rejection when projected-box intersection covers at
  least 95% of the smaller box; legacy capture behavior remains unchanged.
- Made nonzero invisible-object review findings a hard Python gate failure.
- Generated and reviewed a 100-frame smoke run with zero containment violations.
- Generated `mixed-device-6000-640-occlusion-safe-0907` without deleting or replacing
  any earlier dataset or trained model.
- Validated all 6,000 image/label/manifest triples and reviewed 100 overlays, exactly 20
  from each device profile. Every recorded visual defect count is zero.
- Created the exact 4,800/900/300 background-isolated prepared split with 79/14/5
  disjoint background groups.

## Artifacts

- Generated run: `Python-ModelTraining/datasets/generated/mixed-device-6000-640-occlusion-safe-0907`
- Prepared split: `Python-ModelTraining/datasets/prepared/mixed-device-6000-640-occlusion-safe-0907`
- Unity Edit Mode results: `/tmp/pokemon-occlusion-editmode-4.xml`
- Unity Play Mode results: `/tmp/pokemon-occlusion-playmode.xml`

## Verification

- Unity focused Edit Mode: 18 passed, 0 failed.
- Unity focused standalone Play Mode: 12 passed, 0 failed.
- Python suite: 156 passed.
- Ruff: passed.
- Full dataset validator: passed, zero errors.
- Pairwise projected containment >= 0.95: zero frames.
- Manual balanced review: accepted, 100 overlays, all defect counts zero.
- Prepared counts: train 4,800; validation 900; test 300; no background overlap.

## Next step

Run the physical-batch-64 MPS training smoke gate. Start the full 100-epoch run only if
the smoke finishes without memory or backend errors.
