"""Detection pipeline: orchestrates L1/L2/L3 layers with timing and merge."""

from __future__ import annotations

import time

import structlog
from presidio_analyzer import AnalyzerEngine

from app.config import Settings
from app.detection.layer1_regex.engine import run_layer1
from app.detection.layer2_ner.engine import run_layer2
from app.detection.layer2_ner.language_detect import detect_language
from app.detection.layer3_llm.llm_client import Layer3LLM
from app.detection.merger import RawDetection, merge_detections
from app.models import DetectionResult, DetectRequest, DetectResponse, LayerStat

logger = structlog.get_logger()


class DetectionPipeline:
    """Orchestrates multi-layer PII detection across segments."""

    def __init__(
        self,
        settings: Settings,
        l1_engine: AnalyzerEngine,
        l2_engine: AnalyzerEngine | None,
        l3_llm: Layer3LLM,
    ) -> None:
        self.settings = settings
        self.l1_engine = l1_engine
        self.l2_engine = l2_engine
        self.l3_llm = l3_llm

    def _available_layers(self) -> list[str]:
        layers = ["regex"]
        if self.l2_engine is not None:
            layers.append("ner")
        if self.l3_llm.backend != "disabled":
            layers.append("llm")
        return layers

    async def detect(self, request: DetectRequest) -> DetectResponse:
        """Run detection pipeline on all segments in the request."""
        start_time = time.monotonic()
        all_detections: list[DetectionResult] = []
        layers_used: set[str] = set()

        # Determine which layers to run
        requested_layers = set(request.layers) if request.layers else set(self._available_layers())

        # Per-layer tracking
        layer_counts: dict[str, int] = {"regex": 0, "ner": 0, "llm": 0}
        layer_failed: dict[str, bool] = {"regex": False, "ner": False, "llm": False}

        for segment in request.segments:
            text = segment.text_content
            if not text or not text.strip():
                continue

            # Detect language
            lang = detect_language(text, fallback=request.language_hint or "de")

            # Layer 1: Regex + Checksum (always available)
            l1_results: list[RawDetection] = []
            if "regex" in requested_layers:
                try:
                    l1_results = run_layer1(self.l1_engine, text, lang)
                    layers_used.add("regex")
                    layer_counts["regex"] += len(l1_results)
                except Exception:
                    logger.exception("layer1_failed", segment_id=segment.segment_id)
                    layer_failed["regex"] = True

            # Layer 2: NER
            l2_results: list[RawDetection] = []
            if "ner" in requested_layers and self.l2_engine is not None:
                try:
                    l2_results = run_layer2(self.l2_engine, text, lang)
                    layers_used.add("ner")
                    layer_counts["ner"] += len(l2_results)
                except Exception:
                    logger.exception("layer2_failed", segment_id=segment.segment_id)
                    layer_failed["ner"] = True

            # Layer 3: LLM
            l3_results: list[RawDetection] = []
            if "llm" in requested_layers and self.l3_llm.backend != "disabled":
                try:
                    # Use L1+L2 results if they ran, otherwise fall back to existing_detections
                    if l1_results or l2_results:
                        already = [
                            f"{d.entity_type} at pos {d.start}-{d.end}"
                            for d in l1_results + l2_results
                        ]
                    elif request.existing_detections:
                        already = [
                            f"{d.entity_type} at pos {d.start_offset}-{d.end_offset}"
                            for d in request.existing_detections
                        ]
                    else:
                        already = []
                    l3_results = await self.l3_llm.detect_additional_pii(
                        text, already,
                        custom_instructions=request.custom_instructions,
                        language=request.language_hint or "de",
                    )
                    layers_used.add("llm")
                    layer_counts["llm"] += len(l3_results)
                except Exception:
                    logger.exception("layer3_failed", segment_id=segment.segment_id)
                    layer_failed["llm"] = True

            # Merge results from all layers
            merged = merge_detections(
                l1_results,
                l2_results,
                l3_results,
            )

            # Convert to response model
            for det in merged:
                all_detections.append(
                    DetectionResult(
                        segment_id=segment.segment_id,
                        entity_type=det.entity_type,
                        start_offset=det.start,
                        end_offset=det.end,
                        confidence=round(det.confidence, 4),
                        detection_source=det.source,
                        original_text=det.original_text,
                        confidence_tier=det.reason,  # "HIGH", "MEDIUM", "LOW"
                    )
                )

        elapsed_ms = int((time.monotonic() - start_time) * 1000)

        # Build per-layer stats
        layer_stats: list[LayerStat] = []
        for layer_name in ("regex", "ner", "llm"):
            if layer_name not in requested_layers:
                continue
            if layer_failed[layer_name]:
                layer_stats.append(LayerStat(layer=layer_name, status="failed"))
            elif layer_name == "ner" and self.l2_engine is None:
                layer_stats.append(LayerStat(
                    layer=layer_name, status="not_available",
                    skip_reason="NER engine not loaded",
                ))
            elif layer_name == "llm" and self.l3_llm.backend == "disabled":
                layer_stats.append(LayerStat(
                    layer=layer_name, status="disabled",
                    skip_reason=f"LLM_BACKEND={self.l3_llm.backend}",
                ))
            elif layer_name in layers_used:
                layer_stats.append(LayerStat(
                    layer=layer_name, status="ran",
                    detection_count=layer_counts[layer_name],
                ))
            else:
                layer_stats.append(LayerStat(
                    layer=layer_name, status="ran",
                    detection_count=0,
                ))

        return DetectResponse(
            detections=all_detections,
            processing_time_ms=elapsed_ms,
            layers_used=sorted(layers_used),
            layer_stats=layer_stats,
        )
