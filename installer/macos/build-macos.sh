#!/usr/bin/env bash
# Our Happy Home - builds macOS app bundles (Apple Silicon and Intel):
#   artifacts/OurHappyHome-<version>-macos-<arch>.zip      (on macOS; .tar.gz when built elsewhere)
#   artifacts/OurHappyHome-<version>-macos-<arch>.dmg   (when run on macOS: hdiutil)
# The .app can be built on any OS; icons (.icns), ad-hoc signing and DMG need macOS.
set -euo pipefail
VERSION="${1:-1.1.0}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
ART="$ROOT/artifacts"
mkdir -p "$ART"

for RID in osx-arm64 osx-x64; do
  ARCH="${RID#osx-}"
  PUB="$ART/publish/$RID"
  STAGE="$ART/staging-$RID"
  APP="$STAGE/Our Happy Home.app"
  echo "Publishing Our Happy Home $VERSION for $RID..."
  rm -rf "$PUB" "$STAGE"
  dotnet publish "$ROOT/src/OurHappyHome" -c Release -r "$RID" --self-contained \
    -p:Version="$VERSION" -p:DebugType=None -o "$PUB"
  find "$PUB" -name '*.pdb' -delete

  mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
  cp -r "$PUB/." "$APP/Contents/MacOS/"
  sed "s/__VERSION__/$VERSION/g" "$HERE/Info.plist" > "$APP/Contents/Info.plist"
  chmod +x "$APP/Contents/MacOS/OurHappyHome" 2>/dev/null || true

  ICON_PNG="$ROOT/src/OurHappyHome/Assets/Art/icon.png"
  if command -v iconutil >/dev/null 2>&1 && command -v sips >/dev/null 2>&1; then
    SET="$STAGE/icon.iconset"
    mkdir -p "$SET"
    for SIZE in 16 32 64 128 256 512; do
      sips -z $SIZE $SIZE "$ICON_PNG" --out "$SET/icon_${SIZE}x${SIZE}.png" >/dev/null
      sips -z $((SIZE * 2)) $((SIZE * 2)) "$ICON_PNG" --out "$SET/icon_${SIZE}x${SIZE}@2x.png" >/dev/null
    done
    iconutil -c icns "$SET" -o "$APP/Contents/Resources/OurHappyHome.icns"
    rm -rf "$SET"
  else
    cp "$ICON_PNG" "$APP/Contents/Resources/OurHappyHome.png"
  fi

  if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "$APP" || true   # ad-hoc signature for local runs
  fi

  if command -v ditto >/dev/null 2>&1; then
    ZIP="$ART/OurHappyHome-$VERSION-macos-$ARCH.zip"
    rm -f "$ZIP"
    ditto -c -k --keepParent "$APP" "$ZIP"
    echo "macOS package: $ZIP"
  else
    # Built off a Mac: a tarball keeps the executable bit of the app binary.
    TGZ="$ART/OurHappyHome-$VERSION-macos-$ARCH.tar.gz"
    tar -C "$STAGE" --mode='u+rwx,go+rx' -czf "$TGZ" "Our Happy Home.app"
    echo "macOS package: $TGZ"
  fi

  if command -v hdiutil >/dev/null 2>&1; then
    ln -s /Applications "$STAGE/Applications"
    hdiutil create -volname "Our Happy Home" -srcfolder "$STAGE" -ov -format UDZO "$ART/OurHappyHome-$VERSION-macos-$ARCH.dmg"
    echo "DMG: $ART/OurHappyHome-$VERSION-macos-$ARCH.dmg"
  fi

  rm -rf "$STAGE"
done
