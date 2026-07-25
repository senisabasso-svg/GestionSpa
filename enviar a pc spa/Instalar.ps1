#Requires -Version 5.1
<#
.SYNOPSIS
  Instala ApiPorteroSpa en esta PC: Python (si falta), venv, dependencias y base SQLite.
#>
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

function Write-Step($msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Find-SystemPython {
    foreach ($c in @("py", "python", "python3")) {
        try {
            $ver = & $c -c "import sys; print(sys.version_info[0])" 2>$null
            if ("$ver" -eq "3") { return $c }
        } catch {}
    }
    return $null
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Instalador ApiPorteroSpa" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "Carpeta: $Root"

# 1) Python del sistema
Write-Step "Buscando Python 3..."
$python = Find-SystemPython
if (-not $python) {
    Write-Host "Python no encontrado. Intentando instalar con winget..." -ForegroundColor Yellow
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        Write-Host "ERROR: No hay Python ni winget." -ForegroundColor Red
        Write-Host "Instalá Python 3.11+ desde https://www.python.org/downloads/" -ForegroundColor Red
        Write-Host "Marcá 'Add python.exe to PATH' y volvé a ejecutar Instalar.bat." -ForegroundColor Red
        exit 1
    }
    winget install -e --id Python.Python.3.12 --accept-package-agreements --accept-source-agreements
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path", "User")
    $python = Find-SystemPython
    if (-not $python) {
        Write-Host "ERROR: Python se instaló pero no está en PATH. Cerrá esta ventana, abrí otra y ejecutá Instalar.bat de nuevo." -ForegroundColor Red
        exit 1
    }
}

$pyVer = & $python -c "import sys; print('%d.%d.%d'%sys.version_info[:3])"
Write-Host "Python OK: $python ($pyVer)" -ForegroundColor Green

# 2) venv
Write-Step "Creando entorno virtual (.venv)..."
$venvPy = Join-Path $Root ".venv\Scripts\python.exe"
if (-not (Test-Path $venvPy)) {
    & $python -m venv (Join-Path $Root ".venv")
}
if (-not (Test-Path $venvPy)) {
    Write-Host "ERROR: no se pudo crear .venv" -ForegroundColor Red
    exit 1
}
Write-Host "venv OK: $venvPy" -ForegroundColor Green

# 3) pip + requirements
Write-Step "Descargando e instalando dependencias (Flask, Waitress, requests...)..."
& $venvPy -m pip install --upgrade pip
& $venvPy -m pip install -r (Join-Path $Root "requirements.txt")
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: falló pip install" -ForegroundColor Red
    exit 1
}
Write-Host "Dependencias OK" -ForegroundColor Green

# 4) Base de datos + carpetas
Write-Step "Inicializando base de datos SQLite (si no existe)..."
& $venvPy (Join-Path $Root "init_db.py")
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: falló init_db.py" -ForegroundColor Red
    exit 1
}

# 5) .env plantilla si no hay
$envFile = Join-Path $Root ".env"
if (-not (Test-Path $envFile)) {
    Write-Step "Creando .env inicial..."
    @"
# Generado por el instalador ApiPorteroSpa
PORTERO_API_KEY=portero-dev-key-change-me
PORTERO_GESTION_BASE_URL=
PORTERO_EMISOR_SLUG=
PORTERO_POLL_SECONDS=10
PORTERO_WEBHOOK_URL=
PORTERO_WEBHOOK_SECRET=
"@ | Set-Content -Path $envFile -Encoding UTF8
    Write-Host ".env creado (podés editarlo desde el panel)" -ForegroundColor Green
}

# 6) Launcher que usa venv
Write-Step "Actualizando Iniciar_Panel.bat..."
@"
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
"@ | Set-Content -Path (Join-Path $Root "Iniciar_Panel.bat") -Encoding ASCII

# 7) Icono .ico desde PNG (si se puede)
Write-Step "Preparando icono del acceso directo..."
$pngPath = Join-Path $Root "portero-icon.png"
$icoPath = Join-Path $Root "portero-icon.ico"
if ((Test-Path $pngPath) -and -not (Test-Path $icoPath)) {
    try {
        Add-Type -AssemblyName System.Drawing
        $bmp = New-Object System.Drawing.Bitmap $pngPath
        $iconHandle = $bmp.GetHicon()
        $icon = [System.Drawing.Icon]::FromHandle($iconHandle)
        $fs = [System.IO.File]::Create($icoPath)
        $icon.Save($fs)
        $fs.Close()
        $bmp.Dispose()
        Write-Host "Icono OK: portero-icon.ico" -ForegroundColor Green
    } catch {
        Write-Host "No se pudo crear .ico (se usara icono por defecto): $_" -ForegroundColor Yellow
    }
}

# 8) Acceso directo en el Escritorio
Write-Step "Creando acceso directo en el Escritorio..."
$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "ApiPorteroSpa.lnk"
$target = Join-Path $Root "Iniciar_Panel.bat"
try {
    $wsh = New-Object -ComObject WScript.Shell
    $sc = $wsh.CreateShortcut($shortcutPath)
    $sc.TargetPath = $target
    $sc.WorkingDirectory = $Root
    $sc.WindowStyle = 1
    $sc.Description = "Panel ApiPorteroSpa - control de acceso del spa"
    if (Test-Path $icoPath) {
        $sc.IconLocation = "$icoPath,0"
    } elseif (Test-Path $venvPy) {
        $sc.IconLocation = "$venvPy,0"
    }
    $sc.Save()
    Write-Host "Acceso directo OK: $shortcutPath" -ForegroundColor Green
} catch {
    Write-Host "No se pudo crear el acceso directo: $_" -ForegroundColor Yellow
    Write-Host "Igual podes abrir Iniciar_Panel.bat manualmente." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Instalacion completada" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "En el Escritorio tenes el icono: ApiPorteroSpa"
Write-Host "1) Abrilo (o Iniciar_Panel.bat)"
Write-Host "2) Completa API Key / Webhook en el panel"
Write-Host "3) Pulsa Iniciar servicio"
Write-Host "4) Configura el ZKTeco con la IP y puerto 8081 del panel"
Write-Host ""
