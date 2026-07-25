@echo off
cd /d "%~dp0"
title ApiPorteroSpa - Panel
if exist "%~dp0.venv\Scripts\python.exe" (
  "%~dp0.venv\Scripts\python.exe" "%~dp0desktop_app.py"
) else (
  python "%~dp0desktop_app.py"
)
if errorlevel 1 (
  echo.
  echo Fallo al abrir el panel. Ejecuta Instalar.bat primero.
  pause
)
