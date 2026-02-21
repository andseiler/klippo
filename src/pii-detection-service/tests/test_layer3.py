"""Layer 3 LLM tests with mocked HTTP calls."""

from __future__ import annotations

import json
from unittest.mock import AsyncMock, patch

import httpx
import pytest

from app.config import Settings
from app.detection.layer3_llm.llm_client import Layer3LLM
from app.detection.layer3_llm.parser import parse_llm_response
from app.detection.layer3_llm.prompts import build_detection_prompt, SYSTEM_PROMPT, DEFAULT_INSTRUCTIONS


class TestLlmResponseParser:
    def test_valid_json_array(self):
        raw = json.dumps([
            {
                "entity_type": "ADDRESS",
                "text": "Berliner Straße 42",
                "confidence": 0.8,
                "reason": "Street address",
            }
        ])
        results = parse_llm_response(raw, "Wohnt in Berliner Straße 42, Berlin")
        assert len(results) == 1
        assert results[0].entity_type == "ADDRESS"
        assert results[0].source == "llm"
        assert results[0].confidence <= 0.85  # capped

    def test_json_in_code_fence(self):
        raw = '```json\n[{"entity_type": "PERSON", "text": "Hans", "confidence": 0.7}]\n```'
        results = parse_llm_response(raw, "Hans ging nach Hause")
        assert len(results) == 1
        assert results[0].entity_type == "PERSON"

    def test_empty_array(self):
        raw = "[]"
        results = parse_llm_response(raw, "some text")
        assert results == []

    def test_malformed_json(self):
        raw = "This is not JSON at all"
        results = parse_llm_response(raw, "some text")
        assert results == []

    def test_empty_input(self):
        results = parse_llm_response("", "some text")
        assert results == []

    def test_confidence_capped_at_085(self):
        raw = json.dumps([
            {"entity_type": "PERSON", "text": "Max", "confidence": 0.99}
        ])
        results = parse_llm_response(raw, "Max went home")
        assert results[0].confidence == 0.85

    def test_text_not_found_in_original(self):
        raw = json.dumps([
            {"entity_type": "PERSON", "text": "nonexistent text", "confidence": 0.7}
        ])
        results = parse_llm_response(raw, "completely different text")
        assert results == []

    def test_wrapped_in_entities_key(self):
        raw = json.dumps({
            "entities": [
                {"entity_type": "PERSON", "text": "Max", "confidence": 0.7}
            ]
        })
        results = parse_llm_response(raw, "Max is here")
        assert len(results) == 1

    def test_case_insensitive_text_match(self):
        raw = json.dumps([
            {"entity_type": "PERSON", "text": "max", "confidence": 0.7}
        ])
        results = parse_llm_response(raw, "Max went home")
        assert len(results) == 1
        assert results[0].start == 0


