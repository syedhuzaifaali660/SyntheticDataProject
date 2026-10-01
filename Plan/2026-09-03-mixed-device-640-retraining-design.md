# Mixed-Device 640 Retraining Design

**Date:** 2026-09-03  
**Status:** Approved in conversation  
**Workspace:** `/Users/syedhuzaifaali/Desktop/Projects/SyntheticDataProject`

## Goal

Generate a new 6,000-image synthetic dataset that represents iPhone portrait and landscape cameras, laptop displays, and webcams; retrain YOLO26n at a fixed `640 x 640` model input with physical batch size 64 for 100 epochs; export the winning checkpoint to Core ML; and keep iOS box placement consistent by using the same letterbox geometry as training.

## Important Resolution Distinction

Unity capture resolution and YOLO model input resolution are separate settings.

- Unity saves native rectangular images from several device-oriented profiles.
- Ultralytics letterboxes every training image to a `640 x 640` tensor without stretching it.
- The Core ML model keeps a fixed `640 x 640` input.
- The iOS app letterboxes the complete camera frame to `640 x 640` and maps detections through the inverse letterbox transform.

This preserves portrait and landscape geometry while retaining a fast mobile-sized model. Randomly stretching rectangular images to a square is prohibited.

## Selected Approach

Use one mixed-aspect dataset and one detector. Separate models per device category would increase maintenance without addressing the underlying domain gap, while square-only generation would continue discarding important portrait and landscape composition.

## Dataset Size and Split

The new run ID is `mixed-device-6000-640` and contains exactly 6,000 successfully validated frames.

| Partition | Frames | Rule |
|---|---:|---|
| Train | 4,800 | Background-group isolated |
| Validation | 900 | Background-group isolated |
| Test | 300 | Background-group isolated |

No background asset may occur in more than one partition. The existing immutable class mapping remains `0=pikachu`, `1=charmander`, and `2=squirtle`.

## Unity Capture Profiles

The sampler chooses a device category by weight and then chooses one of its resolutions uniformly. Resolutions are intentionally scaled representations of common aspect ratios; exact physical iPhone pixel counts are unnecessary because every sample is ultimately letterboxed to 640 for training.

| Profile | Dataset weight | Native resolutions |
|---|---:|---|
| Square | 15% | `640x640` |
| iPhone portrait, 19.5:9 | 25% | `360x780`, `540x1170`, `720x1560` |
| iPhone landscape, 19.5:9 | 20% | `780x360`, `1170x540`, `1560x720` |
| Webcam | 20% | `640x480`, `960x540`, `1280x720` |
| Laptop window, 16:10 | 20% | `640x400`, `960x600`, `1280x800` |

The profile controls output width, output height, camera aspect, and metadata. The camera field-of-view continues to vary independently within its configured safe range. Profile selection and resolution selection must remain deterministic for a given global seed, frame index, and retry index.

## Object Size Diversity

Object diversity is defined by the final visible box size after projecting the object and applying the same 640-letterbox transform used for training. This is more reliable than relying only on world-space scale or distance.

| Size band | Dataset-object weight | Longest box side at 640 |
|---|---:|---:|
| Small/distant | 30% | 8-72 pixels (user-approved September 3 revision) |
| Medium | 50% | >72-192 pixels |
| Large/close | 20% | 193-448 pixels |

September 7 correction: profile, resolution, lighting band, and size bands are selected once per frame and retained across retries. Unity measures the actual rotated mesh, solves root scale to reach the selected model-pixel size, and clamps its projected center using both profile margins and the full projected box extents. This replaces guessing world scales and rejecting mismatches. Targets use an interior portion of each allowed band; large objects are limited by the shorter native viewport dimension. The frame retry limit stays at 20. The projector requires both native box axes to be at least 8 pixels for mixed captures, and the final longest-side gate is in 640-model pixels. These are explicitly different units. Prefab `ModelHolder` transforms remain unchanged; variation is applied only to the instantiated prefab root.

## Lighting and Camera Appearance Diversity

Lighting uses explicit strata so uniform random sampling does not accidentally produce mostly average scenes.

| Lighting band | Frame weight | Ambient intensity | Point intensity |
|---|---:|---:|---:|
| Low light | 20% | 0.15-0.45 | 0.15-0.80 |
| Normal | 60% | 0.45-1.10 | 0.50-2.00 |
| Bright | 20% | 0.90-1.50 | 1.50-3.50 |

