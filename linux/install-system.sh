#!/bin/sh
# Installs the system-level pieces hakchi2 CE needs on Linux:
#   - the udev rule (USB access in FEL mode, fixed "hakchi0" interface name)
#   - the NetworkManager profile for the console's USB network link
# Run as root:  sudo ./install-system.sh
set -e
DIR="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
if [ "$(id -u)" -ne 0 ]; then
  echo "Run this script as root: sudo $0" >&2
  exit 1
fi

install -m 0644 "$DIR/99-hakchi.rules" /etc/udev/rules.d/99-hakchi.rules
udevadm control --reload-rules
udevadm trigger
echo "Installed /etc/udev/rules.d/99-hakchi.rules"

if [ -d /etc/NetworkManager/system-connections ]; then
  install -m 0600 -o root -g root "$DIR/hakchi-usb.nmconnection" \
    /etc/NetworkManager/system-connections/hakchi-usb.nmconnection
  if command -v nmcli >/dev/null 2>&1; then
    nmcli connection reload || true
  fi
  echo "Installed /etc/NetworkManager/system-connections/hakchi-usb.nmconnection"
else
  echo "NetworkManager not found. When the console is connected, give the"
  echo "hakchi0 interface a link-local address yourself, for example:"
  echo "  ip addr add 169.254.13.38/16 dev hakchi0 && ip link set hakchi0 up"
fi

echo "Done. Unplug and replug the console if it is already connected."
