#Requires -Version 5.1
# Instalador ApiPorteroSpa - ASCII-safe for Windows PowerShell 5.1
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root
$MinMajor = 3
$MinMinor = 10

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host ("==> " + $msg) -ForegroundColor Cyan
}

function Get-PythonVersionTuple([string]$exe) {
    try {
        $raw = & $exe -c "import sys; print('%d.%d.%d' % sys.version_info[:3])" 2>$null
        if (-not $raw) { return $null }
        $parts = ($raw.ToString().Trim() -split '\.')
        if ($parts.Count -lt 2) { return $null }
        return @{
            Major = [int]$parts[0]
            Minor = [int]$parts[1]
            Patch = if ($parts.Count -gt 2) { [int]$parts[2] } else { 0 }
            Text  = $raw.ToString().Trim()
            Exe   = $exe
        }
    } catch {
        return $null
    }
}

function Test-PythonOk($ver) {
    if (-not $ver) { return $false }
    if ($ver.Major -gt $MinMajor) { return $true }
    if ($ver.Major -eq $MinMajor -and $ver.Minor -ge $MinMinor) { return $true }
    return $false
}

function Find-GoodPython {
    $candidates = New-Object System.Collections.Generic.List[string]

    $pyLauncher = Get-Command py -ErrorAction SilentlyContinue
    if ($pyLauncher) {
        foreach ($tag in @("-3.12", "-3.11", "-3.10", "-3")) {
            try {
                $exe = & py $tag -c "import sys; print(sys.executable)" 2>$null
                if ($exe) { [void]$candidates.Add($exe.ToString().Trim()) }
            } catch {}
        }
    }

    foreach ($c in @("python", "python3")) {
        $cmd = Get-Command $c -ErrorAction SilentlyContinue
        if ($cmd) { [void]$candidates.Add($cmd.Source) }
    }

    $pf = ${env:ProgramFiles}
    $pf86 = ${env:ProgramFiles(x86)}
    $local = $env:LOCALAPPDATA
    foreach ($base in @($pf, $pf86, (Join-Path $local "Programs\Python"))) {
        if (-not $base -or -not (Test-Path $base)) { continue }
        Get-ChildItem -Path $base -Filter "python.exe" -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 12 |
            ForEach-Object { [void]$candidates.Add($_.FullName) }
    }

    $seen = @{}
    foreach ($exe in $candidates) {
        if (-not $exe -or $seen.ContainsKey($exe)) { continue }
        $seen[$exe] = $true
        $ver = Get-PythonVersionTuple $exe
        if (Test-PythonOk $ver) { return $ver }
    }
    return $null
}

function Install-Python312 {
    Write-Host ("Se necesita Python " + $MinMajor + "." + $MinMinor + "+. Flask 3 no corre en 3.7.") -ForegroundColor Yellow
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        Write-Host "ERROR: no hay winget para instalar Python nuevo." -ForegroundColor Red
        Write-Host "Instala Python 3.12 desde https://www.python.org/downloads/" -ForegroundColor Red
        Write-Host "Marca Add python.exe to PATH, cierra esta ventana y vuelve a ejecutar Instalar.bat." -ForegroundColor Red
        exit 1
    }
    Write-Host "Instalando Python 3.12 con winget..." -ForegroundColor Yellow
    winget install -e --id Python.Python.3.12 --accept-package-agreements --accept-source-agreements
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path", "User")
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Instalador ApiPorteroSpa" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ("Carpeta: " + $Root)
Write-Host ("Requisito: Python >= " + $MinMajor + "." + $MinMinor)

Write-Step ("Buscando Python " + $MinMajor + "." + $MinMinor + "+ ...")
$pyInfo = Find-GoodPython
if (-not $pyInfo) {
    $old = Get-PythonVersionTuple "python"
    if ($old) {
        Write-Host ("Encontrado Python " + $old.Text + " - demasiado viejo.") -ForegroundColor Yellow
    }
    Install-Python312
    $pyInfo = Find-GoodPython
    if (-not $pyInfo) {
        Write-Host "ERROR: Python nuevo no aparece en PATH." -ForegroundColor Red
        Write-Host "Cierra esta ventana, abre otra y ejecuta Instalar.bat de nuevo." -ForegroundColor Red
        Write-Host "O instala a mano Python 3.12 desde python.org y marca Add to PATH." -ForegroundColor Red
        exit 1
    }
}

