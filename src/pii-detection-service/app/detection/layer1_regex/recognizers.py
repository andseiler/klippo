"""Custom Presidio PatternRecognizer subclasses for DACH-region PII."""

from __future__ import annotations

from presidio_analyzer import Pattern, PatternRecognizer

from . import checksums


# ---------------------------------------------------------------------------
# German Steuer-ID
# ---------------------------------------------------------------------------
class DeSteuerIdRecognizer(PatternRecognizer):
    PATTERNS = [Pattern("de_steuer_id", r"\b\d{2}\s?\d{3}\s?\d{5}\d\b", 0.60)]
    CONTEXT = ["steuer", "steuernummer", "steuerid", "steuer-id", "tin", "tax"]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="DE_STEUER_ID",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="de",
        )

    def validate_result(self, pattern_text: str) -> bool | None:
        cleaned = pattern_text.replace(" ", "")
        if checksums.steuer_id_checksum(cleaned):
            return True
        return None  # let base class decide


# ---------------------------------------------------------------------------
# German Sozialversicherungsnummer
# ---------------------------------------------------------------------------
class DeSozialversicherungsnrRecognizer(PatternRecognizer):
    PATTERNS = [
        Pattern("de_sv_nr", r"\b\d{2}\s?\d{6}\s?[A-Z]\s?\d{3}\b", 0.60),
    ]
    CONTEXT = [
        "sozialversicherung",
        "sv-nr",
        "svnr",
        "versicherungsnummer",
        "rentenversicherung",
    ]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="DE_SOZIALVERSICHERUNGSNR",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="de",
        )

    def validate_result(self, pattern_text: str) -> bool | None:
        cleaned = pattern_text.replace(" ", "")
        if checksums.sv_nr_checksum(cleaned):
            return True
        return None


# ---------------------------------------------------------------------------
# German Handelsregister
# ---------------------------------------------------------------------------
class DeHandelsregisterRecognizer(PatternRecognizer):
    PATTERNS = [
        Pattern("de_handelsregister", r"\b(?:HRA|HRB)\s?\d{3,6}\b", 0.85),
    ]
    CONTEXT = ["handelsregister", "registergericht", "amtsgericht", "hrb", "hra"]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="DE_HANDELSREGISTER",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="de",
        )


# ---------------------------------------------------------------------------
# Swiss AHV-Nr
# ---------------------------------------------------------------------------
class ChAhvNrRecognizer(PatternRecognizer):
    PATTERNS = [
        Pattern("ch_ahv_nr", r"\b756\.\d{4}\.\d{4}\.\d{2}\b", 0.70),
    ]
    CONTEXT = ["ahv", "ahv-nr", "ahvnr", "sozialversicherung", "avs"]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="CH_AHV_NR",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="de",
        )

    def validate_result(self, pattern_text: str) -> bool | None:
        if checksums.ahv_ean13_checksum(pattern_text):
            return True
        return None


# ---------------------------------------------------------------------------
# Swiss UID
# ---------------------------------------------------------------------------
class ChUidRecognizer(PatternRecognizer):
    PATTERNS = [
        Pattern("ch_uid", r"\bCHE-\d{3}\.\d{3}\.\d{3}\b", 0.70),
    ]
    CONTEXT = ["uid", "unternehmens-identifikationsnummer", "mwst", "ust"]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="CH_UID",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="de",
        )

    def validate_result(self, pattern_text: str) -> bool | None:
        if checksums.uid_checksum(pattern_text):
            return True
        return None


# ---------------------------------------------------------------------------
# Austrian Sozialversicherungsnummer
# ---------------------------------------------------------------------------
class AtSozialversicherungsnrRecognizer(PatternRecognizer):
    PATTERNS = [
        Pattern("at_sv_nr", r"\b\d{4}\s?\d{6}\b", 0.40),
    ]
    CONTEXT = [
        "sv-nr",
        "svnr",
        "sozialversicherungsnummer",
        "versicherungsnummer",
        "sv-nummer",
    ]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="AT_SOZIALVERSICHERUNGSNR",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="de",
        )

    def validate_result(self, pattern_text: str) -> bool | None:
        cleaned = pattern_text.replace(" ", "")
        if checksums.at_sv_checksum(cleaned):
            return True
        return None


