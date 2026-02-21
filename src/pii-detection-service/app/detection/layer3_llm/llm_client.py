"""Layer 3 LLM client abstraction for Ollama, Mistral, or disabled."""

from __future__ import annotations

import httpx
import structlog

from app.config import Settings
from app.detection.merger import RawDetection

from .parser import parse_llm_response
from .prompts import SYSTEM_PROMPT, build_detection_prompt

logger = structlog.get_logger()


class Layer3LLM:
    """LLM-based PII detection layer.

    Supports three backends:
    - ``disabled``: returns empty results immediately
    - ``ollama``: calls a local Ollama instance
    - ``mistral``: calls the Mistral API
    """

    def __init__(self, settings: Settings) -> None:
        self.backend = settings.LLM_BACKEND
        self.ollama_url = settings.OLLAMA_URL
        self.ollama_model = settings.OLLAMA_MODEL
        self.mistral_api_key = settings.MISTRAL_API_KEY
        self.mistral_model = settings.MISTRAL_MODEL
        self.timeout = settings.LLM_TIMEOUT_SECONDS

    async def detect_additional_pii(
        self,
        text: str,
        already_detected: list[str],
        custom_instructions: str | None = None,
        language: str = "de",
    ) -> list[RawDetection]:
        """Detect PII that other layers may have missed.

        Args:
            text: The text segment to analyze.
            already_detected: List of descriptions of already-detected entities.
            custom_instructions: Optional custom instructions to use instead of defaults.
            language: Language key ("en" or "de"). Defaults to "de".

        Returns:
            List of additional RawDetection objects from LLM analysis.
        """
        if self.backend == "disabled":
            return []

        lang = language if language in ("en", "de") else "de"
        prompt = build_detection_prompt(text, already_detected, custom_instructions=custom_instructions, language=lang)
        system_prompt = SYSTEM_PROMPT[lang]

        try:
            if self.backend == "ollama":
                raw_response = await self._call_ollama(prompt, system_prompt)
            elif self.backend == "mistral":
                raw_response = await self._call_mistral(prompt, system_prompt)
            else:
                logger.warning("unknown_llm_backend", backend=self.backend)
                return []
        except httpx.TimeoutException:
            logger.warning("llm_timeout", backend=self.backend, timeout=self.timeout)
            return []
        except Exception:
            logger.exception("llm_call_failed", backend=self.backend)
            return []

        return parse_llm_response(raw_response, text)

    async def _call_ollama(self, prompt: str, system: str) -> str:
        """Call Ollama's generate API."""
        url = f"{self.ollama_url}/api/generate"
        payload = {
            "model": self.ollama_model,
            "prompt": prompt,
            "system": system,
            "stream": False,
            "format": "json",
        }

        async with httpx.AsyncClient(timeout=self.timeout) as client:
            response = await client.post(url, json=payload)
            response.raise_for_status()
            data = response.json()
            return str(data.get("response", ""))

    async def _call_mistral(self, prompt: str, system: str) -> str:
        """Call Mistral's chat completions API."""
        url = "https://api.mistral.ai/v1/chat/completions"
        headers = {
            "Authorization": f"Bearer {self.mistral_api_key}",
            "Content-Type": "application/json",
        }
        payload = {
            "model": self.mistral_model,
            "messages": [
                {"role": "system", "content": system},
                {"role": "user", "content": prompt},
            ],
            "response_format": {"type": "json_object"},
        }

        async with httpx.AsyncClient(timeout=self.timeout) as client:
            response = await client.post(url, json=payload, headers=headers)
            response.raise_for_status()
            data = response.json()
            return str(data["choices"][0]["message"]["content"])
