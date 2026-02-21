from fastapi import APIRouter, Request

from app.detection.pipeline import DetectionPipeline
from app.models import DetectRequest, DetectResponse

router = APIRouter()


@router.post("/api/detect", response_model=DetectResponse, response_model_by_alias=True)
async def detect(request: Request, body: DetectRequest) -> DetectResponse:
    """Run multi-layer PII detection on the provided text segments."""
    pipeline: DetectionPipeline = request.app.state.pipeline
    return await pipeline.detect(body)
