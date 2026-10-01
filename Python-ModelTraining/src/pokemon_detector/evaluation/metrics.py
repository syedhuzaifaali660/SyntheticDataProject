"""Immutable class-aware detection outcome metrics."""

from dataclasses import dataclass

from pokemon_detector.domain import PokemonClass


@dataclass(frozen=True, slots=True)
class DetectionOutcome:
    true_class_id: int | None
    predicted_class_id: int | None

    def __post_init__(self) -> None:
        for class_id in (self.true_class_id, self.predicted_class_id):
            if class_id is not None:
                PokemonClass(class_id)
        if self.true_class_id is None and self.predicted_class_id is None:
            raise ValueError("an outcome requires a true or predicted class")


@dataclass(frozen=True, slots=True)
class ClassMetric:
    class_id: int
    true_positives: int
    false_positives: int
    false_negatives: int

    @property
    def precision(self) -> float:
        denominator = self.true_positives + self.false_positives
        return self.true_positives / denominator if denominator else 0.0

    @property
    def recall(self) -> float:
        denominator = self.true_positives + self.false_negatives
        return self.true_positives / denominator if denominator else 0.0

    @property
    def f1(self) -> float:
        denominator = self.precision + self.recall
        return 2 * self.precision * self.recall / denominator if denominator else 0.0

    def to_dict(self) -> dict[str, int | float | str]:
        return {
            "class_id": self.class_id,
            "class_name": PokemonClass(self.class_id).name.lower(),
            "precision": self.precision,
            "recall": self.recall,
            "f1": self.f1,
            "false_positives": self.false_positives,
            "false_negatives": self.false_negatives,
        }


def summarize_outcomes(outcomes: list[DetectionOutcome]) -> dict[int, ClassMetric]:
    """Summarize class-aware outcomes across the immutable detector taxonomy."""
    counts = {class_id: [0, 0, 0] for class_id in PokemonClass}
    for outcome in outcomes:
        if outcome.true_class_id == outcome.predicted_class_id and outcome.true_class_id is not None:
            counts[outcome.true_class_id][0] += 1
        else:
            if outcome.true_class_id is not None:
                counts[outcome.true_class_id][2] += 1
            if outcome.predicted_class_id is not None:
                counts[outcome.predicted_class_id][1] += 1
    return {
        int(class_id): ClassMetric(int(class_id), true_positives, false_positives, false_negatives)
        for class_id, (true_positives, false_positives, false_negatives) in counts.items()
    }
