"""Evaluate PII detection pipeline against annotated test dataset.

Usage:
    cd src/pii-detection-service
    python -m evaluation.evaluate --input evaluation/data/test_set.jsonl
"""

from __future__ import annotations

import argparse
import asyncio
import json
import sys

from evaluation.metrics import EntitySpan, compute_per_type_metrics, format_metrics_table


def _load_dataset(path: str) -> list[dict]:
    samples = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                samples.append(json.loads(line))
    return samples


async def _run_evaluation(input_path: str, layers: list[str] | None = None) -> None:
    # Import here to allow running without full model loading if only generating
    from app.config import Settings
    from app.detection.layer1_regex.engine import create_layer1_engine
    from app.detection.layer2_ner.engine import create_layer2_engine
    from app.detection.layer3_llm.llm_client import Layer3LLM
    from app.detection.pipeline import DetectionPipeline
    from app.models import DetectRequest, DetectionSegment

    settings = Settings()
    l1_engine = create_layer1_engine()

    l2_engine = None
    if settings.LAYER2_ENABLED:
        try:
            l2_engine = create_layer2_engine(settings)
        except Exception as e:
            print(f"Warning: L2 engine failed to load: {e}")

    l3_llm = Layer3LLM(settings)
    pipeline = DetectionPipeline(settings, l1_engine, l2_engine, l3_llm)

    samples = _load_dataset(input_path)
    print(f"Loaded {len(samples)} test samples from {input_path}\n")

    all_gold: list[EntitySpan] = []
    all_predicted: list[EntitySpan] = []

    for sample in samples:
        # Build gold entities
        gold_entities = [
            EntitySpan(
                entity_type=e["entity_type"],
                start=e["start"],
                end=e["end"],
                text=e["text"],
            )
            for e in sample["entities"]
        ]

        # Run detection
        request = DetectRequest(
            job_id=sample["id"],
            segments=[
                DetectionSegment(
                    segment_id=f"{sample['id']}_seg0",
                    segment_index=0,
                    text_content=sample["text"],
                    source_type="text",
                )
            ],
            document_class=sample.get("document_class", "other"),
            sensitivity="standard",
            layers=layers or [],
            language_hint="de",
        )

        response = await pipeline.detect(request)

        predicted_entities = [
            EntitySpan(
                entity_type=d.entity_type,
                start=d.start_offset,
                end=d.end_offset,
                text=d.original_text or "",
            )
            for d in response.detections
        ]

        all_gold.extend(gold_entities)
        all_predicted.extend(predicted_entities)

    # Compute metrics
    metrics = compute_per_type_metrics(all_gold, all_predicted)

    print("## PII Detection Evaluation Results\n")
    print(f"Samples: {len(samples)}")
    print(f"Gold entities: {len(all_gold)}")
    print(f"Predicted entities: {len(all_predicted)}\n")
    print(format_metrics_table(metrics))


def main() -> None:
    parser = argparse.ArgumentParser(description="Evaluate PII detection pipeline")
    parser.add_argument("--input", default="evaluation/data/test_set.jsonl", help="Input JSONL path")
    parser.add_argument("--layers", nargs="*", default=None, help="Layers to use (e.g., regex ner)")
    args = parser.parse_args()

    asyncio.run(_run_evaluation(args.input, args.layers))


if __name__ == "__main__":
    main()