Light temperature varies from 2,800 K to 8,500 K. Background brightness varies from 0.55 to 1.35, contrast from 0.70 to 1.30, and blur from 0 to 2 pixels. The renderer may add mild full-frame exposure, sensor noise, and motion/defocus blur, but these effects must remain bounded so Pokémon identity and label visibility are preserved. Pokémon meshes are never recolored or deformed.

## Manifest Contract

Every `manifest.jsonl` record adds:

- `capture_profile`
- `source_width` and `source_height`
- `source_aspect_ratio`
- `letterbox_scale`
- `letterbox_pad_x` and `letterbox_pad_y`
- `lighting_band`
- per-object `size_band`
- per-object projected box dimensions at the 640 model input

Existing fields remain backward compatible. Python readers must accept older manifests that do not contain the new optional fields.

## Dataset Validation Gates

Training is blocked until all of the following pass:

- exactly 6,000 readable image/label pairs;
- no missing pairs, invalid class IDs, malformed boxes, or out-of-range coordinates;
- no background-group leakage across partitions;
- capture-profile frame counts within 3 percentage points of their target weights;
- object-size counts within 5 percentage points of `30/50/20`;
- lighting-band frame counts within 3 percentage points of `20/60/20`;
- per-class object counts within 5% of one another;
- at least 100 reviewed overlays, with at least 20 from each capture profile;
- explicit review of small objects, frame edges, portrait samples, landscape samples, low-light samples, and close-up boxes.

## Training Configuration

The primary retraining run uses:

```yaml
model: yolo26n.pt
epochs: 100
patience: 20
image_size: 640
batch: 64
workers: 0
device: mps
seed: 42
```

Ultralytics performs aspect-preserving letterboxing to 640. Training must not resize by independent X and Y scale factors. The run directory stores the resolved arguments, environment details, metrics, plots, and `best.pt`.

If physical batch 64 fails due to MPS memory pressure, that run stops and records the failure. Reducing the physical batch is a deliberate plan revision, not a silent automatic change.

## Evaluation and Promotion Gate

The new model is compared with `runs/baseline-3900/weights/best.pt` on:

- the untouched synthetic test partition;
- the existing real iPhone validation images;
- additional user-supplied portrait and landscape iPhone frames;
- small/distant, medium, and close object subsets;
- low, normal, and bright lighting subsets;
- per-class precision, recall, mAP50, and mAP50-95;
- box placement overlays and failure galleries.

The model is promoted only if it improves real iPhone detection without a material regression on any Pokémon class. A high synthetic score alone is insufficient.

## Core ML and iOS Integration

Export the promoted checkpoint with a fixed `640 x 640` Core ML image input. The iOS app must use an aspect-preserving 640 letterbox with the same pad convention as Ultralytics, then invert scale and padding when mapping model boxes to camera coordinates. Portrait and landscape box mapping receive standalone geometry tests. The inference FPS badge remains enabled so the new model can be compared with the current baseline on the same iPhone.

## Testing Strategy

Unity edit-mode tests cover deterministic profile selection, all allowed resolution variants, configuration validation, size/lighting strata, manifest serialization, and letterbox box geometry. Play-mode smoke tests capture at least one valid image from every profile and verify its actual dimensions and label bounds.

Python tests cover optional manifest-field compatibility, distribution validation, exact split counts, training configuration loading, and the batch-64/640 contract. A small training smoke test runs before the full 100-epoch job.

iOS standalone Swift tests cover camera-to-letterbox and letterbox-to-preview mapping in portrait and landscape. The complete simulator target must build before the Core ML package is replaced, followed by physical-device testing.

## Expected Resource Use

Six thousand PNG files at mixed resolutions may consume several gigabytes. The full Mac training run is expected to take roughly five to six hours at 640, depending on augmentation throughput and whether batch 64 improves MPS utilization. Unity generation time and Core ML export are additional.

## Out of Scope

- Increasing the model input to 960 in this round.
- Training separate portrait and landscape models.
- Changing from YOLO26n to a larger model family.
- Adding new Pokémon classes.
- Treating images displayed on a laptop screen as a substitute for untouched real-world evaluation.
