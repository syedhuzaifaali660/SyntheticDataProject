# Synthetic Pokémon Detector

A computer vision project for generating synthetic training data, training a YOLO detector, and running live Pokémon detection on iOS.

The project combines a Unity synthetic data generator, a Python training and evaluation pipeline, and a native iOS camera app. The Unity and Python projects exchange labeled images and metadata through a documented dataset format.

## Project components

- **Unity generator** (`Unity-SyntheticDataGenrator/`) creates rendered images, YOLO bounding-box labels, a JSON Lines manifest, and a resolved run configuration. See the [Unity operating guide](Unity-SyntheticDataGenrator/Assets/SyntheticData/README.md).
- **Python pipeline** (`Python-ModelTraining/`) validates captures, reviews annotations, creates background-grouped dataset splits, trains and evaluates YOLO26n, and supports webcam inference. See the [Python operating guide](Python-ModelTraining/README.md).
- **iOS app** (`Xcode-PokemonDetector/PokemonDetector/`) runs Core ML object detection using the device camera. Its source and two trained Core ML model packages are included.
- **Project records** (`Plan/`, `TaskCompleted/`) contain design notes, implementation plans, and completed work records.

## Getting started

### Generate data

Open `Unity-SyntheticDataGenrator` with Unity **6000.3.8f1** and follow the [Unity guide](Unity-SyntheticDataGenrator/Assets/SyntheticData/README.md) to generate a new run. The guide covers editor and batch-mode capture, validation, and Unity tests.

### Train and evaluate

The Python project requires Python **3.12** and [`uv`](https://docs.astral.sh/uv/).

```bash
cd Python-ModelTraining
uv sync
uv run python -m pokemon_detector.cli.validate --help
```

For the complete workflow, including annotation review, dataset splitting, training, evaluation, and webcam inference, see the [Python operating guide](Python-ModelTraining/README.md).

### Run the iOS app

On macOS, open `Xcode-PokemonDetector/PokemonDetector/PokemonDetector.xcodeproj` in Xcode. The app's trained Core ML models are included; allow camera access when prompted. The iOS app's unit tests are in `Xcode-PokemonDetector/PokemonDetector/Tests/`.

## Local-only assets

Images, Unity background and 3D model assets, Python training weights, and training datasets are intentionally excluded from Git. The two trained iOS Core ML packages (`best.mlpackage` and `MixedDevice6000.mlpackage`) are included as model backups. The Unity capture scene expects compatible local assets at its configured paths; provide those files from sources you are permitted to use before running it. Generated datasets and other model artifacts should stay local or use a separate artifact store.

## Dataset and model artifacts

Generated datasets, training runs, checkpoints, and evaluation images can be very large. They are reproducible outputs and should normally be stored outside Git or in a dedicated artifact store. Follow the operating guides to generate the data and models locally.

## Public release notes

This project uses Pokémon names, characters, and related 3D assets. Pokémon is a third-party intellectual property; review the rights and licenses for the character models, backgrounds, textures, dependencies, and any included model files before redistributing them publicly. A public repository does not grant rights to those materials.

No project license is declared yet. Add a `LICENSE` file that matches your intended reuse terms before inviting contributions or presenting the code as open source. Third-party assets may have separate terms.
