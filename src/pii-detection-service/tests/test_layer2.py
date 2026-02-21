"""Layer 2 NER tests with mocked transformer model."""

from __future__ import annotations

from unittest.mock import MagicMock, patch

from app.detection.layer2_ner.language_detect import detect_language


class TestLanguageDetect:
    def test_short_text_returns_fallback(self):
        assert detect_language("Hi", fallback="de") == "de"
        assert detect_language("Hi", fallback="en") == "en"

    def test_german_text(self):
        text = "Dies ist ein langer deutscher Text mit vielen Wörtern und Sätzen."
        result = detect_language(text)
        assert result in ("de", "en")  # langdetect can be nondeterministic

    def test_english_text(self):
        text = "This is a long English text with many words and sentences for testing."
        result = detect_language(text)
        assert result in ("de", "en")

    def test_fallback_on_error(self):
        """When langdetect raises an error, should return fallback."""
        with patch("langdetect.detect", side_effect=Exception("mock error")):
            result = detect_language("Some text here that is long enough", fallback="de")
            assert result == "de"


class TestLayer2Engine:
    """Test Layer 2 NER engine integration with mocked model."""

    def test_run_layer2_returns_raw_detections(self):
        """Verify run_layer2 returns properly structured RawDetection objects."""
        # Mock the analyzer engine
        mock_engine = MagicMock()
        mock_result = MagicMock()
        mock_result.entity_type = "PERSON"
        mock_result.start = 0
        mock_result.end = 14
        mock_result.score = 0.85
        mock_engine.analyze.return_value = [mock_result]

        from app.detection.layer2_ner.engine import run_layer2

        results = run_layer2(mock_engine, "Max Mustermann", "de")

        assert len(results) == 1
        assert results[0].entity_type == "PERSON"
        assert results[0].source == "ner"
        assert results[0].confidence == 0.85
        assert results[0].start == 0
        assert results[0].end == 14

    def test_run_layer2_normalizes_language(self):
        """Non-de/en languages should fall back to 'de'."""
        mock_engine = MagicMock()
        mock_engine.analyze.return_value = []

        from app.detection.layer2_ner.engine import run_layer2

        run_layer2(mock_engine, "text", "fr")
        mock_engine.analyze.assert_called_once_with(
            text="text", language="de", score_threshold=0.3
        )
