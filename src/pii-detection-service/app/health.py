from fastapi import APIRouter, Request

from app.models import HealthResponse

router = APIRouter()


@router.get("/health", response_model=HealthResponse)
async def health(request: Request) -> HealthResponse:
    layers: list[str] = ["regex"]

    if getattr(request.app.state, "l2_loaded", False):
        layers.append("ner")

    l3_backend = getattr(request.app.state, "l3_backend", "disabled")
    if l3_backend != "disabled":
        layers.append("llm")

    return HealthResponse(
        status="healthy",
        version="0.1.0",
        layers_available=layers,
    )
