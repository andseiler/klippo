"""Parse LLM JSON responses into RawDetection objects."""

from __future__ import annotations

import json
import re

import structlog

from app.detection.merger import RawDetection

logger = structlog.get_logger()

# Cap LLM-only confidence at 0.85
MAX_LLM_CONFIDENCE = 0.85


def _extract_json_from_text(raw_text: str) -> str | None:
    """Try to extract JSON from markdown code fences or raw text."""
    # Try code fence extraction
    fence_match = re.search(r"```(?:json)?\s*\n?(.*?)\n?\s*```", raw_text, re.DOTALL)
    if fence_match:
        return fence_match.group(1).strip()

    # Try finding array brackets
    bracket_match = re.search(r"\[.*\]", raw_text, re.DOTALL)
    if bracket_match:
        return bracket_match.group(0)

    return None


def parse_llm_response(raw_text: str, original_text: str) -> list[RawDetection]:
    """Parse LLM response text into RawDetection objects.

    Tries multiple strategies:
    1. Direct json.loads()
    2. Extract JSON from markdown code fences
    3. Return empty list on failure
    """
    if not raw_text or not raw_text.strip():
        return []

    parsed = None

    # Strategy 1: direct parse
    try:
        parsed = json.loads(raw_text.strip())
    except json.JSONDecodeError:
        pass

    # Strategy 2: extract from fences/brackets
    if parsed is None:
        extracted = _extract_json_from_text(raw_text)
        if extracted:
            try:
                parsed = json.loads(extracted)
            except json.JSONDecodeError:
                pass

    if parsed is None:
        logger.warning("llm_response_parse_failed", raw_text=raw_text[:200])
        return []

    # Ensure it's a list
    if isinstance(parsed, dict):
        # Some LLMs wrap in {"entities": [...]}
        for key in ("entities", "results", "pii", "detections"):
            if key in parsed and isinstance(parsed[key], list):
                parsed = parsed[key]
                break
        else:
            parsed = [parsed]

    if not isinstance(parsed, list):
        logger.warning("llm_response_not_list", type=type(parsed).__name__)
        return []

    detections: list[RawDetection] = []
    for item in parsed:
        if not isinstance(item, dict):
            continue

        entity_type = item.get("entity_type", "UNKNOWN")
        text_span = item.get("text", "")
        confidence = min(float(item.get("confidence", 0.7)), MAX_LLM_CONFIDENCE)
        reason = item.get("reason", "")

        if not text_span:
            continue

        # Find the text span in the original text
        start = original_text.find(text_span)
        if start == -1:
            # Try case-insensitive search
            lower_original = original_text.lower()
            start = lower_original.find(text_span.lower())

        if start == -1:
            logger.debug("llm_span_not_found", text=text_span[:50])
            continue

        end = start + len(text_span)

        detections.append(
            RawDetection(
                entity_type=entity_type,
                start=start,
                end=end,
                confidence=confidence,
                source="llm",
                original_text=text_span,
                reason=reason,
            )
        )

    return detections
