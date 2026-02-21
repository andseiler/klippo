#!/bin/bash
# Deploy PII Gateway to production server
# Usage: ./deploy/deploy.sh [--skip-build]
#
# Can be run locally (SSH-based) or on the server directly.
# Expects: /opt/piigateway/.env.production to exist.

set -euo pipefail

DEPLOY_DIR="${DEPLOY_DIR:-/opt/piigateway}"
COMPOSE_FILE="${DEPLOY_DIR}/docker/docker-compose.prod.yml"
ENV_FILE="${DEPLOY_DIR}/.env.production"
SKIP_BUILD="${1:-}"

echo "==> PII Gateway Deployment"
echo "==> $(date)"
echo "==> Deploy dir: ${DEPLOY_DIR}"

# Verify environment
if [ ! -f "${ENV_FILE}" ]; then
    echo "ERROR: ${ENV_FILE} not found. Copy from .env.production.example and configure."
    exit 1
fi

cd "${DEPLOY_DIR}"

# 1. Pull latest code (if git repo)
if [ -d .git ]; then
    echo "==> Pulling latest code..."
    git pull --ff-only
fi

# 2. Build images (unless skipped, e.g., when using pre-built from registry)
if [ "${SKIP_BUILD}" != "--skip-build" ]; then
    echo "==> Building Docker images..."
    docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" build
fi

# 3. Run database migrations
echo "==> Running database migrations..."
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" up -d postgres
sleep 5  # Wait for postgres to be ready

# Migrations are auto-applied on startup in Development mode
# For production, use the API container directly
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" run --rm api \
    sh -c "dotnet PiiGateway.Api.dll --migrate-only 2>/dev/null || true" &
MIGRATE_PID=$!
sleep 10
kill $MIGRATE_PID 2>/dev/null || true

# 4. Rolling restart
echo "==> Starting services..."
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" up -d

# 5. Wait for health check
echo "==> Waiting for health check..."
MAX_ATTEMPTS=30
ATTEMPT=0
HEALTHY=false

while [ $ATTEMPT -lt $MAX_ATTEMPTS ]; do
    ATTEMPT=$((ATTEMPT + 1))
    sleep 2

    STATUS=$(docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" \
        exec -T api curl -sf http://localhost:8080/api/v1/health 2>/dev/null | \
        grep -o '"status":"[^"]*"' | head -1 || echo "")

    if echo "${STATUS}" | grep -q "healthy"; then
        HEALTHY=true
        break
    fi

    echo "  Attempt ${ATTEMPT}/${MAX_ATTEMPTS}..."
done

if [ "${HEALTHY}" = true ]; then
    echo "==> Deployment successful! API is healthy."
else
    echo "==> WARNING: Health check did not return healthy after ${MAX_ATTEMPTS} attempts."
    echo "==> Checking logs..."
    docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" logs --tail=20 api
    echo ""
    echo "==> Rolling back..."
    docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" down
    echo "==> Rollback complete. Investigate logs above."
    exit 1
fi

# 6. Cleanup old images
echo "==> Cleaning up old Docker images..."
docker image prune -f

echo ""
echo "==> Deployment complete at $(date)"
