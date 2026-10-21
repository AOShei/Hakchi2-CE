#!/bin/sh
# Builds hakchi2 CE on Linux with Mono's MSBuild.
# Requirements: mono, mono-msbuild (Arch: 'sudo pacman -S mono mono-msbuild';
# Debian/Ubuntu: 'sudo apt install mono-complete msbuild'), git, wget, tar.
set -e
ROOT="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
CONFIG="${1:-Release}"
cd "$ROOT"
# Mono maps the assemblies from disk; overwriting them under a running instance
# corrupts it ("Corrupt .resources file", metadata assertions). Refuse to do that.
if pgrep -x mono -a 2>/dev/null | grep -Fq "hakchi_gui/bin/$CONFIG/net48/hakchi.exe"; then
  echo "hakchi is running from hakchi_gui/bin/$CONFIG/net48. Quit it before building." >&2
  exit 1
fi
git submodule update --init --recursive
msbuild /restore /nologo /v:minimal "/p:Configuration=$CONFIG" "/p:SolutionDir=$ROOT/" hakchi_gui/hakchi_gui.csproj
OUT="$ROOT/hakchi_gui/bin/$CONFIG/net48"
cp "$ROOT/linux/hakchi.sh" "$OUT/hakchi.sh"
cp "$ROOT/linux/99-hakchi.rules" "$OUT/99-hakchi.rules"
cp "$ROOT/linux/hakchi-usb.nmconnection" "$OUT/hakchi-usb.nmconnection"
cp "$ROOT/linux/install-system.sh" "$OUT/install-system.sh"
chmod +x "$OUT/install-system.sh"
chmod +x "$OUT/hakchi.sh"
echo
echo "Build finished. Run it with:  $OUT/hakchi.sh"
