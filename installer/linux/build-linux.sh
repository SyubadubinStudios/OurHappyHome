#!/usr/bin/env bash
# Our Happy Home - builds the Linux package (works on Linux, macOS or Windows Git Bash):
#   artifacts/OurHappyHome-<version>-linux-x64.tar.gz   (extract, then ./install.sh)
#   artifacts/OurHappyHome-<version>-x86_64.AppImage     (when appimagetool is installed)
set -euo pipefail
VERSION="${1:-1.0.0}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
ART="$ROOT/artifacts"
PUB="$ART/publish/linux-x64"
STAGE="$ART/staging-linux/OurHappyHome"

echo "Publishing Our Happy Home $VERSION for linux-x64..."
rm -rf "$PUB" "$ART/staging-linux"
dotnet publish "$ROOT/src/OurHappyHome" -c Release -r linux-x64 --self-contained \
  -p:Version="$VERSION" -p:DebugType=None -o "$PUB"
find "$PUB" -name '*.pdb' -delete

mkdir -p "$STAGE"
cp -r "$PUB/." "$STAGE/"
cp "$HERE/install.sh" "$HERE/uninstall.sh" "$HERE/ourhappyhome.desktop" "$STAGE/"
cp "$ROOT/src/OurHappyHome/Assets/Art/icon.png" "$STAGE/ourhappyhome.png"
cp "$ROOT/README.md" "$STAGE/"
chmod +x "$STAGE/OurHappyHome" "$STAGE/install.sh" "$STAGE/uninstall.sh" 2>/dev/null || true

mkdir -p "$ART"
TAR="$ART/OurHappyHome-$VERSION-linux-x64.tar.gz"
# --mode keeps the executable bits even when the archive is built on Windows.
tar -C "$ART/staging-linux" --mode='u+rwx,go+rx' -czf "$TAR" OurHappyHome
echo "Linux package: $TAR"

if command -v appimagetool >/dev/null 2>&1; then
  APPDIR="$ART/staging-linux/OurHappyHome.AppDir"
  mkdir -p "$APPDIR/usr/lib/ourhappyhome"
  cp -r "$PUB/." "$APPDIR/usr/lib/ourhappyhome/"
  cp "$HERE/ourhappyhome.desktop" "$APPDIR/"
  sed -i 's|^Exec=.*|Exec=OurHappyHome|' "$APPDIR/ourhappyhome.desktop"
  cp "$ROOT/src/OurHappyHome/Assets/Art/icon.png" "$APPDIR/ourhappyhome.png"
  printf '#!/bin/sh\nHERE="$(dirname "$(readlink -f "$0")")"\nexec "$HERE/usr/lib/ourhappyhome/OurHappyHome" "$@"\n' > "$APPDIR/AppRun"
  chmod +x "$APPDIR/AppRun" "$APPDIR/usr/lib/ourhappyhome/OurHappyHome"
  ARCH=x86_64 appimagetool "$APPDIR" "$ART/OurHappyHome-$VERSION-x86_64.AppImage"
  echo "AppImage: $ART/OurHappyHome-$VERSION-x86_64.AppImage"
else
  echo "appimagetool not found: skipped AppImage (https://appimage.github.io/appimagetool/)"
fi

rm -rf "$ART/staging-linux"
