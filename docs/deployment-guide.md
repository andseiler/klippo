# PII Gateway — Deployment Guide (Hetzner Cloud)

Deploy the PII Gateway (Klippo) to a Hetzner Cloud VPS in Germany. This guide uses regex (Layer 1) + NER (Layer 2) for PII detection — no LLM required.

**Stack:** Vue 3 + ASP.NET Core 8 + Python FastAPI + PostgreSQL 16 + Redis 7 + Nginx + Let's Encrypt

**Server:** Hetzner CPX21 — 3 AMD vCPU, 4 GB RAM, 80 GB NVMe — ~€9.99/mo

---

## Prerequisites

Before you begin, you need:

1. **A domain name** — e.g., `pii.yourcompany.com`. You must be able to edit DNS records.
2. **A GitHub account** — with access to this repository.
3. **An SSH key pair** — if you don't have one:
   ```bash
   ssh-keygen -t ed25519 -C "your@email.com"
   cat ~/.ssh/id_ed25519.pub
   # Copy this public key — you'll need it in Step 2
   ```

---

## Step 1: Create Hetzner Cloud Account

1. Go to [https://console.hetzner.cloud](https://console.hetzner.cloud)
2. Create an account and verify your email
3. Create a new **Project** (e.g., "PII Gateway")

---

## Step 2: Provision the Server

1. In your Hetzner project, click **Add Server**
2. Configure:
   - **Location:** Falkenstein or Nuremberg (Germany)
   - **Image:** Ubuntu 24.04
   - **Type:** CPX21 (Shared vCPU, AMD, 3 vCPU, 4 GB RAM, 80 GB NVMe)
   - **Networking:** Public IPv4 (checked), IPv6 (checked)
   - **SSH Keys:** Add your public key from the prerequisites
   - **Name:** `piigateway` (or whatever you prefer)
3. Click **Create & Buy Now**
4. Note the server's **IPv4 address** (e.g., `65.108.xxx.xxx`)

---

## Step 3: Point Your Domain to the Server

Go to your domain registrar's DNS settings and create an **A record**:

| Type | Name | Value | TTL |
|------|------|-------|-----|
| A | `pii` (or `@` for root domain) | `65.108.xxx.xxx` | 300 |

Wait for DNS propagation (usually 1-5 minutes):
```bash
dig +short pii.yourcompany.com
# Should return your server IP
```

---

## Step 4: Set Up the Server

SSH into the server and run the setup script:

```bash
ssh root@YOUR_SERVER_IP
```

Once connected, download and run the setup script:

```bash
# Download the setup script
curl -fsSL https://raw.githubusercontent.com/YOUR_ORG/YOUR_REPO/main/deploy/setup-server.sh -o /tmp/setup-server.sh

# Review it (always review scripts before running as root)
cat /tmp/setup-server.sh

# Run it
bash /tmp/setup-server.sh
```

**What this does:**
- Updates system packages
- Installs Docker + Docker Compose
- Configures firewall (UFW) — ports 22, 80, 443 only
- Creates `piigateway` user with Docker access
- Creates 2 GB swap (important for NER model on 4 GB server)
- Installs fail2ban (SSH brute-force protection)
- Enables automatic security updates

---

## Step 5: Clone the Repository

```bash
# Switch to the piigateway user
su - piigateway

# Clone the repo
git clone https://github.com/YOUR_ORG/YOUR_REPO.git /opt/piigateway

cd /opt/piigateway
```

> **Private repo?** You'll need to set up a GitHub deploy key or personal access token:
> ```bash
> # Generate a deploy key on the server
> ssh-keygen -t ed25519 -f ~/.ssh/deploy_key -N ""
> cat ~/.ssh/deploy_key.pub
> # Add this as a Deploy Key in GitHub repo → Settings → Deploy keys (read-only)
>
> # Configure git to use it
> echo -e "Host github.com\n  IdentityFile ~/.ssh/deploy_key" >> ~/.ssh/config
> ```

---

## Step 6: Create Production Environment File

```bash
cd /opt/piigateway

# Copy the template
cp .env.production.example .env.production

# Generate secrets and edit the file
nano .env.production
```

Replace each `CHANGE_ME` value. Use these commands to generate secrets:

```bash
# Generate POSTGRES_PASSWORD (copy the output)
openssl rand -base64 32

# Generate REDIS_PASSWORD (copy the output)
openssl rand -base64 32

# Generate JWT_SECRET (copy the output)
openssl rand -base64 64

# Generate ENCRYPTION_KEY (copy the output)
openssl rand -base64 32
```

Your `.env.production` should look like this (with your actual values):

```env
# Domain
DOMAIN=pii.yourcompany.com

# Database
POSTGRES_PASSWORD=aB3x...your-generated-password...

# Redis
REDIS_PASSWORD=kL9m...your-generated-password...

# JWT (64+ chars)
JWT_SECRET=xY7n...your-generated-secret...

# Encryption
ENCRYPTION_KEY=pQ4r...your-generated-key...

# Container registry (update with your GitHub org/repo)
REGISTRY=ghcr.io
IMAGE_PREFIX=your-org/your-repo
VERSION=latest

# LLM disabled — regex + NER only
LLM_BACKEND=disabled
MISTRAL_API_KEY=
OLLAMA_URL=

# NER
NER_MODEL_NAME=Davlan/bert-base-multilingual-cased-ner-hrl
NER_DEVICE=cpu
LAYER2_ENABLED=true
```

**Verify no placeholders remain:**
```bash
grep 'CHANGE_ME' .env.production
# Should return nothing
```

---

## Step 7: Get SSL Certificate

```bash
cd /opt/piigateway
chmod +x docker/certbot-init.sh

# Replace with your actual domain and email
./docker/certbot-init.sh pii.yourcompany.com admin@yourcompany.com
```

**What this does:**
- Runs certbot in standalone mode on port 80
- Obtains a Let's Encrypt SSL certificate
- Stores the cert in a Docker named volume (shared with nginx)
- Certificate auto-renews via the certbot container in docker-compose

> **Troubleshooting:** If this fails, verify that:
> - DNS A record points to this server (`dig +short YOUR_DOMAIN`)
> - Port 80 is open (`sudo ufw status`)
> - No other process is using port 80 (`sudo lsof -i :80`)

---

## Step 8: First Deployment

```bash
cd /opt/piigateway
chmod +x deploy/deploy.sh

./deploy/deploy.sh
```

**What this does:**
1. Builds all Docker images (API, PII service, frontend)
2. Starts PostgreSQL, waits for it to be healthy
3. Runs database migrations
4. Starts all services (API, PII service, frontend, nginx, certbot, Redis)
5. Runs health checks (up to 30 attempts, 2s apart)
6. If health check fails, automatically rolls back

**First build takes 5-10 minutes** (subsequent builds are faster due to Docker layer caching). The PII service takes an extra 1-2 minutes to download the NER model on first start.

---

## Step 9: Verify Deployment

### Health check
```bash
curl -s https://YOUR_DOMAIN/api/v1/health
# Expected: {"status":"healthy"}
```

### Browser test
Open `https://YOUR_DOMAIN` in your browser. You should see the PII Gateway login page.

### Verify NER model loaded
```bash
cd /opt/piigateway
docker compose -f docker/docker-compose.prod.yml --env-file .env.production logs pii-service | grep -i "model\|layer\|ready"
# Should show NER model loaded successfully
```

### Check all containers are running
```bash
docker compose -f docker/docker-compose.prod.yml --env-file .env.production ps
# All services should show "Up" (except frontend-build which exits after copying files)
```

### SSL verification
```bash
curl -sI https://YOUR_DOMAIN | head -20
# Should show HTTP/2 200 and security headers
```

---

## Step 10: GitHub Actions Setup (CI/CD)

Set up automatic deployment when you push to `main`.

### Add GitHub Secrets

Go to your GitHub repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:

| Secret | Value |
|--------|-------|
| `DEPLOY_HOST` | Your server IP (e.g., `65.108.xxx.xxx`) |
| `DEPLOY_USER` | `piigateway` |
| `DEPLOY_SSH_KEY` | Contents of the SSH private key that can connect to the server |

### Generate a deploy SSH key

```bash
# On the server, as root
ssh-keygen -t ed25519 -f /tmp/deploy_key -N ""

# Add public key to piigateway user's authorized_keys
cat /tmp/deploy_key.pub >> /home/piigateway/.ssh/authorized_keys

# Copy the private key — paste it as the DEPLOY_SSH_KEY secret in GitHub
cat /tmp/deploy_key

# Clean up
rm /tmp/deploy_key /tmp/deploy_key.pub
```

### Create GitHub Environment

Go to repo → **Settings** → **Environments** → **New environment** → name it `production`.

### Trigger a deployment

Go to repo → **Actions** → **Build, Push & Deploy** → **Run workflow** → select `production` → **Run workflow**.

---

## Updating / Redeployment

### Manual redeployment (on server)

```bash
ssh piigateway@YOUR_SERVER_IP
cd /opt/piigateway
git pull --ff-only
./deploy/deploy.sh
```

### Automated (CI/CD)

Trigger from GitHub Actions (see Step 10). The workflow:
1. Builds Docker images
2. Pushes to GitHub Container Registry
3. SSHs into server, pulls latest code, runs `deploy.sh --skip-build`

### Deploying a specific version

```bash
cd /opt/piigateway
git fetch
git checkout v1.2.3  # or a specific commit
./deploy/deploy.sh
```

---

## Scaling

The Hetzner CPX21 (4 GB) handles light-to-medium workloads. If you need more capacity:

### Live resize via Hetzner Console

1. Go to [Hetzner Cloud Console](https://console.hetzner.cloud)
2. Select your server → **Rescale**
3. Choose a larger plan:

| Plan | vCPU | RAM | NVMe | Price |
|------|------|-----|------|-------|
| CPX21 | 3 | 4 GB | 80 GB | ~€9.99/mo |
| CPX31 | 4 | 8 GB | 160 GB | ~€17/mo |
| CPX41 | 8 | 16 GB | 240 GB | ~€29/mo |

4. The server reboots (~30 seconds). Docker services auto-restart.

### Signs you need to scale

- `docker stats` shows memory consistently >80%
- PII service (NER model) getting OOM-killed
- Response times increasing under load
- Swap usage consistently high (`free -h`)

---

## Rollback

### If a deployment fails

The `deploy.sh` script automatically rolls back if the health check fails after deployment. You'll see:
```
==> WARNING: Health check did not return healthy...
==> Rolling back...
==> Rollback complete. Investigate logs above.
```

### Manual rollback to previous version

```bash
cd /opt/piigateway

# See recent commits
git log --oneline -10

# Go back to a known-good commit
git checkout COMMIT_HASH

# Redeploy
./deploy/deploy.sh
```

### Emergency: stop everything

```bash
cd /opt/piigateway
docker compose -f docker/docker-compose.prod.yml --env-file .env.production down
```

### Restart after emergency stop

```bash
cd /opt/piigateway
docker compose -f docker/docker-compose.prod.yml --env-file .env.production up -d
```

---

## Troubleshooting

### Common Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| `502 Bad Gateway` | API container not ready yet | Wait 30s. Check: `docker compose logs api` |
| `503 Service Unavailable` | Nginx can't reach API | Check: `docker compose ps` — is the API running? |
| SSL cert error in browser | Certificate not yet obtained | Run `./docker/certbot-init.sh YOUR_DOMAIN YOUR_EMAIL` |
| Health returns `degraded` | PostgreSQL or Redis not connected | Check: `docker compose logs postgres redis` |
| PII service OOM killed | Not enough RAM for NER model | Scale to CPX31 (8 GB). Check: `docker compose logs pii-service` |
| `CORS error` in browser console | Domain mismatch in `.env.production` | Verify `DOMAIN=` matches your actual domain. Restart API. |
| Upload rejected | File type not in whitelist | Allowed: `.pdf`, `.docx`, `.xlsx`, `.txt`, `.csv` |
| `429 Too Many Requests` | Rate limiting triggered | Wait 1 minute. Normal for rapid requests. |
| Database connection failed | PostgreSQL not started or wrong password | Check `POSTGRES_PASSWORD` matches in `.env.production` |
| Container won't start | Port conflict | Check: `sudo lsof -i :80` and `sudo lsof -i :443` |

### Viewing Logs

```bash
cd /opt/piigateway

# All services
docker compose -f docker/docker-compose.prod.yml --env-file .env.production logs -f

# Specific service
docker compose -f docker/docker-compose.prod.yml --env-file .env.production logs -f api
docker compose -f docker/docker-compose.prod.yml --env-file .env.production logs -f pii-service
docker compose -f docker/docker-compose.prod.yml --env-file .env.production logs -f nginx
docker compose -f docker/docker-compose.prod.yml --env-file .env.production logs -f postgres

# Last 100 lines
docker compose -f docker/docker-compose.prod.yml --env-file .env.production logs --tail=100 api
```

### Database Backup

```bash
# Create backup
docker compose -f docker/docker-compose.prod.yml --env-file .env.production \
  exec postgres pg_dump -U piigateway piigateway | gzip > backup_$(date +%Y%m%d_%H%M%S).sql.gz

# Restore from backup
gunzip -c backup_YYYYMMDD_HHMMSS.sql.gz | \
  docker compose -f docker/docker-compose.prod.yml --env-file .env.production \
  exec -T postgres psql -U piigateway piigateway
```

### SSL Certificate Renewal

Certificates auto-renew via the certbot container. To manually renew:

```bash
docker compose -f docker/docker-compose.prod.yml --env-file .env.production \
  exec certbot certbot renew
docker compose -f docker/docker-compose.prod.yml --env-file .env.production \
  exec nginx nginx -s reload
```

---

## Architecture Overview

```
Internet
    │
    ▼
┌─────────┐
│  Nginx  │ :80 (→HTTPS redirect) / :443
│  + SSL  │
└────┬────┘
     │
     ├──── /api/*  ──→  ASP.NET Core API (:8080)
     │                        │
     │                        ├── PostgreSQL (:5432)
     │                        ├── Redis (:6379)
     │                        └── PII Service (:8001)
     │                              ├── Layer 1: Regex
     │                              └── Layer 2: NER (BERT)
     │
     └──── /*  ──→  Vue 3 SPA (static files)
```

All services run in Docker containers on a single server. Only ports 80 and 443 are exposed to the internet. PostgreSQL, Redis, and the PII service are on an internal Docker network.
