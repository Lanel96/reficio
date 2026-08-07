#!/bin/bash
set -e

GIT_HOST="github.com"
echo "========================================="
echo "  CONFIGURAR CREDENCIALES DE ACTUALIZACION"
echo "========================================="
echo
echo "Requisitos:"
echo "  - Crear un Personal Access Token (PAT) en:"
echo "    https://github.com/settings/tokens"
echo "    con scope: 'repo'"
echo
read -r -p "Usuario de GitHub (ej. Lanel96): " USUARIO
read -r -s -p "Token de acceso: " TOKEN
echo
echo
if [ -z "$USUARIO" ] || [ -z "$TOKEN" ]; then
  echo "[ERROR] Usuario o token vacíos."
  exit 1
fi

printf 'https://%s:%s@%s\n' "$USUARIO" "$TOKEN" "$GIT_HOST" > "$HOME/.git-credentials"
chmod 600 "$HOME/.git-credentials"

echo "[OK] Credenciales guardadas en ~/.git-credentials"
echo
echo "Pruebe la revisión de actualizaciones en el programa (botón de actualización)."
