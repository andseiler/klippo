"""Layer 2 engine: Transformer NER via Presidio."""

from __future__ import annotations

import structlog
from presidio_analyzer import AnalyzerEngine, RecognizerRegistry
from presidio_analyzer.predefined_recognizers import SpacyRecognizer
from presidio_analyzer.nlp_engine import TransformersNlpEngine

from app.detection.merger import RawDetection

logger = structlog.get_logger()

# Mapping from HuggingFace NER labels to our entity types
HF_ENTITY_MAP = {
    "PER": "PERSON",
    "LOC": "LOCATION",
    "ORG": "ORGANIZATION",
    "MISC": "MISC",
}


def create_layer2_engine(model_name: str, device: str = "cpu") -> AnalyzerEngine:
    """Create a Presidio AnalyzerEngine backed by a HuggingFace transformer NER model.

    The model is loaded once and kept in memory.
    """
    logger.info("loading_ner_model", model=model_name, device=device)

    transformers_nlp_engine = TransformersNlpEngine(
        models=[
            {
                "lang_code": "de",
                "model_name": {
                    "spacy": "de_core_news_sm",
                    "transformers": model_name,
                },
            },
            {
                "lang_code": "en",
                "model_name": {
                    "spacy": "en_core_web_sm",
                    "transformers": model_name,
                },
            },
        ],
    )

    registry = RecognizerRegistry(supported_languages=["de", "en"])
    # Skip load_predefined_recognizers() — broken in presidio 2.2.361
    # (UsSsnRecognizer.__init__() gets unexpected 'name' kwarg).
    # Manually register SpacyRecognizer to read NER labels from the
    # transformer model output on the spaCy doc.
    registry.add_recognizer(SpacyRecognizer(supported_language="de"))
    registry.add_recognizer(SpacyRecognizer(supported_language="en"))

    engine = AnalyzerEngine(
        registry=registry,
        nlp_engine=transformers_nlp_engine,
        supported_languages=["de", "en"],
    )

    logger.info("ner_model_loaded", model=model_name)
    return engine


def run_layer2(engine: AnalyzerEngine, text: str, language: str = "de") -> list[RawDetection]:
    """Run Layer 2 NER detection on text, return list of RawDetection."""
    if language not in ("de", "en"):
        language = "de"

    results = engine.analyze(text=text, language=language, score_threshold=0.3)

    detections: list[RawDetection] = []
    for r in results:
        detections.append(
            RawDetection(
                entity_type=r.entity_type,
                start=r.start,
                end=r.end,
                confidence=r.score,
                source="ner",
                original_text=text[r.start : r.end],
            )
        )

    return detections
