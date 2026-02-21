"""Per-segment language detection."""

from __future__ import annotations

import structlog

logger = structlog.get_logger()


def detect_language(text: str, fallback: str = "de") -> str:
    """Detect language of text, returning 'de' or 'en'.

    Falls back to ``fallback`` for short segments (<20 chars) or on error.
    """
    if len(text.strip()) < 20:
        return fallback

    try:
        from langdetect import detect

        lang = detect(text)
        # Normalize to the two languages we support
        if lang.startswith("de"):
            return "de"
        elif lang.startswith("en"):
            return "en"
        else:
            return fallback
    except Exception:
        logger.warning("language_detection_failed", fallback=fallback)
        return fallback
