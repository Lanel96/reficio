#!/bin/bash
set -e

echo "=== Reficio - Build Multiplataforma ==="
echo ""

DOTNET="/usr/local/share/dotnet/dotnet"
PUBLISH_DIR="publish"

rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

echo "[1/4] Compilando para Windows (win-x64)..."
$DOTNET publish Reficio.csproj -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$PUBLISH_DIR/windows-x64"

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
  cp "../../Resources/Reficio.icns" "$APP_DIR/Resources/Reficio.icns"

  # Crear Info.plist
  cat > "$APP_DIR/Info.plist" << 'PLIST'
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
    <string>1.3.8</string>
    <key>CFBundleShortVersionString</key>
    <string>1.3.8</string>
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

echo "Empaquetando ZIPs..."
cd "$PUBLISH_DIR"
zip -r Reficio-windows-x64.zip windows-x64/ -x "*.pdb"
zip -r Reficio-macos-x64.zip macos-x64/ -x "*.pdb"
zip -r Reficio-macos-arm64.zip macos-arm64/ -x "*.pdb"
cd ..

echo ""
echo "=== Build completado ==="
ls -lh "$PUBLISH_DIR"/*.zip
