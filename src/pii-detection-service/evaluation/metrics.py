"""Pure metric functions for PII detection evaluation."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass
class EntitySpan:
    """A labelled entity span."""

    entity_type: str
    start: int
    end: int
    text: str = ""


def spans_overlap(a: EntitySpan, b: EntitySpan, min_overlap_ratio: float = 0.5) -> bool:
    """Check if two spans overlap by at least min_overlap_ratio of the shorter span."""
    overlap_start = max(a.start, b.start)
    overlap_end = min(a.end, b.end)
    overlap_len = max(0, overlap_end - overlap_start)

    shorter = min(a.end - a.start, b.end - b.start)
    if shorter == 0:
        return False

    return overlap_len / shorter >= min_overlap_ratio


def match_entities(
    gold: list[EntitySpan],
    predicted: list[EntitySpan],
    min_overlap_ratio: float = 0.5,
) -> tuple[int, int, int]:
    """Match gold and predicted entities with overlap-based matching.

    Returns (true_positives, false_positives, false_negatives).
    A predicted entity is a TP if it overlaps with a gold entity of the same type.
    """
    matched_gold: set[int] = set()
    matched_pred: set[int] = set()

    for pi, pred in enumerate(predicted):
        for gi, gold_ent in enumerate(gold):
            if gi in matched_gold:
                continue
            if pred.entity_type == gold_ent.entity_type and spans_overlap(pred, gold_ent, min_overlap_ratio):
                matched_gold.add(gi)
                matched_pred.add(pi)
                break

    tp = len(matched_pred)
    fp = len(predicted) - tp
    fn = len(gold) - len(matched_gold)

    return tp, fp, fn


def precision(tp: int, fp: int) -> float:
    """Compute precision."""
    return tp / (tp + fp) if (tp + fp) > 0 else 0.0


def recall(tp: int, fn: int) -> float:
    """Compute recall."""
    return tp / (tp + fn) if (tp + fn) > 0 else 0.0


def f_score(prec: float, rec: float, beta: float = 1.0) -> float:
    """Compute F-beta score."""
    if prec + rec == 0:
        return 0.0
    b2 = beta * beta
    return (1 + b2) * prec * rec / (b2 * prec + rec)


@dataclass
class EvalMetrics:
    entity_type: str
    tp: int
    fp: int
    fn: int
    precision: float
    recall: float
    f1: float
    f2: float


def compute_per_type_metrics(
    gold: list[EntitySpan],
    predicted: list[EntitySpan],
) -> list[EvalMetrics]:
    """Compute precision/recall/F1/F2 per entity type."""
    all_types = sorted(set(e.entity_type for e in gold + predicted))
    results: list[EvalMetrics] = []

    for etype in all_types:
        gold_of_type = [e for e in gold if e.entity_type == etype]
        pred_of_type = [e for e in predicted if e.entity_type == etype]
        tp, fp, fn = match_entities(gold_of_type, pred_of_type)
        p = precision(tp, fp)
        r = recall(tp, fn)
        results.append(
            EvalMetrics(
                entity_type=etype,
                tp=tp,
                fp=fp,
                fn=fn,
                precision=round(p, 4),
                recall=round(r, 4),
                f1=round(f_score(p, r, beta=1.0), 4),
                f2=round(f_score(p, r, beta=2.0), 4),
            )
        )

    return results


def format_metrics_table(metrics: list[EvalMetrics]) -> str:
    """Format metrics as a markdown table."""
    lines = [
        "| Entity Type | TP | FP | FN | Precision | Recall | F1 | F2 |",
        "|-------------|----|----|-----|-----------|--------|------|------|",
    ]
    total_tp = total_fp = total_fn = 0
    for m in metrics:
        lines.append(
            f"| {m.entity_type:<12} | {m.tp:>2} | {m.fp:>2} | {m.fn:>3} "
            f"| {m.precision:.4f}    | {m.recall:.4f} | {m.f1:.4f} | {m.f2:.4f} |"
        )
        total_tp += m.tp
        total_fp += m.fp
        total_fn += m.fn

    p = precision(total_tp, total_fp)
    r = recall(total_tp, total_fn)
    lines.append(
        f"| **TOTAL**    | {total_tp:>2} | {total_fp:>2} | {total_fn:>3} "
        f"| {p:.4f}    | {r:.4f} | {f_score(p, r):.4f} | {f_score(p, r, 2.0):.4f} |"
    )

    return "\n".join(lines)
