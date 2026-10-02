#!/usr/bin/env bash
# Our Happy Home - per-user installer for Linux (no root needed).
# Installs to ~/.local/share/OurHappyHome, adds a launcher command and a desktop entry.
set -euo pipefail
SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEST="${XDG_DATA_HOME:-$HOME/.local/share}/OurHappyHome"
BIN="$HOME/.local/bin"
APPS="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
ICONS="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/256x256/apps"

echo "Installing Our Happy Home to $DEST ..."
rm -rf "$DEST"
mkdir -p "$DEST" "$BIN" "$APPS" "$ICONS"
cp -r "$SRC/." "$DEST/"
rm -f "$DEST/install.sh"
chmod +x "$DEST/OurHappyHome" "$DEST/uninstall.sh"
ln -sf "$DEST/OurHappyHome" "$BIN/ourhappyhome"
cp "$SRC/ourhappyhome.png" "$ICONS/ourhappyhome.png"
sed "s|^Exec=.*|Exec=$DEST/OurHappyHome|; s|^Path=.*|Path=$DEST|" "$SRC/ourhappyhome.desktop" > "$APPS/ourhappyhome.desktop"
chmod +x "$APPS/ourhappyhome.desktop"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS" || true

echo "Done! Start 'Our Happy Home' from your application menu or run: ourhappyhome"
echo "Needs a GPU driver with Vulkan support."
