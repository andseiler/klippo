from contextlib import asynccontextmanager
from typing import AsyncIterator

import structlog
from fastapi import FastAPI

from app.config import Settings
from app.detection.layer1_regex.engine import create_layer1_engine
from app.detection.layer3_llm.llm_client import Layer3LLM
from app.detection.pipeline import DetectionPipeline
from app.detection.router import router as detection_router
from app.health import router as health_router

logger = structlog.get_logger()


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    settings = Settings()

    # Layer 1: Regex + Checksum (always available)
    logger.info("loading_layer1")
    l1_engine = create_layer1_engine()
    logger.info("layer1_ready")

    # Layer 2: NER (optional, may take 10-30s to load)
    l2_engine = None
    if settings.LAYER2_ENABLED:
        try:
            from app.detection.layer2_ner.engine import create_layer2_engine

            logger.info("loading_layer2", model=settings.NER_MODEL_NAME)
            l2_engine = create_layer2_engine(settings.NER_MODEL_NAME, settings.NER_DEVICE)
            logger.info("layer2_ready")
        except Exception:
            logger.exception("layer2_load_failed")
    else:
        logger.info("layer2_disabled")

    # Layer 3: LLM (lightweight init, just stores config)
    l3_llm = Layer3LLM(settings)
    logger.info("layer3_configured", backend=settings.LLM_BACKEND)

    # Construct pipeline
    pipeline = DetectionPipeline(settings, l1_engine, l2_engine, l3_llm)

    # Store in app.state for dependency injection
    app.state.pipeline = pipeline
    app.state.settings = settings
    app.state.l2_loaded = l2_engine is not None
    app.state.l3_backend = settings.LLM_BACKEND

    yield

    # Shutdown — nothing to clean up


app = FastAPI(
    title="PII Detection Service",
    version="0.1.0",
    lifespan=lifespan,
)

app.include_router(health_router)
app.include_router(detection_router)
