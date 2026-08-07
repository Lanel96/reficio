#!/bin/bash
set -e

echo "=== Reficio - Build Multiplataforma ==="
echo ""

DOTNET="/usr/local/share/dotnet/dotnet"
PUBLISH_DIR="publish"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION="$(sed -nE 's#.*<Version>(.*)</Version>.*#\1#p' "$SCRIPT_DIR/Reficio.csproj" | head -1)"
[ -z "$VERSION" ] && VERSION="1.4.0"
echo "Versión a publicar: $VERSION"

rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

echo "[1/4] Compilando para Windows (win-x64)..."
$DOTNET publish Reficio.csproj -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$PUBLISH_DIR/windows-x64"
$DOTNET publish ReficioUpdater/ReficioUpdater.csproj -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$PUBLISH_DIR/windows-x64/ReficioUpdater" >/dev/null
mv "$PUBLISH_DIR/windows-x64/ReficioUpdater/ReficioUpdater.exe" "$PUBLISH_DIR/windows-x64/ReficioUpdater.exe"
rm -rf "$PUBLISH_DIR/windows-x64/ReficioUpdater"

echo "[2/4] Compilando para macOS Intel (osx-x64)..."
$DOTNET publish Reficio.csproj -c Release -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$PUBLISH_DIR/macos-x64"

echo "[3/4] Compilando para macOS Apple Silicon (osx-arm64)..."
$DOTNET publish Reficio.csproj -c Release -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$PUBLISH_DIR/macos-arm64"

echo "[4/4] Creando bundles .app para macOS..."

create_app_bundle() {
  local ARCH_DIR="$1"
  local APP_DIR="$ARCH_DIR/Reficio.app/Contents"

  mkdir -p "$APP_DIR/MacOS"
  mkdir -p "$APP_DIR/Resources"

  # Mover ejecutable a MacOS/
  mv "$ARCH_DIR/Reficio" "$APP_DIR/MacOS/Reficio"

  # Mover librerías nativas a MacOS/
  for lib in "$ARCH_DIR"/*.dylib; do
    [ -f "$lib" ] && mv "$lib" "$APP_DIR/MacOS/"
  done

  # Mover .pdb a Resources/ (opcional, para debug)
  for pdb in "$ARCH_DIR"/*.pdb; do
    [ -f "$pdb" ] && mv "$pdb" "$APP_DIR/Resources/"
  done

  # Copiar icono
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  cp "$SCRIPT_DIR/Resources/Reficio.icns" "$APP_DIR/Resources/Reficio.icns"

  # Crear Info.plist
  cat > "$APP_DIR/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key>
    <string>Reficio</string>
    <key>CFBundleExecutable</key>
    <string>Reficio</string>
    <key>CFBundleIdentifier</key>
    <string>com.reficio.app</string>
    <key>CFBundleName</key>
    <string>Reficio</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleIconFile</key>
    <string>Reficio.icns</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSSupportsAutomaticGraphicsSwitching</key>
    <true/>
</dict>
</plist>
PLIST
}

create_app_bundle "$PUBLISH_DIR/macos-x64"
create_app_bundle "$PUBLISH_DIR/macos-arm64"

# Compilar el actualizador para macOS y colocarlo en el bundle (Contents/MacOS/ReficioUpdater)
build_updater_macos() {
  local RID="$1"
  local APP_MACOS="$2"
  $DOTNET publish ReficioUpdater/ReficioUpdater.csproj -c Release -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o "$PUBLISH_DIR/updater_$RID" >/dev/null
  cp "$PUBLISH_DIR/updater_$RID/ReficioUpdater" "$APP_MACOS/ReficioUpdater"
  chmod +x "$APP_MACOS/ReficioUpdater"
  rm -rf "$PUBLISH_DIR/updater_$RID"
}

build_updater_macos osx-x64 "$PUBLISH_DIR/macos-x64/Reficio.app/Contents/MacOS"
build_updater_macos osx-arm64 "$PUBLISH_DIR/macos-arm64/Reficio.app/Contents/MacOS"

echo "Empaquetando ZIPs..."
cd "$PUBLISH_DIR"
zip -r Reficio-windows-x64.zip windows-x64/ -x "*.pdb"
zip -r Reficio-macos-x64.zip macos-x64/ -x "*.pdb"
zip -r Reficio-macos-arm64.zip macos-arm64/ -x "*.pdb"
cd ..

echo ""
echo "=== Build completado ==="
ls -lh "$PUBLISH_DIR"/*.zip