$python = $pyInfo.Exe
Write-Host ("Python OK: " + $python + " (" + $pyInfo.Text + ")") -ForegroundColor Green

Write-Step "Creando entorno virtual .venv ..."
$venvDir = Join-Path $Root ".venv"
$venvPy = Join-Path $venvDir "Scripts\python.exe"
$needRecreate = $true
if (Test-Path $venvPy) {
    $venvVer = Get-PythonVersionTuple $venvPy
    if (Test-PythonOk $venvVer) {
        $needRecreate = $false
        Write-Host ("venv existente OK (" + $venvVer.Text + ")") -ForegroundColor Green
    } else {
        Write-Host "venv hecho con Python viejo - se recrea..." -ForegroundColor Yellow
        Remove-Item -Recurse -Force $venvDir
    }
}
if ($needRecreate) {
    & $python -m venv $venvDir
}
if (-not (Test-Path $venvPy)) {
    Write-Host "ERROR: no se pudo crear .venv" -ForegroundColor Red
    exit 1
}
Write-Host ("venv OK: " + $venvPy) -ForegroundColor Green

Write-Step "Descargando e instalando dependencias..."
& $venvPy -m pip install --upgrade pip
& $venvPy -m pip install -r (Join-Path $Root "requirements.txt")
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: fallo pip install" -ForegroundColor Red
    exit 1
}
Write-Host "Dependencias OK" -ForegroundColor Green

Write-Step "Inicializando base de datos SQLite..."
& $venvPy (Join-Path $Root "init_db.py")
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: fallo init_db.py" -ForegroundColor Red
    exit 1
}

$envFile = Join-Path $Root ".env"
if (-not (Test-Path $envFile)) {
    Write-Step "Creando .env inicial..."
    @(
        "# Generado por el instalador ApiPorteroSpa"
        "PORTERO_API_KEY=portero-dev-key-change-me"
        "PORTERO_GESTION_BASE_URL="
        "PORTERO_EMISOR_SLUG="
        "PORTERO_POLL_SECONDS=10"
        "PORTERO_TCP_PORT=8081"
        "PORTERO_WEBHOOK_URL="
        "PORTERO_WEBHOOK_SECRET="
        ""
    ) | Set-Content -Path $envFile -Encoding ASCII
    Write-Host ".env creado - podes editarlo desde el panel" -ForegroundColor Green
}

Write-Step "Actualizando Iniciar_Panel.bat..."
@(
    "@echo off"
    "cd /d `"%~dp0`""
    "title ApiPorteroSpa - Panel"
    "if exist `"%~dp0.venv\Scripts\python.exe`" ("
    "  `"%~dp0.venv\Scripts\python.exe`" `"%~dp0desktop_app.py`""
    ") else ("
    "  python `"%~dp0desktop_app.py`""
    ")"
    "if errorlevel 1 ("
    "  echo."
    "  echo Fallo al abrir el panel. Ejecuta Instalar.bat primero."
    "  pause"
    ")"
) | Set-Content -Path (Join-Path $Root "Iniciar_Panel.bat") -Encoding ASCII

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
        Write-Host "No se pudo crear .ico - se usara icono por defecto" -ForegroundColor Yellow
    }
}

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
    $sc.Description = "Panel ApiPorteroSpa"
    if (Test-Path $icoPath) {
        $sc.IconLocation = ($icoPath + ",0")
    } elseif (Test-Path $venvPy) {
        $sc.IconLocation = ($venvPy + ",0")
    }
    $sc.Save()
    Write-Host ("Acceso directo OK: " + $shortcutPath) -ForegroundColor Green
} catch {
    Write-Host "No se pudo crear el acceso directo." -ForegroundColor Yellow
    Write-Host "Podes abrir Iniciar_Panel.bat manualmente." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Instalacion completada" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "En el Escritorio: ApiPorteroSpa"
Write-Host "1) Abrilo o Iniciar_Panel.bat"
Write-Host "2) Completa API Key y URL de GestionSpa"
Write-Host "3) Pulsa Iniciar servicio"
Write-Host "4) Configura el ZKTeco con IP y puerto 8081"
Write-Host ""
