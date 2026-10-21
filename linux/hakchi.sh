#!/bin/sh
# Launcher for hakchi2 CE on Linux. Place next to hakchi.exe or leave in the
# release folder; it runs the exe from its own directory under Mono.
set -e
DIR="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
if ! command -v mono >/dev/null 2>&1; then
  echo "hakchi2 CE needs the Mono runtime. Install it with your package manager (e.g. 'sudo pacman -S mono' or 'sudo apt install mono-complete')." >&2
  exit 1
fi
# Mono's WinForms is X11-based; under Wayland it runs through XWayland.
export GDK_BACKEND=x11
cd "$DIR"
exec mono "$DIR/hakchi.exe" "$@"
