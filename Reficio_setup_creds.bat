@echo off
title Configurar Credenciales Reficio
color 0A
cls
echo =========================================
echo   CONFIGURAR CREDENCIALES DE ACTUALIZACION
echo =========================================
echo.
echo Este script configurara las credenciales para
echo acceder al repositorio privado de GitHub.
echo.
echo Requisitos:
echo   - Token de acceso personal (PAT) de GitHub
echo     Crear en: https://github.com/settings/tokens  con scope "repo"
echo.
echo =========================================
echo.
set /p USUARIO="Ingrese su usuario de GitHub: "
set /p TOKEN="Ingrese su token de acceso: "
echo.
echo.
echo Creando archivo de credenciales...
echo.
set CREDFILE=%USERPROFILE%\.git-credentials
echo https://%USUARIO%:%TOKEN%@github.com > "%CREDFILE%"
echo.
if %ERRORLEVEL% EQU 0 (
    echo [OK] Archivo creado: %CREDFILE%
    echo.
    echo Ahora puede usar "Buscar Actualizacion" en el programa.
) else (
    echo [ERROR] No se pudo crear el archivo
    echo.
    echo Intente manualmente:
    echo   Crear archivo: %CREDFILE%
    echo   Contenido: https://%USUARIO%:%TOKEN%@github.com
)
echo.
echo =========================================
echo.
echo Presione cualquier tecla para salir...
pause >nul
