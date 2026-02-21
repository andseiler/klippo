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

# 5a. Grant sudo and copy SSH keys to piigateway
echo "==> Granting sudo to piigateway and copying SSH keys..."
usermod -aG sudo piigateway

echo 'piigateway ALL=(ALL) NOPASSWD:ALL' > /etc/sudoers.d/piigateway
chmod 440 /etc/sudoers.d/piigateway

mkdir -p /home/piigateway/.ssh
if [ -f /root/.ssh/authorized_keys ]; then
    cp /root/.ssh/authorized_keys /home/piigateway/.ssh/authorized_keys
    chown -R piigateway:piigateway /home/piigateway/.ssh
    chmod 700 /home/piigateway/.ssh
    chmod 600 /home/piigateway/.ssh/authorized_keys
fi

# 5b. SSH hardening
echo "==> Hardening SSH..."
# Safety check: only proceed if piigateway has authorized_keys (prevents lockout)
if [ -f /home/piigateway/.ssh/authorized_keys ] && [ -s /home/piigateway/.ssh/authorized_keys ]; then
    sed -i 's/^#\?Port .*/Port 22222/' /etc/ssh/sshd_config
    sed -i 's/^#\?PermitRootLogin .*/PermitRootLogin no/' /etc/ssh/sshd_config
    sed -i 's/^#\?PasswordAuthentication .*/PasswordAuthentication no/' /etc/ssh/sshd_config

    # Ensure the settings exist if they weren't in the file at all
    grep -q '^Port ' /etc/ssh/sshd_config || echo 'Port 22222' >> /etc/ssh/sshd_config
    grep -q '^PermitRootLogin ' /etc/ssh/sshd_config || echo 'PermitRootLogin no' >> /etc/ssh/sshd_config
    grep -q '^PasswordAuthentication ' /etc/ssh/sshd_config || echo 'PasswordAuthentication no' >> /etc/ssh/sshd_config

    # Ubuntu 22.04+ uses ssh.socket which overrides sshd_config port.
    # Disable socket activation so ssh.service reads the port from sshd_config.
    systemctl disable ssh.socket
    systemctl stop ssh.socket
    systemctl restart ssh.service

    # Update UFW: remove old SSH port, allow new one
    ufw delete allow 22/tcp || true
    ufw allow 22222/tcp comment 'SSH (hardened port)'

    echo "==> SSH hardened: port 22222, root login disabled, password auth disabled"
else
    echo "==> WARNING: Skipping SSH hardening — piigateway has no authorized_keys."
    echo "    Copy your SSH public key to /home/piigateway/.ssh/authorized_keys first."
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
echo ""
echo "==> IMPORTANT: SSH has been hardened. Your next connection must use:"
echo "    ssh -p 22222 piigateway@YOUR_SERVER_IP"
echo ""
echo "==> Next steps:"
echo "    1. Reconnect: ssh -p 22222 piigateway@YOUR_SERVER_IP"
echo "    2. Copy your deployment files to /opt/piigateway/"
echo "    3. Create /opt/piigateway/.env.production from .env.production.example"
echo "    4. Run: cd /opt/piigateway && ./deploy/deploy.sh"
