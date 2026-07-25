@echo off
cd /d "%~dp0"
title Instalador ApiPorteroSpa - PC del spa
echo.
echo ========================================
echo   Instalador ApiPorteroSpa
echo   Carpeta: %cd%
echo ========================================
echo.
echo Se va a instalar Python (si falta), dependencias,
echo la base de datos y un icono en el Escritorio.
echo.
pause

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar.ps1"
set ERR=%ERRORLEVEL%
echo.
if %ERR% neq 0 (
  echo La instalacion fallo con codigo %ERR%.
  pause
  exit /b %ERR%
)

echo.
echo Listo. En el Escritorio deberia aparecer: ApiPorteroSpa
echo.
choice /C SN /M "Abrir el panel ahora"
if errorlevel 2 goto end
if errorlevel 1 call "%~dp0Iniciar_Panel.bat"
:end
pause
