#!/bin/bash
# One-time server setup for Hetzner (or any Ubuntu 22.04+ VPS)
# Usage: ssh root@your-server 'bash -s' < setup-server.sh

set -euo pipefail

echo "==> PII Gateway Server Setup"
echo "==> $(date)"

# 1. System updates
echo "==> Updating system packages..."
apt-get update -qq
apt-get upgrade -y -qq

# 2. Install Docker
echo "==> Installing Docker..."
if ! command -v docker &>/dev/null; then
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker
fi

# 3. Install Docker Compose (v2)
echo "==> Verifying Docker Compose..."
docker compose version

# 4. Firewall (UFW)
echo "==> Configuring firewall..."
apt-get install -y -qq ufw
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp   # SSH
ufw allow 80/tcp   # HTTP (for Let's Encrypt + redirect)
ufw allow 443/tcp  # HTTPS
ufw --force enable

# 5. Create application user
echo "==> Creating application user..."
if ! id -u piigateway &>/dev/null; then
    useradd -m -s /bin/bash piigateway
    usermod -aG docker piigateway
fi

# 6. Create application directories
echo "==> Creating application directories..."
mkdir -p /opt/piigateway
chown piigateway:piigateway /opt/piigateway

# 7. Swap (for small VPS instances)
echo "==> Configuring swap..."
if [ ! -f /swapfile ]; then
    fallocate -l 2G /swapfile
    chmod 600 /swapfile
    mkswap /swapfile
    swapon /swapfile
    echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

# 8. Fail2ban for SSH protection
echo "==> Installing fail2ban..."
apt-get install -y -qq fail2ban
systemctl enable fail2ban
systemctl start fail2ban

# 9. Automatic security updates
echo "==> Configuring unattended upgrades..."
apt-get install -y -qq unattended-upgrades
dpkg-reconfigure -f noninteractive unattended-upgrades

echo ""
echo "==> Server setup complete!"
echo "==> Next steps:"
echo "    1. Copy your deployment files to /opt/piigateway/"
echo "    2. Create /opt/piigateway/.env.production from .env.production.example"
echo "    3. Run: cd /opt/piigateway && ./deploy/deploy.sh"
