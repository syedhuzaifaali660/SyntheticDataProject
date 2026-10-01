"""Resolve project-owned paths from the Python project root."""

from pathlib import Path


def find_project_root(start: Path) -> Path:
    """Find the nearest ancestor containing the Python project's pyproject.toml."""
    candidate = start.resolve()
    directory = candidate.parent if candidate.is_file() else candidate
    for parent in (directory, *directory.parents):
        if (parent / "pyproject.toml").is_file():
            return parent
    raise ValueError(f"unable to locate pyproject.toml from: {start}")


def resolve_project_path(value: str | Path, *, start: Path) -> Path:
    """Resolve project-owned relative paths from the discovered project root."""
    path = Path(value)
    if path.is_absolute():
        return path.resolve()
    return (find_project_root(start) / path).resolve()
