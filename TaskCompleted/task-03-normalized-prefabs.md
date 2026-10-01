# Task 3: Normalized Labeled Pokémon Prefabs

- Status: Completed
- Completed: 2026-08-29
- Commits: `432d25a`, `6c3179c`

## Outcome

Created the runtime Pokémon class contract and label component, then built textured prefabs for Pikachu, Charmander, and Squirtle. Each model keeps its model-specific scale and forward rotation under a unit-scale `ModelHolder`; rendered bounds are centered on X/Z and grounded at Y=0.

## Verification

- Class IDs are stable: `0=pikachu`, `1=charmander`, `2=squirtle`.
- Renderers are enabled and materials are assigned.
- The instantiated-bounds regression failed for all three pre-fix prefabs, then passed after the correction.
- An isolated Unity 6000.3.8f1 edit-mode run passed all 9 import and prefab tests.
- A +Z camera render confirmed all three models are upright and front-facing.
