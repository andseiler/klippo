import pytest
from fastapi.testclient import TestClient

from app.main import app


@pytest.fixture(scope="module")
def client():
    with TestClient(app) as c:
        yield c


def test_health_returns_200(client):
    response = client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"
    assert data["version"] == "0.1.0"
    assert "regex" in data["layers_available"]


def test_detect_empty_segments(client):
    response = client.post(
        "/api/detect",
        json={
            "job_id": "00000000-0000-0000-0000-000000000001",
            "segments": [],
            "layers": [],
            "language_hint": "de",
        },
    )
    assert response.status_code == 200
    data = response.json()
    assert data["detections"] == []
    assert "layersUsed" in data or "layers_used" in data


def test_detect_accepts_camel_case(client):
    response = client.post(
        "/api/detect",
        json={
            "jobId": "00000000-0000-0000-0000-000000000001",
            "segments": [],
            "layers": [],
            "languageHint": "de",
        },
    )
    assert response.status_code == 200
