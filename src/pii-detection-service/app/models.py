from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class CamelModel(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
    )


class DetectionSegment(CamelModel):
    segment_id: str
    segment_index: int
    text_content: str
    source_type: str


class ExistingDetection(CamelModel):
    entity_type: str
    start_offset: int
    end_offset: int


class DetectRequest(CamelModel):
    job_id: str
    segments: list[DetectionSegment]
    layers: list[str] = []
    language_hint: str | None = None
    existing_detections: list[ExistingDetection] = []
    custom_instructions: str | None = None


class DetectionResult(CamelModel):
    segment_id: str
    entity_type: str
    start_offset: int
    end_offset: int
    confidence: float
    detection_source: str
    original_text: str | None = None
    confidence_tier: str | None = None


class LayerStat(CamelModel):
    layer: str               # "regex", "ner", "llm"
    status: str              # "ran", "disabled", "failed", "not_available"
    detection_count: int = 0
    skip_reason: str | None = None


class DetectResponse(CamelModel):
    detections: list[DetectionResult] = []
    processing_time_ms: int = 0
    layers_used: list[str] = []
    layer_stats: list[LayerStat] = []


class HealthResponse(BaseModel):
    status: str
    version: str
    layers_available: list[str]
