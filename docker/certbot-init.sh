#!/bin/bash
# Let's Encrypt initial certificate setup
# Usage: ./certbot-init.sh yourdomain.com admin@yourdomain.com
#
# Must be run from the repo root (e.g., /opt/piigateway/)

set -euo pipefail

DOMAIN="${1:?Usage: $0 <domain> <email>}"
EMAIL="${2:?Usage: $0 <domain> <email>}"
COMPOSE_FILE="docker/docker-compose.prod.yml"
ENV_FILE=".env.production"

if [ ! -f "${ENV_FILE}" ]; then
    echo "ERROR: ${ENV_FILE} not found. Create it first."
    exit 1
fi

echo "==> Requesting Let's Encrypt certificate for ${DOMAIN}"

# Stop nginx if running (it binds port 80)
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" stop nginx 2>/dev/null || true

# Use a temporary certbot container that writes into the Docker named volumes
# used by docker-compose.prod.yml (certbot_conf and certbot_www)
# Override the entrypoint (compose defines a renewal loop as entrypoint)
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" run --rm \
  -p 80:80 \
  --entrypoint certbot \
  certbot certonly \
  --standalone \
  --email "${EMAIL}" \
  --agree-tos \
  --no-eff-email \
  -d "${DOMAIN}" \
  --preferred-challenges http

echo "==> Certificate obtained successfully"
echo "==> Starting all services..."

docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" up -d

echo "==> Done! SSL is now active for https://${DOMAIN}"
