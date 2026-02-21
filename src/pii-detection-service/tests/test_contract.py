"""Tests for camelCase wire format compatibility with .NET backend."""

from __future__ import annotations

from app.models import (
    DetectionResult,
    DetectionSegment,
    DetectRequest,
    DetectResponse,
)


class TestCamelCaseSerialization:
    """Verify .NET interop: camelCase serialization/deserialization."""

    def test_request_accepts_camel_case(self):
        """DetectRequest should accept camelCase keys (as sent by .NET)."""
        data = {
            "jobId": "abc-123",
            "segments": [
                {
                    "segmentId": "seg-1",
                    "segmentIndex": 0,
                    "textContent": "Hello world",
                    "sourceType": "text",
                }
            ],
            "layers": [],
            "languageHint": "de",
        }
        req = DetectRequest(**data)
        assert req.job_id == "abc-123"
        assert req.segments[0].segment_id == "seg-1"
        assert req.segments[0].text_content == "Hello world"
        assert req.language_hint == "de"

    def test_request_accepts_snake_case(self):
        """DetectRequest should also accept snake_case (backward compat)."""
        data = {
            "job_id": "abc-123",
            "segments": [
                {
                    "segment_id": "seg-1",
                    "segment_index": 0,
                    "text_content": "Hello world",
                    "source_type": "text",
                }
            ],
        }
        req = DetectRequest(**data)
        assert req.job_id == "abc-123"
        assert req.segments[0].segment_id == "seg-1"

    def test_response_serializes_to_camel_case(self):
        """DetectResponse should serialize to camelCase for .NET consumption."""
        result = DetectionResult(
            segment_id="seg-1",
            entity_type="IBAN",
            start_offset=10,
            end_offset=30,
            confidence=0.95,
            detection_source="regex+checksum",
            original_text="DE89 3704 0044 0532 0130 00",
            confidence_tier="HIGH",
        )
        resp = DetectResponse(
            detections=[result],
            processing_time_ms=42,
            layers_used=["regex"],
        )

        # Serialize with aliases (camelCase)
        data = resp.model_dump(by_alias=True)

        assert "detections" in data
        assert "processingTimeMs" in data
        assert "layersUsed" in data

        det = data["detections"][0]
        assert "segmentId" in det
        assert "entityType" in det
        assert "startOffset" in det
        assert "endOffset" in det
        assert "detectionSource" in det
        assert "originalText" in det
        assert "confidenceTier" in det

        # Verify no snake_case keys when using by_alias
        assert "segment_id" not in det
        assert "entity_type" not in det

    def test_segment_camel_case_round_trip(self):
        """DetectionSegment should round-trip through camelCase."""
        seg = DetectionSegment(
            segment_id="seg-1",
            segment_index=0,
            text_content="test",
            source_type="text",
        )
        dumped = seg.model_dump(by_alias=True)
        assert dumped["segmentId"] == "seg-1"
        assert dumped["segmentIndex"] == 0
        assert dumped["textContent"] == "test"
        assert dumped["sourceType"] == "text"

        # Re-parse from camelCase
        seg2 = DetectionSegment(**dumped)
        assert seg2.segment_id == "seg-1"

    def test_confidence_tier_in_response(self):
        """DetectionResult should include confidence_tier field."""
        result = DetectionResult(
            segment_id="seg-1",
            entity_type="PERSON",
            start_offset=0,
            end_offset=5,
            confidence=0.85,
            detection_source="ner",
            confidence_tier="MEDIUM",
        )
        data = result.model_dump(by_alias=True)
        assert data["confidenceTier"] == "MEDIUM"

    def test_confidence_tier_optional(self):
        """confidence_tier should be optional (None by default)."""
        result = DetectionResult(
            segment_id="seg-1",
            entity_type="EMAIL_ADDRESS",
            start_offset=0,
            end_offset=20,
            confidence=0.99,
            detection_source="regex",
        )
        assert result.confidence_tier is None