# ---------------------------------------------------------------------------
# DACH Phone Numbers
# ---------------------------------------------------------------------------
class PhoneDachRecognizer(PatternRecognizer):
    PATTERNS = [
        Pattern(
            "phone_dach",
            r"(?:^|(?<=\s))(?:\+\s?(?:49|43|41)|0)\s?[\d\s/\-()]{6,15}\b",
            0.50,
        ),
    ]
    CONTEXT = ["telefon", "tel", "phone", "handy", "mobil", "fax", "anrufen", "rufnummer"]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="PHONE_DACH",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="de",
        )

    def validate_result(self, pattern_text: str) -> bool | None:
        if checksums.phone_plausibility(pattern_text):
            return True
        return None


# ---------------------------------------------------------------------------
# Date of Birth
# ---------------------------------------------------------------------------
class DateOfBirthRecognizer(PatternRecognizer):
    PATTERNS = [
        Pattern(
            "dob_de",
            r"(?i)(?:geboren|geb\.|geb|geburtsdatum|birth|born|dob)"
            r"[:\s]*(?:am\s+|on\s+)?\d{1,2}[./\-]\d{1,2}[./\-]\d{2,4}",
            0.70,
        ),
    ]
    CONTEXT = ["geboren", "geburtsdatum", "geb", "birth", "born", "dob"]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="DATE_OF_BIRTH",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="de",
        )

    def validate_result(self, pattern_text: str) -> bool | None:
        import re

        # Extract just the date portion
        match = re.search(r"\d{1,2}[./\-]\d{1,2}[./\-]\d{2,4}", pattern_text)
        if match:
            date_str = match.group()
            # Normalize separators
            date_str = date_str.replace("/", ".").replace("-", ".")
            if checksums.date_plausibility(date_str):
                return True
        return None


# ---------------------------------------------------------------------------
# UK National Insurance Number
# ---------------------------------------------------------------------------
class UkNationalInsuranceRecognizer(PatternRecognizer):
    PATTERNS = [
        Pattern(
            "uk_ni",
            r"\b[A-CEGHJ-PR-TW-Z]{2}\s?\d{2}\s?\d{2}\s?\d{2}\s?[A-D]\b",
            0.60,
        ),
    ]
    CONTEXT = ["national insurance", "ni number", "nino", "insurance number"]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="UK_NATIONAL_INSURANCE",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="en",
        )

    def validate_result(self, pattern_text: str) -> bool | None:
        if checksums.ni_number_prefix_check(pattern_text):
            return True
        return None


# ---------------------------------------------------------------------------
# US SSN
# ---------------------------------------------------------------------------
class UsSsnRecognizer(PatternRecognizer):
    PATTERNS = [
        Pattern("us_ssn", r"\b\d{3}[-\s]?\d{2}[-\s]?\d{4}\b", 0.40),
    ]
    CONTEXT = ["ssn", "social security", "social security number"]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="US_SSN",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="en",
        )

    def validate_result(self, pattern_text: str) -> bool | None:
        if checksums.ssn_area_check(pattern_text):
            return True
        return None


# ---------------------------------------------------------------------------
# Passport Number (keyword-prefixed)
# ---------------------------------------------------------------------------
class PassportNumberRecognizer(PatternRecognizer):
    PATTERNS = [
        Pattern(
            "passport",
            r"(?i)(?:reisepass|passport|pass-nr|passnummer|pass\s*nr)"
            r"[:\s]*[A-Z0-9]{6,9}",
            0.85,
        ),
    ]
    CONTEXT = ["reisepass", "passport", "passnummer", "pass-nr"]

    def __init__(self) -> None:
        super().__init__(
            supported_entity="PASSPORT_NUMBER",
            patterns=self.PATTERNS,
            context=self.CONTEXT,
            supported_language="de",
        )


def get_all_custom_recognizers() -> list[PatternRecognizer]:
    """Return instances of all custom recognizers."""
    return [
        DeSteuerIdRecognizer(),
        DeSozialversicherungsnrRecognizer(),
        DeHandelsregisterRecognizer(),
        ChAhvNrRecognizer(),
        ChUidRecognizer(),
        AtSozialversicherungsnrRecognizer(),
        PhoneDachRecognizer(),
        DateOfBirthRecognizer(),
        UkNationalInsuranceRecognizer(),
        UsSsnRecognizer(),
        PassportNumberRecognizer(),
    ]
