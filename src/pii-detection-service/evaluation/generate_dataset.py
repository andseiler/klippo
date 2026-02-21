"""Generate synthetic German business documents with known PII at known offsets.

Usage:
    python -m evaluation.generate_dataset --output evaluation/data/test_set.jsonl --count 20
"""

from __future__ import annotations

import argparse
import json
import os
import random
from dataclasses import asdict, dataclass

from faker import Faker

fake_de = Faker("de_DE")
Faker.seed(42)
random.seed(42)


@dataclass
class AnnotatedEntity:
    entity_type: str
    start: int
    end: int
    text: str


@dataclass
class TestSample:
    id: str
    text: str
    entities: list[AnnotatedEntity]
    document_class: str


TEMPLATES = [
    # Template 1: Contract reference
    lambda: _build_contract(),
    # Template 2: HR document
    lambda: _build_hr_doc(),
    # Template 3: Correspondence
    lambda: _build_correspondence(),
    # Template 4: Invoice
    lambda: _build_invoice(),
    # Template 5: Simple personal data
    lambda: _build_personal(),
]


def _build_contract() -> TestSample:
    name = fake_de.name()
    company = fake_de.company()
    iban = fake_de.iban()
    email = fake_de.email()

    parts: list[str] = []
    entities: list[AnnotatedEntity] = []

    parts.append("Vertrag zwischen ")
    _add(parts, entities, name, "PERSON")
    parts.append(" und ")
    _add(parts, entities, company, "ORGANIZATION")
    parts.append(". Bankverbindung: ")
    _add(parts, entities, iban, "IBAN")
    parts.append(". Kontakt: ")
    _add(parts, entities, email, "EMAIL")
    parts.append(".")

    return TestSample(
        id=f"contract_{random.randint(1000,9999)}",
        text="".join(parts),
        entities=entities,
        document_class="contract",
    )


def _build_hr_doc() -> TestSample:
    name = fake_de.name()
    dob = fake_de.date_of_birth(minimum_age=20, maximum_age=60).strftime("%d.%m.%Y")
    phone = f"+49 {random.randint(30,89)} {random.randint(10000000,99999999)}"
    email = fake_de.email()

    parts: list[str] = []
    entities: list[AnnotatedEntity] = []

    parts.append("Personalakte: ")
    _add(parts, entities, name, "PERSON")
    parts.append(", geboren am ")
    _add(parts, entities, dob, "DATE_OF_BIRTH")
    parts.append(". Telefon: ")
    _add(parts, entities, phone, "PHONE_NUMBER")
    parts.append(". E-Mail: ")
    _add(parts, entities, email, "EMAIL")
    parts.append(".")

    return TestSample(
        id=f"hr_{random.randint(1000,9999)}",
        text="".join(parts),
        entities=entities,
        document_class="hr_document",
    )


def _build_correspondence() -> TestSample:
    name = fake_de.name()
    address = fake_de.address().replace("\n", ", ")
    phone = f"+49 {random.randint(30,89)} {random.randint(10000000,99999999)}"

    parts: list[str] = []
    entities: list[AnnotatedEntity] = []

    parts.append("Sehr geehrter Herr/Frau ")
    _add(parts, entities, name, "PERSON")
    parts.append(", wir schreiben Ihnen bezüglich Ihrer Adresse: ")
    _add(parts, entities, address, "ADDRESS")
    parts.append(". Rückfragen unter ")
    _add(parts, entities, phone, "PHONE_NUMBER")
    parts.append(".")

    return TestSample(
        id=f"corr_{random.randint(1000,9999)}",
        text="".join(parts),
        entities=entities,
        document_class="correspondence",
    )


def _build_invoice() -> TestSample:
    company = fake_de.company()
    iban = fake_de.iban()
    name = fake_de.name()

    parts: list[str] = []
    entities: list[AnnotatedEntity] = []

    parts.append("Rechnung von ")
    _add(parts, entities, company, "ORGANIZATION")
    parts.append(". Bitte überweisen Sie auf IBAN ")
    _add(parts, entities, iban, "IBAN")
    parts.append(". Ansprechpartner: ")
    _add(parts, entities, name, "PERSON")
    parts.append(".")

    return TestSample(
        id=f"inv_{random.randint(1000,9999)}",
        text="".join(parts),
        entities=entities,
        document_class="invoice",
    )


def _build_personal() -> TestSample:
    name = fake_de.name()
    email = fake_de.email()
    iban = fake_de.iban()
    phone = f"+49 {random.randint(30,89)} {random.randint(10000000,99999999)}"

    parts: list[str] = []
    entities: list[AnnotatedEntity] = []

    _add(parts, entities, name, "PERSON")
    parts.append(" (")
    _add(parts, entities, email, "EMAIL")
    parts.append(") hat folgende Bankdaten: ")
    _add(parts, entities, iban, "IBAN")
    parts.append(". Tel: ")
    _add(parts, entities, phone, "PHONE_NUMBER")
    parts.append(".")

    return TestSample(
        id=f"personal_{random.randint(1000,9999)}",
        text="".join(parts),
        entities=entities,
        document_class="other",
    )


def _add(
    parts: list[str],
    entities: list[AnnotatedEntity],
    text: str,
    entity_type: str,
) -> None:
    start = sum(len(p) for p in parts)
    parts.append(text)
    entities.append(AnnotatedEntity(entity_type=entity_type, start=start, end=start + len(text), text=text))


def generate_dataset(count: int) -> list[TestSample]:
    samples: list[TestSample] = []
    for i in range(count):
        template_fn = TEMPLATES[i % len(TEMPLATES)]
        samples.append(template_fn())
    return samples


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate synthetic PII test dataset")
    parser.add_argument("--output", default="evaluation/data/test_set.jsonl", help="Output JSONL path")
    parser.add_argument("--count", type=int, default=20, help="Number of samples")
    args = parser.parse_args()

    os.makedirs(os.path.dirname(args.output), exist_ok=True)

    samples = generate_dataset(args.count)
    with open(args.output, "w", encoding="utf-8") as f:
        for sample in samples:
            f.write(json.dumps(asdict(sample), ensure_ascii=False) + "\n")

    print(f"Generated {len(samples)} samples → {args.output}")


if __name__ == "__main__":
    main()
