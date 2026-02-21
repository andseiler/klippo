# PII Gateway — Pre-Deployment Security Checklist

Complete every item before going live. Each item includes a verification command or procedure.

---

## 1. Secrets & Environment

- [ ] **`.env.production` exists and is NOT committed to git**
  ```bash
  # Verify it's ignored
  git check-ignore .env.production
  # Should print: .env.production
  ```

- [ ] **All placeholder values replaced** — no `CHANGE_ME` strings remain
  ```bash
  grep -c 'CHANGE_ME' .env.production
  # Should print: 0
  ```

- [ ] **POSTGRES_PASSWORD is randomly generated (32+ chars)**
  ```bash
  # Generate with:
  openssl rand -base64 32
  ```

- [ ] **REDIS_PASSWORD is randomly generated (32+ chars)**
  ```bash
  openssl rand -base64 32
  ```

- [ ] **JWT_SECRET is randomly generated (64+ chars)**
  ```bash
  openssl rand -base64 64
  ```

- [ ] **ENCRYPTION_KEY is a valid AES-256 key (32 bytes, base64-encoded)**
  ```bash
  openssl rand -base64 32
  ```

- [ ] **LLM_BACKEND is set to `disabled`** (unless you explicitly want LLM)
  ```bash
  grep '^LLM_BACKEND=' .env.production
  # Should print: LLM_BACKEND=disabled
  ```

- [ ] **DOMAIN is set to your actual domain** (not `example.com`)
  ```bash
  grep '^DOMAIN=' .env.production
  ```

---

## 2. SSL / TLS

- [ ] **Let's Encrypt certificate is installed**
  ```bash
  docker compose -f docker/docker-compose.prod.yml --env-file .env.production \
    exec certbot certbot certificates
  ```

- [ ] **SSL Labs test returns A or A+**
  ```
  https://www.ssllabs.com/ssltest/analyze.html?d=YOUR_DOMAIN
  ```

- [ ] **HTTP redirects to HTTPS**
  ```bash
  curl -I http://YOUR_DOMAIN
  # Should return 301 → https://YOUR_DOMAIN/
  ```

- [ ] **HSTS header is present**
  ```bash
  curl -sI https://YOUR_DOMAIN | grep -i strict-transport
  # Should include: max-age=63072000
  ```

---

## 3. Security Headers

