#!/usr/bin/env bash
set -euo pipefail

# Create a new user in the PiiGateway database via Docker.
# Usage: bash scripts/create-user.sh

echo "=== Klippo – Create User ==="
echo

read -rp "Email: " EMAIL
read -rp "Name:  " NAME
read -rsp "Password: " PASSWORD
echo

if [[ -z "$EMAIL" || -z "$NAME" || -z "$PASSWORD" ]]; then
  echo "Error: all fields are required." >&2
  exit 1
fi

# Generate UUID
USER_ID=$(python3 -c "import uuid; print(uuid.uuid4())")

# Hash password with bcrypt inside the pii-service container (has Python + bcrypt)
HASH=$(docker compose exec -T pii-service python3 -c "
import bcrypt, sys
pw = sys.stdin.buffer.read()
print(bcrypt.hashpw(pw, bcrypt.gensalt(12)).decode())
" <<< "$PASSWORD")

# Trim whitespace
HASH=$(echo "$HASH" | tr -d '[:space:]')

# Escape single quotes in name/email for SQL safety
SAFE_EMAIL="${EMAIL//\'/\'\'}"
SAFE_NAME="${NAME//\'/\'\'}"

# Insert user
docker compose exec -T postgres psql -U piigateway -d piigateway -c "
  INSERT INTO users (id, email, name, password_hash, created_at)
  VALUES ('${USER_ID}', '${SAFE_EMAIL}', '${SAFE_NAME}', '${HASH}', NOW());
"

echo
echo "User created successfully."
echo "  ID:    ${USER_ID}"
echo "  Email: ${EMAIL}"
echo "  Name:  ${NAME}"