class TestLayer3LLMClient:
    @pytest.fixture
    def disabled_settings(self):
        return Settings(LLM_BACKEND="disabled")

    @pytest.fixture
    def ollama_settings(self):
        return Settings(
            LLM_BACKEND="ollama",
            OLLAMA_URL="http://localhost:11434",
            OLLAMA_MODEL="llama3.1:8b",
            LLM_TIMEOUT_SECONDS=5,
        )

    @pytest.fixture
    def mistral_settings(self):
        return Settings(
            LLM_BACKEND="mistral",
            MISTRAL_API_KEY="test-key",
            MISTRAL_MODEL="mistral-large-latest",
            LLM_TIMEOUT_SECONDS=5,
        )

    @pytest.mark.asyncio
    async def test_disabled_returns_empty(self, disabled_settings):
        llm = Layer3LLM(disabled_settings)
        results = await llm.detect_additional_pii("Some text", [])
        assert results == []

    @pytest.mark.asyncio
    async def test_ollama_success(self, ollama_settings):
        llm = Layer3LLM(ollama_settings)
        mock_response = httpx.Response(
            200,
            json={
                "response": json.dumps([
                    {"entity_type": "ADDRESS", "text": "Berlin", "confidence": 0.8}
                ])
            },
            request=httpx.Request("POST", "http://localhost:11434/api/generate"),
        )

        with patch("httpx.AsyncClient.post", new_callable=AsyncMock, return_value=mock_response):
            results = await llm.detect_additional_pii("Lives in Berlin", [])
            assert len(results) == 1
            assert results[0].entity_type == "ADDRESS"

    @pytest.mark.asyncio
    async def test_mistral_success(self, mistral_settings):
        llm = Layer3LLM(mistral_settings)
        mock_response = httpx.Response(
            200,
            json={
                "choices": [
                    {
                        "message": {
                            "content": json.dumps([
                                {"entity_type": "PERSON", "text": "Hans", "confidence": 0.7}
                            ])
                        }
                    }
                ]
            },
            request=httpx.Request("POST", "https://api.mistral.ai/v1/chat/completions"),
        )

        with patch("httpx.AsyncClient.post", new_callable=AsyncMock, return_value=mock_response):
            results = await llm.detect_additional_pii("Hans ist hier", [])
            assert len(results) == 1

    @pytest.mark.asyncio
    async def test_timeout_returns_empty(self, ollama_settings):
        llm = Layer3LLM(ollama_settings)

        with patch(
            "httpx.AsyncClient.post",
            new_callable=AsyncMock,
            side_effect=httpx.TimeoutException("timeout"),
        ):
            results = await llm.detect_additional_pii("Some text", [])
            assert results == []

    @pytest.mark.asyncio
    async def test_http_error_returns_empty(self, ollama_settings):
        llm = Layer3LLM(ollama_settings)

        with patch(
            "httpx.AsyncClient.post",
            new_callable=AsyncMock,
            side_effect=httpx.HTTPError("connection failed"),
        ):
            results = await llm.detect_additional_pii("Some text", [])
            assert results == []

    @pytest.mark.asyncio
    async def test_unknown_backend_returns_empty(self):
        settings = Settings(LLM_BACKEND="unknown_backend")
        llm = Layer3LLM(settings)
        results = await llm.detect_additional_pii("Some text", [])
        assert results == []

    @pytest.mark.asyncio
    async def test_ollama_uses_german_system_prompt_by_default(self, ollama_settings):
        """Default language=de should use the German system prompt."""
        llm = Layer3LLM(ollama_settings)
        mock_response = httpx.Response(
            200,
            json={"response": "[]"},
            request=httpx.Request("POST", "http://localhost:11434/api/generate"),
        )

        with patch("httpx.AsyncClient.post", new_callable=AsyncMock, return_value=mock_response) as mock_post:
            await llm.detect_additional_pii("Test text", [])
            call_kwargs = mock_post.call_args
            payload = call_kwargs.kwargs.get("json") or call_kwargs[1].get("json")
            assert payload["system"] == SYSTEM_PROMPT["de"]

    @pytest.mark.asyncio
    async def test_ollama_uses_english_system_prompt(self, ollama_settings):
        """Passing language='en' should use the English system prompt."""
        llm = Layer3LLM(ollama_settings)
        mock_response = httpx.Response(
            200,
            json={"response": "[]"},
            request=httpx.Request("POST", "http://localhost:11434/api/generate"),
        )

        with patch("httpx.AsyncClient.post", new_callable=AsyncMock, return_value=mock_response) as mock_post:
            await llm.detect_additional_pii("Test text", [], language="en")
            call_kwargs = mock_post.call_args
            payload = call_kwargs.kwargs.get("json") or call_kwargs[1].get("json")
            assert payload["system"] == SYSTEM_PROMPT["en"]


class TestLanguageAwarePrompts:
    """Verify that prompts contain only the requested language."""

    def test_english_prompt_has_no_german(self):
        prompt = build_detection_prompt("Hello world", [], language="en")
        assert "Already detected:" in prompt
        assert "Text to analyze:" in prompt
        # Must not contain German text
        assert "Bereits erkannt" not in prompt
        assert "Zu analysierender Text" not in prompt
        assert "Analysiere" not in prompt
        assert "Antworte mit" not in prompt

    def test_german_prompt_has_no_english(self):
        prompt = build_detection_prompt("Hallo Welt", [], language="de")
        assert "Bereits erkannt:" in prompt
        assert "Zu analysierender Text:" in prompt
        # Must not contain English text
        assert "Already detected:" not in prompt
        assert "Text to analyze:" not in prompt
        assert "Analyze the following" not in prompt
        assert "Respond with a JSON" not in prompt

    def test_default_language_is_german(self):
        prompt = build_detection_prompt("Text", [])
        assert "Bereits erkannt:" in prompt

    def test_invalid_language_falls_back_to_german(self):
        prompt = build_detection_prompt("Text", [], language="fr")
        assert "Bereits erkannt:" in prompt

    def test_custom_instructions_override_default(self):
        custom = "My custom instructions"
        prompt = build_detection_prompt("Text", [], custom_instructions=custom, language="en")
        assert custom in prompt
        # Response format should still be English
        assert "Respond with a JSON" in prompt

    def test_system_prompt_dict_has_both_languages(self):
        assert "en" in SYSTEM_PROMPT
        assert "de" in SYSTEM_PROMPT
        assert "PII detection" in SYSTEM_PROMPT["en"]
        assert "personenbezogener Daten" in SYSTEM_PROMPT["de"]

    def test_default_instructions_dict_has_both_languages(self):
        assert "en" in DEFAULT_INSTRUCTIONS
        assert "de" in DEFAULT_INSTRUCTIONS
