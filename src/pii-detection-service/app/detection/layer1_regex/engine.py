"""Layer 1 engine: regex + checksum detection using Presidio AnalyzerEngine."""

from __future__ import annotations

from presidio_analyzer import AnalyzerEngine, RecognizerRegistry
from presidio_analyzer.nlp_engine import NlpEngineProvider
from presidio_analyzer.predefined_recognizers import EmailRecognizer, IbanRecognizer

from app.detection.merger import RawDetection

from .recognizers import get_all_custom_recognizers


def create_layer1_engine() -> AnalyzerEngine:
    """Create a Presidio AnalyzerEngine configured for regex-only Layer 1 detection."""
    registry = RecognizerRegistry(supported_languages=["de", "en"])

    # Add built-in recognizers for both de and en
    registry.add_recognizer(EmailRecognizer(supported_language="en"))
    registry.add_recognizer(EmailRecognizer(supported_language="de"))
    registry.add_recognizer(IbanRecognizer(supported_language="en"))
    registry.add_recognizer(IbanRecognizer(supported_language="de"))

    # Add all custom DACH recognizers
    for recognizer in get_all_custom_recognizers():
        registry.add_recognizer(recognizer)

    # Minimal NLP engine (spacy not needed for regex-only)
    nlp_config = {
        "nlp_engine_name": "spacy",
        "models": [
            {"lang_code": "de", "model_name": "de_core_news_sm"},
            {"lang_code": "en", "model_name": "en_core_web_sm"},
        ],
    }

    nlp_engine = NlpEngineProvider(nlp_configuration=nlp_config).create_engine()

    engine = AnalyzerEngine(
        registry=registry,
        nlp_engine=nlp_engine,
        supported_languages=["de", "en"],
    )

    return engine


def run_layer1(engine: AnalyzerEngine, text: str, language: str = "de") -> list[RawDetection]:
    """Run Layer 1 detection on text, return list of RawDetection."""
    if language not in ("de", "en"):
        language = "de"

    results = engine.analyze(text=text, language=language, score_threshold=0.3)

    detections: list[RawDetection] = []
    for r in results:
        # Determine source label based on whether checksum was used
        source = "regex+checksum" if r.score >= 0.90 else "regex"
        detections.append(
            RawDetection(
                entity_type=r.entity_type,
                start=r.start,
                end=r.end,
                confidence=r.score,
                source=source,
                original_text=text[r.start : r.end],
            )
        )

    return detections
