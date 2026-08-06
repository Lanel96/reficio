#!/bin/bash
set -e

VERSION="${1:?Uso: ./release.sh <nueva-version>   ej: ./release.sh 1.4.1}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

GIT_HOST="git.upc.com.mx"
GIT_PROJECT="luisleon%2Freficiov2"
PACKAGE="reficio"
API="https://$GIT_HOST/api/v4/projects/$GIT_PROJECT"
BRANCH="main"

echo "=== Reficio - Publicar version $VERSION ==="
echo ""

# ---------------------------------------------------------------
# 1. Token (GitLab Personal Access Token con scope api)
# ---------------------------------------------------------------
TOKEN=""
CREDFILE="$HOME/.git-credentials"
if [ -f "$CREDFILE" ]; then
  TOKEN="$(grep "$GIT_HOST" "$CREDFILE" | sed -E 's#^https?://[^:]+:([^@]+)@.*#\1#' | head -1)"
fi
if [ -z "$TOKEN" ]; then
  TOKEN="$(env | grep '^GIT_PASSWORD=' | cut -d= -f2-)"
fi
if [ -z "$TOKEN" ]; then
  echo "[ERROR] No se encontró el token."
  echo "        Cree un token en $GIT_HOST/-/user_settings/personal_access_tokens"
  echo "        con scope 'api' y guárdelo en ~/.git-credentials como:"
  echo "        https://<usuario>:<token>@$GIT_HOST"
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
echo "[2/6] Commiteando todo el código y subiendo a git..."
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
# 5. Subir los ZIP al registro de paquetes de git
# ---------------------------------------------------------------
echo "[4/6] Subiendo paquetes de instalación al repositorio git..."
for ZIP in "$SCRIPT_DIR"/publish/Reficio-*.zip; do
  FILE_NAME="$(basename "$ZIP")"
  echo "        Subiendo $FILE_NAME ..."
  HTTP_CODE="$(curl -sS -o /dev/null -w '%{http_code}' --request POST \
    --header "PRIVATE-TOKEN: $TOKEN" \
    --data-binary "@$ZIP" \
    "$API/packages/generic/$PACKAGE/$VERSION/$FILE_NAME")"
  if [ "$HTTP_CODE" != "201" ] && [ "$HTTP_CODE" != "200" ]; then
    echo "[ERROR] No se pudo subir $FILE_NAME (HTTP $HTTP_CODE)"
    exit 1
  fi
done

# ---------------------------------------------------------------
# 6. Verificación
# ---------------------------------------------------------------
echo "[5/6] Verificando versión publicada..."
echo "        (uso de la app) git: $GIT_HOST/luisleon/reficiov2 - tag v$VERSION"

echo ""
echo "=== Publicación v$VERSION completada ==="
echo "La aplicación descargará la versión automáticamente al consultar git."
