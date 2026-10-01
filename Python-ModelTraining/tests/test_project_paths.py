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
