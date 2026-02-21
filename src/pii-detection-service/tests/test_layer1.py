"""Layer 1 engine integration tests.

These tests use the real Presidio engine with regex recognizers.
They do NOT require spacy models (skipped if unavailable).
"""

from __future__ import annotations

import pytest

from app.detection.layer1_regex.engine import create_layer1_engine, run_layer1


@pytest.fixture(scope="module")
def l1_engine():
    """Create Layer 1 engine once for all tests in this module."""
    try:
        return create_layer1_engine()
    except Exception as e:
        pytest.skip(f"Layer 1 engine unavailable: {e}")


class TestLayer1Engine:
    def test_detects_iban(self, l1_engine):
        text = "IBAN: DE89 3704 0044 0532 0130 00"
        results = run_layer1(l1_engine, text, "de")
        types = [r.entity_type for r in results]
        assert "IBAN_CODE" in types or any("IBAN" in t for t in types)

    def test_detects_email(self, l1_engine):
        text = "E-Mail: max.mustermann@example.com"
        results = run_layer1(l1_engine, text, "de")
        types = [r.entity_type for r in results]
        assert "EMAIL_ADDRESS" in types or any("EMAIL" in t for t in types)

    def test_detects_phone(self, l1_engine):
        text = "Telefon: +49 30 12345678"
        results = run_layer1(l1_engine, text, "de")
        types = [r.entity_type for r in results]
        assert any("PHONE" in t for t in types)

    def test_empty_text_returns_empty(self, l1_engine):
        results = run_layer1(l1_engine, "", "de")
        assert results == []

    def test_no_pii_returns_empty(self, l1_engine):
        text = "Dies ist ein normaler Text ohne personenbezogene Daten."
        results = run_layer1(l1_engine, text, "de")
        # May have some low-confidence matches, but should be few
        high_conf = [r for r in results if r.confidence > 0.7]
        assert len(high_conf) == 0

    def test_results_have_correct_structure(self, l1_engine):
        text = "IBAN: DE89 3704 0044 0532 0130 00"
        results = run_layer1(l1_engine, text, "de")
        if results:
            r = results[0]
            assert r.start >= 0
            assert r.end > r.start
            assert 0 <= r.confidence <= 1.0
            assert r.source in ("regex", "regex+checksum")
            assert r.original_text is not None
