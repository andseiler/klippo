"""Tests for regex pattern recognizers."""

from __future__ import annotations

import re

from app.detection.layer1_regex.recognizers import (
    AtSozialversicherungsnrRecognizer,
    ChAhvNrRecognizer,
    ChUidRecognizer,
    DateOfBirthRecognizer,
    DeHandelsregisterRecognizer,
    DeSozialversicherungsnrRecognizer,
    DeSteuerIdRecognizer,
    PassportNumberRecognizer,
    PhoneDachRecognizer,
    UkNationalInsuranceRecognizer,
    UsSsnRecognizer,
)


def _pattern_matches(recognizer_cls, text: str) -> bool:
    """Check if any pattern in the recognizer matches the text."""
    rec = recognizer_cls()
    for pattern in rec.patterns:
        if re.search(pattern.regex, text):
            return True
    return False


class TestDeSteuerIdPattern:
    def test_matches_valid_format(self):
        assert _pattern_matches(DeSteuerIdRecognizer, "Steuer-ID: 65929970024")

    def test_matches_with_spaces(self):
        assert _pattern_matches(DeSteuerIdRecognizer, "65 929 970024")

    def test_no_match_short(self):
        assert not _pattern_matches(DeSteuerIdRecognizer, "1234567890")


class TestDeSozialversicherungsnrPattern:
    def test_matches_valid_format(self):
        assert _pattern_matches(DeSozialversicherungsnrRecognizer, "12 010185 M 123")

    def test_matches_compact(self):
        assert _pattern_matches(DeSozialversicherungsnrRecognizer, "12010185M123")


class TestDeHandelsregisterPattern:
    def test_matches_hrb(self):
        assert _pattern_matches(DeHandelsregisterRecognizer, "HRB 12345")

    def test_matches_hra(self):
        assert _pattern_matches(DeHandelsregisterRecognizer, "HRA 456")

    def test_no_match_hrc(self):
        assert not _pattern_matches(DeHandelsregisterRecognizer, "HRC 12345")


class TestChAhvNrPattern:
    def test_matches_dotted_format(self):
        assert _pattern_matches(ChAhvNrRecognizer, "756.1234.5678.97")

    def test_no_match_wrong_prefix(self):
        assert not _pattern_matches(ChAhvNrRecognizer, "123.1234.5678.97")


class TestChUidPattern:
    def test_matches_standard_format(self):
        assert _pattern_matches(ChUidRecognizer, "CHE-123.456.788")

    def test_no_match_without_che(self):
        assert not _pattern_matches(ChUidRecognizer, "123.456.788")


class TestAtSozialversicherungsnrPattern:
    def test_matches_valid(self):
        assert _pattern_matches(AtSozialversicherungsnrRecognizer, "SV-Nr: 1234 010185")

    def test_matches_compact(self):
        assert _pattern_matches(AtSozialversicherungsnrRecognizer, "1234010185")


class TestPhoneDachPattern:
    def test_matches_german_international(self):
        assert _pattern_matches(PhoneDachRecognizer, "+49 30 12345678")

    def test_matches_austrian(self):
        assert _pattern_matches(PhoneDachRecognizer, "+43 1 1234567")

    def test_matches_swiss(self):
        assert _pattern_matches(PhoneDachRecognizer, "+41 44 1234567")

    def test_matches_local(self):
        assert _pattern_matches(PhoneDachRecognizer, "030 12345678")


class TestDateOfBirthPattern:
    def test_matches_geboren(self):
        assert _pattern_matches(DateOfBirthRecognizer, "geboren am 15.03.1985")

    def test_matches_geb(self):
        assert _pattern_matches(DateOfBirthRecognizer, "geb. 15/03/1985")

    def test_matches_geburtsdatum(self):
        assert _pattern_matches(DateOfBirthRecognizer, "Geburtsdatum: 01.01.2000")

    def test_no_match_standalone_date(self):
        # Pattern requires keyword prefix
        assert not _pattern_matches(DateOfBirthRecognizer, "15.03.1985")


class TestUkNationalInsurancePattern:
    def test_matches_valid(self):
        assert _pattern_matches(UkNationalInsuranceRecognizer, "AB 12 34 56 A")

    def test_matches_compact(self):
        assert _pattern_matches(UkNationalInsuranceRecognizer, "AB123456A")


class TestUsSsnPattern:
    def test_matches_with_dashes(self):
        assert _pattern_matches(UsSsnRecognizer, "123-45-6789")

    def test_matches_compact(self):
        assert _pattern_matches(UsSsnRecognizer, "123456789")

    def test_matches_with_spaces(self):
        assert _pattern_matches(UsSsnRecognizer, "123 45 6789")


class TestPassportNumberPattern:
    def test_matches_reisepass(self):
        assert _pattern_matches(PassportNumberRecognizer, "Reisepass: C01X00T47")

    def test_matches_passport(self):
        assert _pattern_matches(PassportNumberRecognizer, "Passport AB123456")

    def test_no_match_standalone_code(self):
        assert not _pattern_matches(PassportNumberRecognizer, "AB123456")
