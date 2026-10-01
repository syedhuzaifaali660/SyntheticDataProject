# Prefab Unit-Scale ModelHolder Contract

- Status: Completed
- Completed: 2026-08-29
- Commit: `607ea89`

## Outcome

Set every Pokémon prefab's `ModelHolder` scale to `(1,1,1)`, preserved the user-approved rotations and imported model-child scales, and used holder position only to center the rendered bounds on X/Z and ground them at Y=0. The Pokémon retain their natural relative sizes.

## Verification

- The revised regression test failed before grounding because Charmander was `0.0238` units below Y=0.
- The final isolated Unity 6000.3.8f1 edit-mode run passed all 9 import and prefab tests.
- Unity inspection confirmed all three holder rotations were preserved.
- A +Z render confirmed all three Pokémon remain upright and front-facing.
