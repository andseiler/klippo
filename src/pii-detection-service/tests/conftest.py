"""Shared test fixtures for PII detection service tests."""

from __future__ import annotations

import pytest

from app.config import Settings

SAMPLE_GERMAN_TEXT = (
    "Sehr geehrter Herr Max Mustermann, "
    "Ihre Steuer-ID lautet 65 929 97024. "
    "Die IBAN ist DE89 3704 0044 0532 0130 00. "
    "Telefon: +49 30 12345678. "
    "E-Mail: max.mustermann@example.com. "
    "Geboren am 15.03.1985. "
    "Sozialversicherungsnummer: 12 010185 M 123."
)

SAMPLE_CAMEL_REQUEST = {
    "jobId": "00000000-0000-0000-0000-000000000001",
    "segments": [
        {
            "segmentId": "seg-001",
            "segmentIndex": 0,
            "textContent": SAMPLE_GERMAN_TEXT,
            "sourceType": "text",
        }
    ],
    "layers": [],
    "languageHint": "de",
}

SAMPLE_SNAKE_REQUEST = {
    "job_id": "00000000-0000-0000-0000-000000000001",
    "segments": [
        {
            "segment_id": "seg-001",
            "segment_index": 0,
            "text_content": SAMPLE_GERMAN_TEXT,
            "source_type": "text",
        }
    ],
    "layers": [],
    "language_hint": "de",
}


@pytest.fixture
def sample_german_text() -> str:
    return SAMPLE_GERMAN_TEXT


@pytest.fixture
def sample_camel_request() -> dict:
    return SAMPLE_CAMEL_REQUEST.copy()


@pytest.fixture
def sample_snake_request() -> dict:
    return SAMPLE_SNAKE_REQUEST.copy()


@pytest.fixture
def mock_settings() -> Settings:
    """Settings with L2 disabled, L3 disabled for fast unit tests."""
    return Settings(
        LAYER2_ENABLED=False,
        LLM_BACKEND="disabled",
    )
