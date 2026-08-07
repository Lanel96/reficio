#!/bin/bash
set -e

VERSION="${1:?Uso: ./release.sh <nueva-version>   ej: ./release.sh 1.4.1}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

GH_OWNER="Lanel96"
GH_REPO="reficio"
GH_HOST="github.com"
API="https://api.github.com/repos/$GH_OWNER/$GH_REPO"
BRANCH="main"

echo "=== Reficio - Publicar version $VERSION ==="
echo ""

# ---------------------------------------------------------------
# 1. Token (GitHub Personal Access Token con scope repo)
# ---------------------------------------------------------------
TOKEN=""
CREDFILE="$HOME/.git-credentials"
if [ -f "$CREDFILE" ]; then
  TOKEN="$(grep "$GH_HOST" "$CREDFILE" | sed -E 's#^https?://[^:]+:([^@]+)@.*#\1#' | head -1)"
fi
if [ -z "$TOKEN" ]; then
  TOKEN="$(env | grep -E '^(GIT_PASSWORD|GH_TOKEN|GITHUB_TOKEN)=' | head -1 | cut -d= -f2-)"
fi
if [ -z "$TOKEN" ]; then
  echo "[ERROR] No se encontró el token."
  echo "        Cree un token en https://$GH_HOST/settings/tokens"
  echo "        con scope 'repo' y guárdelo en ~/.git-credentials como:"
  echo "        https://$GH_HOST/<usuario>:<token>@$GH_HOST"
  echo "        (o expórtelo como GITHUB_TOKEN)"
  exit 1
fi

# ---------------------------------------------------------------
# 2. Actualizar versión en el código
# ---------------------------------------------------------------
echo "[1/6] Actualizando versión a $VERSION..."
sed -i.bak -E "s#<Version>.*</Version>#<Version>$VERSION</Version>#" Reficio.csproj
rm -f Reficio.csproj.bak

# ---------------------------------------------------------------
# 3. Commit + tag + push de todo el código
# ---------------------------------------------------------------
echo "[2/6] Commiteando todo el código y subiendo a GitHub..."
git add -A
if ! git diff --cached --quiet; then
  git commit -m "release: v$VERSION"
fi
git push origin "$BRANCH" 2>/dev/null || echo "        (push principal pendiente, se reintentará con el tag)"
git tag -f "v$VERSION"
git push origin "v$VERSION"

# ---------------------------------------------------------------
# 4. Compilar los binarios multiplataforma
# ---------------------------------------------------------------
echo "[3/6] Compilando binarios..."
./publish.sh

# ---------------------------------------------------------------
# 5. Crear Release en GitHub (anexo de assets)
# ---------------------------------------------------------------
echo "[4/6] Creando Release v$VERSION en GitHub..."
DESC="Reficio v$VERSION. Descarga el instalador correspondiente a tu plataforma desde este release."
RELEASE_JSON="$(curl -sS -X POST \
  --header "Authorization: Bearer $TOKEN" \
  --header "Accept: application/vnd.github+json" \
  --data "{\"tag_name\":\"v$VERSION\",\"name\":\"v$VERSION\",\"body\":\"$DESC\",\"draft\":false,\"prerelease\":false}" \
  "$API/releases")"

RELEASE_ID="$(echo "$RELEASE_JSON" | sed -nE 's#.*"id": *([0-9]+).*#\1#p' | head -1)"
if [ -z "$RELEASE_ID" ]; then
  echo "ERROR: No se pudo crear la Release."
  echo "$RELEASE_JSON"
  exit 1
fi
echo "        Release creada (id $RELEASE_ID)."

# ---------------------------------------------------------------
# 6. Subir los ZIP como assets de la Release
# ---------------------------------------------------------------
echo "[5/6] Subiendo instaladores a la Release v$VERSION..."
for ZIP in "$SCRIPT_DIR"/publish/Reficio-*.zip; do
  FILE_NAME="$(basename "$ZIP")"
  echo "        Subiendo $FILE_NAME ..."
  HTTP_CODE="$(curl -sS -o /dev/null -w '%{http_code}' -X POST \
    --header "Authorization: Bearer $TOKEN" \
    --header "Accept: application/vnd.github+json" \
    --header "Content-Type: application/zip" \
    --data-binary "@$ZIP" \
    "$API/releases/$RELEASE_ID/assets?name=$FILE_NAME")"
  if [ "$HTTP_CODE" != "201" ]; then
    echo "ERROR: No se pudo subir $FILE_NAME (HTTP $HTTP_CODE)"
    exit 1
  fi
done

# ---------------------------------------------------------------
# 7. Verificación
# ---------------------------------------------------------------
echo "[6/6] Verificando versión publicada..."
echo "        GitHub: https://github.com/$GH_OWNER/$GH_REPO/releases/tag/v$VERSION"

echo ""
echo "=== Publicación v$VERSION completada ==="
echo "La aplicación validará la versión y descargará el instalador desde GitHub Releases."
echo "IMPORTANTE: Guarde el token GITHUB_TOKEN como 'secret' del repositorio para que las CI de GitHub Actions funcionen."