from pokemon_detector.evaluation.metrics import DetectionOutcome, summarize_outcomes


def test_summarize_outcomes_reports_all_fixed_classes_and_zero_prediction_precision() -> None:
    report = summarize_outcomes(
        [
            DetectionOutcome(true_class_id=0, predicted_class_id=0),
            DetectionOutcome(true_class_id=0, predicted_class_id=None),
            DetectionOutcome(true_class_id=1, predicted_class_id=0),
            DetectionOutcome(true_class_id=2, predicted_class_id=None),
        ]
    )

    assert report[0].to_dict() == {
        "class_id": 0,
        "class_name": "pikachu",
        "precision": 0.5,
        "recall": 0.5,
        "f1": 0.5,
        "false_positives": 1,
        "false_negatives": 1,
    }
    assert report[1].to_dict() == {
        "class_id": 1,
        "class_name": "charmander",
        "precision": 0.0,
        "recall": 0.0,
        "f1": 0.0,
        "false_positives": 0,
        "false_negatives": 1,
    }
    assert report[2].to_dict() == {
        "class_id": 2,
        "class_name": "squirtle",
        "precision": 0.0,
        "recall": 0.0,
        "f1": 0.0,
        "false_positives": 0,
        "false_negatives": 1,
    }
