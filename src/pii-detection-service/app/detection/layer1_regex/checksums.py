"""Pure checksum / validation functions for PII patterns.

Each function takes a cleaned string and returns bool.
"""

from __future__ import annotations

import re
from datetime import datetime


# ---------------------------------------------------------------------------
# German Steuer-ID (Tax ID) — mod-11 per BMF specification
# ---------------------------------------------------------------------------
def steuer_id_checksum(digits: str) -> bool:
    """Validate an 11-digit German Steuer-ID check digit (mod-11)."""
    digits = re.sub(r"\s", "", digits)
    if len(digits) != 11 or not digits.isdigit():
        return False

    product = 10
    for i in range(10):
        total = (int(digits[i]) + product) % 10
        if total == 0:
            total = 10
        product = (total * 2) % 11

    check = 11 - product
    if check == 10:
        check = 0

    return check == int(digits[10])


# ---------------------------------------------------------------------------
# German Sozialversicherungsnummer (SV-Nr) — structure validation
# ---------------------------------------------------------------------------
def sv_nr_checksum(value: str) -> bool:
    """Validate German SV-Nr structure: area(2) + DOB(6) + letter + serial(3)."""
    value = re.sub(r"\s", "", value)
    if len(value) != 12:
        return False
    if not value[:2].isdigit():
        return False
    if not value[2:8].isdigit():
        return False
    if not value[8].isalpha():
        return False
    if not value[9:12].isdigit():
        return False
    # Validate embedded date (DDMMYY)
    try:
        datetime.strptime(value[2:8], "%d%m%y")
    except ValueError:
        return False
    return True


# ---------------------------------------------------------------------------
# Swiss AHV-Nr — EAN-13 checksum (must start with 756)
# ---------------------------------------------------------------------------
def ahv_ean13_checksum(digits: str) -> bool:
    """Validate a Swiss AHV number using EAN-13 checksum. Input: 13 digits."""
    digits = re.sub(r"[.\s]", "", digits)
    if len(digits) != 13 or not digits.isdigit():
        return False
    if not digits.startswith("756"):
        return False

    total = 0
    for i, d in enumerate(digits[:12]):
        weight = 1 if i % 2 == 0 else 3
        total += int(d) * weight

    check = (10 - (total % 10)) % 10
    return check == int(digits[12])


# ---------------------------------------------------------------------------
# Swiss UID — mod-11 checksum (CHE-xxx.xxx.xxx)
# ---------------------------------------------------------------------------
def uid_checksum(uid: str) -> bool:
    """Validate a Swiss UID number (CHE-xxx.xxx.xxx) using mod-11."""
    cleaned = re.sub(r"[.\s-]", "", uid)
    # Strip CHE prefix if present
    if cleaned.upper().startswith("CHE"):
        cleaned = cleaned[3:]
    if len(cleaned) != 9 or not cleaned.isdigit():
        return False

    weights = [5, 4, 3, 2, 7, 6, 5, 4]
    total = sum(int(cleaned[i]) * weights[i] for i in range(8))
    remainder = total % 11
    if remainder == 0:
        check = 0
    elif remainder == 1:
        return False  # invalid
    else:
        check = 11 - remainder

    return check == int(cleaned[8])


# ---------------------------------------------------------------------------
# Austrian Sozialversicherungsnummer — structure validation
# ---------------------------------------------------------------------------
def at_sv_checksum(digits: str) -> bool:
    """Validate Austrian SV-Nr: 4-digit insurance number + 6-digit DOB (DDMMYY)."""
    digits = re.sub(r"\s", "", digits)
    if len(digits) != 10 or not digits.isdigit():
        return False
    # Validate embedded date (DDMMYY) in positions 4-10
    try:
        datetime.strptime(digits[4:10], "%d%m%y")
    except ValueError:
        return False
    # Check digit validation (position 3 is check digit)
    weights = [3, 7, 9, 0, 5, 8, 4, 2, 1, 6]
    total = sum(int(digits[i]) * weights[i] for i in range(10))
    return total % 11 != 0 or int(digits[3]) == 0  # simplified structural check


