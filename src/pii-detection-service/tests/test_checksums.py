"""Unit tests for all checksum/validation functions."""

from __future__ import annotations

from app.detection.layer1_regex.checksums import (
    ahv_ean13_checksum,
    at_sv_checksum,
    date_plausibility,
    iban_mod97_checksum,
    ni_number_prefix_check,
    phone_plausibility,
    ssn_area_check,
    steuer_id_checksum,
    sv_nr_checksum,
    uid_checksum,
)


class TestSteuerIdChecksum:
    def test_valid_steuer_id(self):
        assert steuer_id_checksum("65929970026") is True

    def test_invalid_check_digit(self):
        assert steuer_id_checksum("65929970024") is False

    def test_wrong_length(self):
        assert steuer_id_checksum("1234567890") is False
        assert steuer_id_checksum("123456789012") is False

    def test_with_spaces(self):
        assert steuer_id_checksum("65 929 97002 6") is True

    def test_non_numeric(self):
        assert steuer_id_checksum("6592997002a") is False


class TestSvNrChecksum:
    def test_valid_sv_nr(self):
        # Structure: area(2) + DOB(DDMMYY)(6) + letter + serial(3)
        assert sv_nr_checksum("12010185M123") is True

    def test_invalid_date(self):
        assert sv_nr_checksum("12320185M123") is False  # Day 32

    def test_wrong_length(self):
        assert sv_nr_checksum("12010185M12") is False

    def test_missing_letter(self):
        assert sv_nr_checksum("120101851123") is False

    def test_with_spaces(self):
        assert sv_nr_checksum("12 010185 M 123") is True


class TestAhvEan13Checksum:
    def test_valid_ahv(self):
        assert ahv_ean13_checksum("7561234567897") is True

    def test_invalid_checksum(self):
        assert ahv_ean13_checksum("7561234567890") is False

    def test_wrong_prefix(self):
        assert ahv_ean13_checksum("1231234567897") is False

    def test_with_dots(self):
        assert ahv_ean13_checksum("756.1234.5678.97") is True

    def test_wrong_length(self):
        assert ahv_ean13_checksum("756123456789") is False


class TestUidChecksum:
    def test_valid_uid(self):
        assert uid_checksum("CHE-123.456.788") is True

    def test_invalid_checksum(self):
        assert uid_checksum("CHE-123.456.789") is False

    def test_without_prefix(self):
        assert uid_checksum("123456788") is True

    def test_wrong_length(self):
        assert uid_checksum("CHE-12.456.789") is False


class TestAtSvChecksum:
    def test_valid_at_sv(self):
        # 4-digit insurance number + 6-digit DOB (DDMMYY)
        assert at_sv_checksum("1234010185") is True

    def test_invalid_date(self):
        assert at_sv_checksum("1234320185") is False  # Day 32

    def test_wrong_length(self):
        assert at_sv_checksum("123401018") is False

    def test_with_spaces(self):
        assert at_sv_checksum("1234 010185") is True


class TestIbanMod97:
    def test_valid_german_iban(self):
        assert iban_mod97_checksum("DE89370400440532013000") is True

    def test_valid_german_iban_with_spaces(self):
        assert iban_mod97_checksum("DE89 3704 0044 0532 0130 00") is True

    def test_valid_swiss_iban(self):
        assert iban_mod97_checksum("CH9300762011623852957") is True

    def test_valid_austrian_iban(self):
        assert iban_mod97_checksum("AT611904300234573201") is True

    def test_invalid_iban(self):
        assert iban_mod97_checksum("DE89370400440532013001") is False

    def test_too_short(self):
        assert iban_mod97_checksum("DE8937040") is False

    def test_invalid_country_code(self):
        assert iban_mod97_checksum("12893704004405320130") is False


class TestPhonePlausibility:
    def test_german_mobile(self):
        assert phone_plausibility("+49 170 1234567") is True

    def test_german_landline(self):
        assert phone_plausibility("+49 30 12345678") is True

    def test_austrian_number(self):
        assert phone_plausibility("+43 1 1234567") is True

    def test_swiss_number(self):
        assert phone_plausibility("+41 44 1234567") is True

    def test_local_format(self):
        assert phone_plausibility("030 12345678") is True

    def test_too_short(self):
        assert phone_plausibility("+49 123") is False

    def test_non_dach_country(self):
        assert phone_plausibility("+1 555 1234567") is False


class TestDatePlausibility:
    def test_valid_german_format(self):
        assert date_plausibility("15.03.1985") is True

    def test_valid_iso_format(self):
        assert date_plausibility("1985-03-15") is True

    def test_valid_slash_format(self):
        assert date_plausibility("15/03/1985") is True

    def test_future_date(self):
        assert date_plausibility("15.03.2099") is False

    def test_too_old(self):
        assert date_plausibility("15.03.1850") is False

    def test_invalid_date(self):
        assert date_plausibility("32.13.1985") is False

    def test_garbage(self):
        assert date_plausibility("not a date") is False


class TestNiNumberPrefixCheck:
    def test_valid_ni(self):
        assert ni_number_prefix_check("AB 12 34 56 A") is True

    def test_excluded_first_char_d(self):
        assert ni_number_prefix_check("DA123456A") is False

    def test_excluded_first_char_f(self):
        assert ni_number_prefix_check("FA123456A") is False

    def test_excluded_prefix_bg(self):
        assert ni_number_prefix_check("BG123456A") is False

    def test_excluded_prefix_gb(self):
        assert ni_number_prefix_check("GB123456A") is False

    def test_wrong_length(self):
        assert ni_number_prefix_check("AB1234") is False


class TestSsnAreaCheck:
    def test_valid_ssn(self):
        assert ssn_area_check("123-45-6789") is True

    def test_area_000(self):
        assert ssn_area_check("000-45-6789") is False

    def test_area_666(self):
        assert ssn_area_check("666-45-6789") is False

    def test_area_900_plus(self):
        assert ssn_area_check("900-45-6789") is False
        assert ssn_area_check("999-45-6789") is False

    def test_group_00(self):
        assert ssn_area_check("123-00-6789") is False

    def test_serial_0000(self):
        assert ssn_area_check("123-45-0000") is False

    def test_no_separators(self):
        assert ssn_area_check("123456789") is True

    def test_wrong_length(self):
        assert ssn_area_check("12345678") is False