- [ ] **All security headers are present**
  ```bash
  curl -sI https://YOUR_DOMAIN | grep -iE '(x-content-type|x-frame|referrer-policy|permissions-policy|strict-transport)'
  ```
  Expected:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`
  - `Permissions-Policy: camera=(), microphone=(), geolocation=()`
  - `Strict-Transport-Security: max-age=63072000; includeSubDomains`

---

## 4. API Security

- [ ] **Health endpoint hides details from unauthenticated users**
  ```bash
  curl -s https://YOUR_DOMAIN/api/v1/health | python3 -m json.tool
  # Should return only: {"status": "healthy"} (no components, no version)
  ```

- [ ] **CORS is restricted to your domain only**
  ```bash
  curl -sI -H "Origin: https://evil.com" https://YOUR_DOMAIN/api/v1/health \
    | grep -i access-control
  # Should NOT contain access-control-allow-origin: https://evil.com
  ```

- [ ] **Rate limiting is active**
  ```bash
  # Send 110 requests rapidly — last ones should get 429
  for i in $(seq 1 110); do
    curl -s -o /dev/null -w "%{http_code} " https://YOUR_DOMAIN/api/v1/health
  done
  ```

- [ ] **File upload rejects disallowed extensions**
  ```bash
  # Should return 400
  echo "test" > /tmp/test.exe
  curl -s -X POST https://YOUR_DOMAIN/api/v1/jobs \
    -H "Authorization: Bearer YOUR_TOKEN" \
    -F "file=@/tmp/test.exe" | python3 -m json.tool
  ```

- [ ] **Authentication is required for all job endpoints**
  ```bash
  curl -s -w "%{http_code}" https://YOUR_DOMAIN/api/v1/jobs
  # Should return 401
  ```

---

## 5. Infrastructure

- [ ] **Firewall allows only ports 22222, 80, 443**
  ```bash
  sudo ufw status
  # Should show 22222/tcp, 80/tcp, 443/tcp ALLOW — no port 22
  ```

- [ ] **Fail2ban is running**
  ```bash
  sudo systemctl status fail2ban
  ```

- [ ] **Unattended security updates are enabled**
  ```bash
  systemctl status unattended-upgrades
  ```

- [ ] **Swap is configured** (important for 4GB VPS with NER model)
  ```bash
  free -h | grep Swap
  # Should show 2G swap
  ```

- [ ] **Docker container memory limits are enforced**
  ```bash
  docker stats --no-stream --format "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}"
  ```

- [ ] **Docker logs are rotated** (json-file driver with max-size)
  ```bash
  docker inspect --format='{{.HostConfig.LogConfig}}' $(docker ps -q) | head -5
  # Should show max-size: 10m
  ```

---

## 6. Database

- [ ] **PostgreSQL is not exposed to the internet** (only on Docker internal network)
  ```bash
  # From outside the server:
  nc -zv YOUR_SERVER_IP 5432
  # Should fail / connection refused
  ```

- [ ] **Redis is not exposed to the internet**
  ```bash
  nc -zv YOUR_SERVER_IP 6379
  # Should fail / connection refused
  ```

- [ ] **Redis requires authentication**
  ```bash
  docker compose -f docker/docker-compose.prod.yml --env-file .env.production \
    exec redis redis-cli ping
  # Should return NOAUTH error (password required)
  ```

---

## 7. Backups

- [ ] **Database backup procedure is tested**
  ```bash
  # Create a backup
  docker compose -f docker/docker-compose.prod.yml --env-file .env.production \
    exec postgres pg_dump -U piigateway piigateway | gzip > backup_$(date +%Y%m%d).sql.gz
  ```

- [ ] **Backup is stored off-server** (Hetzner Storage Box, S3, etc.)

---

## 8. SSH Hardening

- [ ] **SSH port is 22222** (not default 22)
  ```bash
  sudo grep '^Port ' /etc/ssh/sshd_config
  # Should print: Port 22222
  ```

- [ ] **Root login is disabled**
  ```bash
  sudo grep '^PermitRootLogin ' /etc/ssh/sshd_config
  # Should print: PermitRootLogin no
  ```

- [ ] **Password authentication is disabled**
  ```bash
  sudo grep '^PasswordAuthentication ' /etc/ssh/sshd_config
  # Should print: PasswordAuthentication no
  ```

- [ ] **Root SSH is actually rejected**
  ```bash
  # From your local machine:
  ssh root@YOUR_SERVER_IP -p 22222
  # Should be rejected
  ```

- [ ] **piigateway user can connect and has sudo**
  ```bash
  ssh -p 22222 piigateway@YOUR_SERVER_IP 'sudo whoami'
  # Should print: root
  ```

---

## 9. Docker Container Hardening

- [ ] **All containers have no-new-privileges set**
  ```bash
  docker inspect --format='{{.Name}} {{.HostConfig.SecurityOpt}}' $(docker ps -q)
  # Every container should show [no-new-privileges:true]
  ```

- [ ] **API and PII service containers have read-only filesystem**
  ```bash
  docker inspect --format='{{.Name}} ReadOnly={{.HostConfig.ReadonlyRootfs}}' $(docker ps -q)
  # api and pii-service should show ReadOnly=true
  ```

---

## Additional Recommendations (Non-Blocking)

These are not required for launch but improve security posture over time:

1. **Set up monitoring** — use Uptime Robot, Hetrixtools, or similar to monitor
   `https://YOUR_DOMAIN/api/v1/health` and alert on non-200 responses.

2. **Log aggregation** — consider shipping Docker logs to a central service for analysis.

3. **Periodic dependency updates** — check for .NET, Python, and npm vulnerabilities monthly.
