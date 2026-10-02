#!/usr/bin/env bash
# Our Happy Home - removes the per-user Linux installation.
# Saves (~/.config/OurHappyHome) are kept unless --remove-saves is passed.
set -euo pipefail
DATA="${XDG_DATA_HOME:-$HOME/.local/share}"
rm -rf "$DATA/OurHappyHome"
rm -f "$HOME/.local/bin/ourhappyhome" "$DATA/applications/ourhappyhome.desktop" "$DATA/icons/hicolor/256x256/apps/ourhappyhome.png"
if [ "${1:-}" = "--remove-saves" ]; then
  rm -rf "${XDG_CONFIG_HOME:-$HOME/.config}/OurHappyHome"
fi
echo "Our Happy Home was uninstalled."
