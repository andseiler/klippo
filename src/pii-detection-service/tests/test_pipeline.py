"""Full pipeline integration tests: L1 real, L2 mocked, L3 disabled."""

from __future__ import annotations

import pytest

from app.config import Settings
from app.detection.layer3_llm.llm_client import Layer3LLM
from app.detection.pipeline import DetectionPipeline
from app.models import DetectionSegment, DetectRequest


@pytest.fixture
def pipeline_l1_only():
    """Pipeline with real L1, no L2, L3 disabled."""
    settings = Settings(LAYER2_ENABLED=False, LLM_BACKEND="disabled")

    try:
        from app.detection.layer1_regex.engine import create_layer1_engine

        l1_engine = create_layer1_engine()
    except Exception as e:
        pytest.skip(f"Layer 1 engine unavailable: {e}")

    l3_llm = Layer3LLM(settings)
    return DetectionPipeline(settings, l1_engine, None, l3_llm)


class TestPipelineIntegration:
    @pytest.mark.asyncio
    async def test_detects_iban_in_german_text(self, pipeline_l1_only):
        request = DetectRequest(
            job_id="test-job",
            segments=[
                DetectionSegment(
                    segment_id="seg-1",
                    segment_index=0,
                    text_content="Die IBAN lautet DE89 3704 0044 0532 0130 00.",
                    source_type="text",
                ),
            ],
        )
        response = await pipeline_l1_only.detect(request)
        assert response.processing_time_ms >= 0
        assert "regex" in response.layers_used

        iban_dets = [d for d in response.detections if "IBAN" in d.entity_type]
        assert len(iban_dets) >= 1

    @pytest.mark.asyncio
    async def test_detects_email(self, pipeline_l1_only):
        request = DetectRequest(
            job_id="test-job",
            segments=[
                DetectionSegment(
                    segment_id="seg-1",
                    segment_index=0,
                    text_content="E-Mail: max.mustermann@example.com",
                    source_type="text",
                ),
            ],
        )
        response = await pipeline_l1_only.detect(request)
        email_dets = [d for d in response.detections if "EMAIL" in d.entity_type]
        assert len(email_dets) >= 1

    @pytest.mark.asyncio
    async def test_empty_segments(self, pipeline_l1_only):
        request = DetectRequest(
            job_id="test-job",
            segments=[],
        )
        response = await pipeline_l1_only.detect(request)
        assert response.detections == []
        assert response.processing_time_ms >= 0

    @pytest.mark.asyncio
    async def test_empty_text_segment(self, pipeline_l1_only):
        request = DetectRequest(
            job_id="test-job",
            segments=[
                DetectionSegment(
                    segment_id="seg-1",
                    segment_index=0,
                    text_content="",
                    source_type="text",
                ),
            ],
        )
        response = await pipeline_l1_only.detect(request)
        assert response.detections == []

    @pytest.mark.asyncio
    async def test_layer_selection(self, pipeline_l1_only):
        """When specific layers are requested, only those run."""
        request = DetectRequest(
            job_id="test-job",
            segments=[
                DetectionSegment(
                    segment_id="seg-1",
                    segment_index=0,
                    text_content="IBAN: DE89 3704 0044 0532 0130 00",
                    source_type="text",
                ),
            ],
            layers=["regex"],
        )
        response = await pipeline_l1_only.detect(request)
        assert "regex" in response.layers_used
        assert "ner" not in response.layers_used
        assert "llm" not in response.layers_used

    @pytest.mark.asyncio
    async def test_detection_results_have_segment_id(self, pipeline_l1_only):
        request = DetectRequest(
            job_id="test-job",
            segments=[
                DetectionSegment(
                    segment_id="my-segment",
                    segment_index=0,
                    text_content="IBAN: DE89 3704 0044 0532 0130 00",
                    source_type="text",
                ),
            ],
        )
        response = await pipeline_l1_only.detect(request)
        for det in response.detections:
            assert det.segment_id == "my-segment"

    @pytest.mark.asyncio
    async def test_multiple_segments(self, pipeline_l1_only):
        request = DetectRequest(
            job_id="test-job",
            segments=[
                DetectionSegment(
                    segment_id="seg-1",
                    segment_index=0,
                    text_content="IBAN: DE89 3704 0044 0532 0130 00",
                    source_type="text",
                ),
                DetectionSegment(
                    segment_id="seg-2",
                    segment_index=1,
                    text_content="E-Mail: test@example.com",
                    source_type="text",
                ),
            ],
        )
        response = await pipeline_l1_only.detect(request)
        segment_ids = {d.segment_id for d in response.detections}
        assert len(segment_ids) >= 1  # At least one segment should have detections

    @pytest.mark.asyncio
    async def test_confidence_tier_populated(self, pipeline_l1_only):
        request = DetectRequest(
            job_id="test-job",
            segments=[
                DetectionSegment(
                    segment_id="seg-1",
                    segment_index=0,
                    text_content="IBAN: DE89 3704 0044 0532 0130 00",
                    source_type="text",
                ),
            ],
        )
        response = await pipeline_l1_only.detect(request)
        for det in response.detections:
            assert det.confidence_tier in ("HIGH", "MEDIUM", "LOW")
