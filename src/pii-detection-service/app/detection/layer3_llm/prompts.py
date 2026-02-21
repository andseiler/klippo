"""Language-aware prompt templates for LLM-based PII detection."""

from __future__ import annotations

SYSTEM_PROMPT: dict[str, str] = {
    "en": (
        "You are a PII detection assistant. Your task is to find personally identifiable "
        "information (PII) in text that may not have been detected by regex and NER systems."
    ),
    "de": (
        "Du bist ein Assistent zur Erkennung personenbezogener Daten (PII). Deine Aufgabe "
        "ist es, personenbezogene Informationen im Text zu finden, die möglicherweise nicht "
        "von Regex- und NER-Systemen erkannt wurden."
    ),
}


DEFAULT_INSTRUCTIONS: dict[str, str] = {
    "en": (
        "Analyze the following text for personally identifiable information (PII) that has NOT "
        "already been detected. Focus on contextual PII like addresses, relationships, health "
        "information, financial details, or any other sensitive personal data."
    ),
    "de": (
        "Analysiere den folgenden Text auf personenbezogene Daten (PII), die NOCH NICHT erkannt "
        "wurden. Konzentriere dich auf kontextuelle PII wie Adressen, Beziehungen, "
        "Gesundheitsinformationen, finanzielle Details oder andere sensible persönliche Daten."
    ),
}

RESPONSE_FORMAT: dict[str, str] = {
    "en": (
        'Respond with a JSON array of objects. Each object must have:\n'
        '- "entity_type": string (e.g. "ADDRESS", "HEALTH_INFO", "FINANCIAL", "PERSON", "RELATIONSHIP")\n'
        '- "text": the exact text span found in the original text\n'
        '- "confidence": float between 0.0 and 1.0\n'
        '- "reason": brief explanation why this is PII\n'
        '\n'
        'If no additional PII is found, return an empty array: []'
    ),
    "de": (
        'Antworte mit einem JSON-Array von Objekten. Jedes Objekt muss haben:\n'
        '- "entity_type": string (z.B. "ADDRESS", "HEALTH_INFO", "FINANCIAL", "PERSON", "RELATIONSHIP")\n'
        '- "text": der exakte Textabschnitt aus dem Originaltext\n'
        '- "confidence": float zwischen 0.0 und 1.0\n'
        '- "reason": kurze Erklärung, warum dies PII ist\n'
        '\n'
        'Wenn keine weiteren PII gefunden werden, gib ein leeres Array zurück: []'
    ),
}

_LABELS: dict[str, dict[str, str]] = {
    "en": {
        "already_detected": "Already detected:",
        "text_to_analyze": "Text to analyze:",
    },
    "de": {
        "already_detected": "Bereits erkannt:",
        "text_to_analyze": "Zu analysierender Text:",
    },
}


def build_detection_prompt(
    text: str,
    already_detected: list[str],
    custom_instructions: str | None = None,
    language: str = "de",
) -> str:
    """Build the prompt for LLM PII detection.

    Assembles: instructions + already-detected block + text block + JSON response tag.
    No str.format() — zero placeholder risk.

    Args:
        text: The text segment to analyze.
        already_detected: List of already-detected entity descriptions (e.g. "IBAN at pos 10-30").
        custom_instructions: Optional custom instructions to use instead of DEFAULT_INSTRUCTIONS.
        language: Language key ("en" or "de"). Defaults to "de".
    """
    lang = language if language in ("en", "de") else "de"
    instructions = custom_instructions or DEFAULT_INSTRUCTIONS[lang]
    labels = _LABELS[lang]

    if already_detected:
        detected_str = "\n".join(f"  - {d}" for d in already_detected)
    else:
        detected_str = "  (none)"

    return (
        instructions
        + "\n\n"
        + RESPONSE_FORMAT[lang]
        + "\n\n"
        + labels["already_detected"]
        + "\n"
        + detected_str
        + "\n\n"
        + labels["text_to_analyze"]
        + "\n---\n"
        + text
        + "\n---\n\nJSON response:"
    )