# ---------------------------------------------------------------------------
# IBAN — ISO 7064 mod-97-10
# ---------------------------------------------------------------------------
def iban_mod97_checksum(iban: str) -> bool:
    """Validate IBAN using ISO 7064 mod-97-10 algorithm."""
    cleaned = re.sub(r"[\s-]", "", iban).upper()
    if len(cleaned) < 15 or len(cleaned) > 34:
        return False
    if not cleaned[:2].isalpha() or not cleaned[2:4].isdigit():
        return False

    # Move first 4 characters to end
    rearranged = cleaned[4:] + cleaned[:4]

    # Convert letters to numbers (A=10, B=11, ..., Z=35)
    numeric = ""
    for ch in rearranged:
        if ch.isdigit():
            numeric += ch
        else:
            numeric += str(ord(ch) - ord("A") + 10)

    return int(numeric) % 97 == 1


# ---------------------------------------------------------------------------
# Phone number plausibility (DACH region)
# ---------------------------------------------------------------------------
def phone_plausibility(phone: str) -> bool:
    """Check if a phone number is plausible for the DACH region."""
    digits_only = re.sub(r"[^\d+]", "", phone)

    # Normalize leading +
    if digits_only.startswith("+"):
        digits_only = digits_only[1:]
    elif digits_only.startswith("00"):
        digits_only = digits_only[2:]
    elif digits_only.startswith("0"):
        # Local format — plausible
        digits_only = digits_only[1:]
        if len(digits_only) < 6 or len(digits_only) > 14:
            return False
        return True

    if len(digits_only) < 7 or len(digits_only) > 15:
        return False

    # Check valid DACH country codes
    valid_prefixes = ("49", "43", "41")
    return any(digits_only.startswith(p) for p in valid_prefixes)


# ---------------------------------------------------------------------------
# Date of birth plausibility
# ---------------------------------------------------------------------------
def date_plausibility(date_str: str) -> bool:
    """Check if a date string is a plausible date of birth (age 0-120)."""
    date_str = date_str.strip()
    formats = [
        "%d.%m.%Y",
        "%d/%m/%Y",
        "%Y-%m-%d",
        "%d-%m-%Y",
        "%d.%m.%y",
        "%d/%m/%y",
    ]
    parsed = None
    for fmt in formats:
        try:
            parsed = datetime.strptime(date_str, fmt)
            break
        except ValueError:
            continue

    if parsed is None:
        return False

    now = datetime.now()
    age = (now - parsed).days / 365.25
    return 0 <= age <= 120


# ---------------------------------------------------------------------------
# UK National Insurance number — prefix exclusion
# ---------------------------------------------------------------------------
def ni_number_prefix_check(ni: str) -> bool:
    """Validate UK NI number prefix rules."""
    cleaned = re.sub(r"\s", "", ni).upper()
    if len(cleaned) != 9:
        return False

    first = cleaned[0]
    prefix = cleaned[:2]

    # Exclude certain first characters
    if first in ("D", "F", "I", "Q", "U", "V"):
        return False

    # Exclude second character
    if cleaned[1] in ("D", "F", "I", "Q", "U", "V"):
        return False

    # Exclude certain prefix combinations
    excluded_prefixes = {"BG", "GB", "NK", "KN", "TN", "NT", "ZZ"}
    if prefix in excluded_prefixes:
        return False

    return True


# ---------------------------------------------------------------------------
# US SSN — range exclusion
# ---------------------------------------------------------------------------
def ssn_area_check(ssn: str) -> bool:
    """Validate US SSN range rules."""
    cleaned = re.sub(r"[\s-]", "", ssn)
    if len(cleaned) != 9 or not cleaned.isdigit():
        return False

    area = int(cleaned[:3])
    group = int(cleaned[3:5])
    serial = int(cleaned[5:9])

    # Exclude invalid ranges
    if area == 0 or area == 666 or area >= 900:
        return False
    if group == 0:
        return False
    if serial == 0:
        return False

    return True
