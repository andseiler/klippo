"""Red-team test cases for PII detection edge cases.

These tests cover adversarial and tricky inputs:
- Names that are common German words (Kurz, Klein, Lang)
- Mixed-language text
- IBANs with unusual separators
- Phone numbers in non-standard formats
- PII adjacent to special characters

Failures are informational — they document known limitations, not blockers.
Run: pytest tests/test_red_team.py -v
"""

from __future__ import annotations

import pytest

from app.detection.layer1_regex.engine import create_layer1_engine, run_layer1

pytestmark = pytest.mark.red_team


@pytest.fixture(scope="module")
def l1_engine():
    return create_layer1_engine()


class TestCommonWordNames:
    """Names that are also common German words."""

    def test_name_kurz(self, l1_engine):
        """'Kurz' is a common surname but also means 'short'."""
        text = "Herr Sebastian Kurz hat den Vertrag unterschrieben."
        results = run_layer1(l1_engine, text, "de")
        # Regex layer won't detect this — it's expected to be caught by NER
        # This documents that L1 alone misses person names
        entity_types = [r.entity_type for r in results]
        # Informational: log whether PERSON was found
        print(f"  Entities found for 'Kurz': {entity_types}")

    def test_name_klein(self, l1_engine):
        """'Klein' means 'small' in German."""
        text = "Dr. Anna Klein ist die Ansprechpartnerin."
        results = run_layer1(l1_engine, text, "de")
        entity_types = [r.entity_type for r in results]
        print(f"  Entities found for 'Klein': {entity_types}")

    def test_name_lang(self, l1_engine):
        """'Lang' means 'long' in German."""
        text = "Kontaktperson: Peter Lang, Tel: +49 89 12345678."
        results = run_layer1(l1_engine, text, "de")
        phones = [r for r in results if r.entity_type == "PHONE_NUMBER"]
        assert len(phones) >= 1, "Should at least detect the phone number"


class TestMixedLanguageText:
    """Documents mixing German and English."""

    def test_german_english_mix(self, l1_engine):
        text = (
            "Dear Mr. Müller, your IBAN DE89 3704 0044 0532 0130 00 "
            "has been verified. Please contact support@example.com."
        )
        results = run_layer1(l1_engine, text, "de")
        entity_types = [r.entity_type for r in results]
        assert "IBAN" in entity_types, "Should detect IBAN in mixed-language text"
        assert "EMAIL" in entity_types, "Should detect email in mixed-language text"

    def test_french_german_mix(self, l1_engine):
        text = "Monsieur Jean-Pierre Dupont, IBAN: FR76 3000 6000 0112 3456 7890 189."
        results = run_layer1(l1_engine, text, "de")
        ibans = [r for r in results if r.entity_type == "IBAN"]
        assert len(ibans) >= 1, "Should detect French IBAN"


class TestUnusualIbanFormats:
    """IBANs with non-standard separators."""

    def test_iban_no_spaces(self, l1_engine):
        text = "IBAN: DE89370400440532013000."
        results = run_layer1(l1_engine, text, "de")
        ibans = [r for r in results if r.entity_type == "IBAN"]
        assert len(ibans) >= 1, "Should detect IBAN without spaces"

    def test_iban_with_dashes(self, l1_engine):
        text = "IBAN: DE89-3704-0044-0532-0130-00."
        results = run_layer1(l1_engine, text, "de")
        ibans = [r for r in results if r.entity_type == "IBAN"]
        # This is a known edge case — document the result
        print(f"  IBANs with dashes detected: {len(ibans)}")

    def test_iban_lowercase(self, l1_engine):
        text = "iban: de89370400440532013000"
        results = run_layer1(l1_engine, text, "de")
        ibans = [r for r in results if r.entity_type == "IBAN"]
        print(f"  Lowercase IBAN detected: {len(ibans)}")


class TestNonStandardPhoneFormats:
    """Phone numbers in unusual formats."""

    def test_phone_with_slash(self, l1_engine):
        text = "Telefon: 030/12345678"
        results = run_layer1(l1_engine, text, "de")
        phones = [r for r in results if r.entity_type == "PHONE_NUMBER"]
        print(f"  Phone with slash detected: {len(phones)}")

    def test_phone_no_country_code(self, l1_engine):
        text = "Rückruf unter 089 12345678."
        results = run_layer1(l1_engine, text, "de")
        phones = [r for r in results if r.entity_type == "PHONE_NUMBER"]
        print(f"  Phone without country code detected: {len(phones)}")

    def test_mobile_with_parentheses(self, l1_engine):
        text = "Mobil: (0171) 1234567"
        results = run_layer1(l1_engine, text, "de")
        phones = [r for r in results if r.entity_type == "PHONE_NUMBER"]
        print(f"  Mobile with parens detected: {len(phones)}")

    def test_austrian_phone(self, l1_engine):
        text = "Erreichbar unter +43 1 51450."
        results = run_layer1(l1_engine, text, "de")
        phones = [r for r in results if r.entity_type == "PHONE_NUMBER"]
        assert len(phones) >= 1, "Should detect Austrian phone number"

    def test_swiss_phone(self, l1_engine):
        text = "Kontakt: +41 44 668 18 00."
        results = run_layer1(l1_engine, text, "de")
        phones = [r for r in results if r.entity_type == "PHONE_NUMBER"]
        assert len(phones) >= 1, "Should detect Swiss phone number"


class TestPiiAdjacentToSpecialChars:
    """PII surrounded by special characters, brackets, quotes."""

    def test_email_in_angle_brackets(self, l1_engine):
        text = "Kontakt: <max.mustermann@example.com>"
        results = run_layer1(l1_engine, text, "de")
        emails = [r for r in results if r.entity_type == "EMAIL"]
        assert len(emails) >= 1, "Should detect email in angle brackets"

    def test_iban_in_parentheses(self, l1_engine):
        text = "(IBAN: DE89370400440532013000)"
        results = run_layer1(l1_engine, text, "de")
        ibans = [r for r in results if r.entity_type == "IBAN"]
        assert len(ibans) >= 1, "Should detect IBAN in parentheses"

    def test_email_with_plus(self, l1_engine):
        text = "E-Mail: max.mustermann+test@example.com"
        results = run_layer1(l1_engine, text, "de")
        emails = [r for r in results if r.entity_type == "EMAIL"]
        assert len(emails) >= 1, "Should detect email with plus addressing"

    def test_pii_after_newline(self, l1_engine):
        text = "Bankdaten:\nDE89370400440532013000\nmax@example.com"
        results = run_layer1(l1_engine, text, "de")
        entity_types = [r.entity_type for r in results]
        assert "IBAN" in entity_types, "Should detect IBAN after newline"
        assert "EMAIL" in entity_types, "Should detect email after newline"


class TestSteuerIdEdgeCases:
    """German Steuer-ID edge cases."""

    def test_steuer_id_with_spaces(self, l1_engine):
        text = "Steuer-ID: 65 929 97024"
        results = run_layer1(l1_engine, text, "de")
        steuer = [r for r in results if "STEUER" in r.entity_type.upper() or "TAX" in r.entity_type.upper()]
        assert len(steuer) >= 1, "Should detect Steuer-ID with spaces"

    def test_steuer_id_no_spaces(self, l1_engine):
        text = "Steuer-Identifikationsnummer: 6592997024"
        results = run_layer1(l1_engine, text, "de")
        steuer = [r for r in results if "STEUER" in r.entity_type.upper() or "TAX" in r.entity_type.upper()]
        print(f"  Steuer-ID without spaces detected: {len(steuer)}")
