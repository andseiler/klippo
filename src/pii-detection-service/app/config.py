from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # Layer 3 — LLM backend
    LLM_BACKEND: str = "disabled"  # "disabled" | "ollama" | "mistral"
    MISTRAL_API_KEY: str = ""
    MISTRAL_MODEL: str = "mistral-large-latest"
    OLLAMA_URL: str = "http://host.docker.internal:11434"
    OLLAMA_MODEL: str = "llama3.1:8b"
    LLM_TIMEOUT_SECONDS: int = 30

    # Layer 2 — NER
    NER_MODEL_NAME: str = "Davlan/bert-base-multilingual-cased-ner-hrl"
    NER_DEVICE: str = "cpu"
    LAYER2_ENABLED: bool = True

    # Confidence thresholds
    MIN_CONFIDENCE: float = 0.50
    HIGH_CONFIDENCE: float = 0.90
    MEDIUM_CONFIDENCE: float = 0.70

    model_config = {"env_prefix": "", "case_sensitive": True}
