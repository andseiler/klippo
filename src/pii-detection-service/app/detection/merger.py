"""Merge and conflict-resolution logic for multi-layer PII detections."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass
class RawDetection:
    entity_type: str
    start: int
    end: int
    confidence: float
    source: str  # "regex", "regex+checksum", "ner", "llm"
    original_text: str | None = None
    reason: str | None = None


DEFAULT_CONFIDENCE_THRESHOLD: float = 0.40


def _overlaps(a: RawDetection, b: RawDetection) -> bool:
    """Check if two detections have overlapping character spans."""
    return a.start < b.end and b.start < a.end


def cluster_overlapping(detections: list[RawDetection]) -> list[list[RawDetection]]:
    """Group detections into clusters based on overlapping character spans.

    Uses a sort+sweep approach: sort by start position, then merge overlapping intervals.
    """
    if not detections:
        return []

    sorted_dets = sorted(detections, key=lambda d: (d.start, d.end))
    clusters: list[list[RawDetection]] = [[sorted_dets[0]]]

    for det in sorted_dets[1:]:
        # Check if this detection overlaps with any in the current cluster
        current_cluster = clusters[-1]
        cluster_end = max(d.end for d in current_cluster)
        if det.start < cluster_end:
            current_cluster.append(det)
        else:
            clusters.append([det])

    return clusters


def _resolve_cluster(cluster: list[RawDetection]) -> RawDetection:
    """Resolve a cluster of overlapping detections into a single detection."""
    if len(cluster) == 1:
        det = cluster[0]
        # Rule 3: Single source only → reduce confidence by 0.1 (floor at 0.5)
        if "checksum" not in det.source:
            det.confidence = max(det.confidence - 0.1, 0.5)
        return det

    sources = {d.source for d in cluster}

    # Rule 1: If any detection has checksum → keep it with high confidence
    checksum_dets = [d for d in cluster if "checksum" in d.source]
    if checksum_dets:
        best = max(checksum_dets, key=lambda d: d.confidence)
        best.confidence = max(best.confidence, 0.95)
        # Rule 4: Take wider span
        best.start = min(d.start for d in cluster)
        best.end = max(d.end for d in cluster)
        best.source = ",".join(sorted(sources))
        return best

    # Find best detection by confidence
    best = max(cluster, key=lambda d: d.confidence)

    # Rule 2: If both NER and LLM sources present → boost confidence by +0.1
    has_ner = any("ner" in d.source for d in cluster)
    has_llm = any("llm" in d.source for d in cluster)
    if has_ner and has_llm:
        best.confidence = min(best.confidence + 0.1, 1.0)

    # Rule 4: Take wider span
    best.start = min(d.start for d in cluster)
    best.end = max(d.end for d in cluster)
    best.source = ",".join(sorted(sources))

    return best


def _confidence_tier(confidence: float) -> str:
    """Assign confidence tier: HIGH (>=0.90), MEDIUM (0.70-0.89), LOW (0.50-0.69)."""
    if confidence >= 0.90:
        return "HIGH"
    elif confidence >= 0.70:
        return "MEDIUM"
    else:
        return "LOW"


def merge_detections(
    regex_detections: list[RawDetection],
    ner_detections: list[RawDetection],
    llm_detections: list[RawDetection],
) -> list[RawDetection]:
    """Merge detections from all layers, applying conflict resolution rules.

    Rules:
      1. Checksum source → keep with confidence >= 0.95
      2. Both NER + LLM → boost best confidence by +0.1 (cap 1.0)
      3. Single source only → reduce confidence by 0.1 (floor 0.5)
      4. Overlapping spans → take wider span
      5. Discard if final confidence < DEFAULT_CONFIDENCE_THRESHOLD (0.40)
    """
    all_detections = regex_detections + ner_detections + llm_detections
    if not all_detections:
        return []

    clusters = cluster_overlapping(all_detections)
    merged: list[RawDetection] = []

    for cluster in clusters:
        resolved = _resolve_cluster(cluster)
        # Rule 5: Discard below threshold
        if resolved.confidence >= DEFAULT_CONFIDENCE_THRESHOLD:
            resolved.reason = _confidence_tier(resolved.confidence)
            merged.append(resolved)

    # Sort by position
    merged.sort(key=lambda d: (d.start, d.end))
    return merged
