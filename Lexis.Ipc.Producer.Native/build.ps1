#Requires -Version 5.1
<#
.SYNOPSIS
  Build Lexis.Ipc.Producer.Native (C++ / libzmq) and install to bin/.

.EXAMPLE
  .\build.ps1
  .\build.ps1 -Clean
#>
param(
    [switch]$Clean,
    [ValidateSet('Release', 'Debug')]
    [string]$Config = 'Release'
)

$ErrorActionPreference = 'Stop'
$Root = if ($PSScriptRoot) { $PSScriptRoot } else { Get-Location }
$BuildDir = Join-Path $Root 'build'
$OutDir = Join-Path $Root 'bin'

function Find-VsDevCmd {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) { return $null }
    $inst = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (-not $inst) { return $null }
    $dev = Join-Path $inst 'Common7\Tools\VsDevCmd.bat'
    if (Test-Path -LiteralPath $dev) { return $dev }
    return $null
}

Write-Host ''
Write-Host 'LEXIS IPC Producer Native (C++)' -ForegroundColor White
Write-Host "Root: $Root"
Write-Host "Config: $Config"
Write-Host ''

$cmake = Get-Command cmake -ErrorAction SilentlyContinue
if (-not $cmake) {
    Write-Host 'ERRORE: cmake non trovato nel PATH.' -ForegroundColor Red
    exit 1
}

$devCmd = Find-VsDevCmd
if (-not $devCmd) {
    Write-Host 'ERRORE: Visual Studio C++ Build Tools non trovati.' -ForegroundColor Red
    Write-Host 'Installa "Desktop development with C++" o VS Build Tools.' -ForegroundColor Yellow
    exit 1
}

if ($Clean -and (Test-Path -LiteralPath $BuildDir)) {
    Write-Host 'Clean build/ ...' -ForegroundColor DarkGray
    try {
        Remove-Item -LiteralPath $BuildDir -Recurse -Force -ErrorAction Stop
    } catch {
        Write-Host '  WARN: clean parziale (file locked) - rebuild incrementale' -ForegroundColor Yellow
        Get-ChildItem -LiteralPath $BuildDir -Force -ErrorAction SilentlyContinue |
            ForEach-Object {
                try { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop } catch { }
            }
    }
}

New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# Configure + build inside a VS developer environment (cl.exe on PATH)
$cfgCmd = @(
    "call `"$devCmd`" -arch=amd64 -host_arch=amd64 >nul"
    "cd /d `"$BuildDir`""
    "cmake -G `"Visual Studio 17 2022`" -A x64 -DLEXIS_FETCH_ZMQ=ON `"$Root`""
    "if errorlevel 1 exit /b 1"
    "cmake --build . --config $Config --parallel"
    "if errorlevel 1 exit /b 1"
) -join ' && '

Write-Host 'Configure + build...' -ForegroundColor Cyan
cmd.exe /c $cfgCmd
if ($LASTEXITCODE -ne 0) {
    # Fallback generator name for VS 18 / Build Tools
    Write-Host 'Retry with default generator...' -ForegroundColor Yellow
    $cfgCmd2 = @(
        "call `"$devCmd`" -arch=amd64 -host_arch=amd64 >nul"
        "cd /d `"$BuildDir`""
        "cmake -A x64 -DLEXIS_FETCH_ZMQ=ON `"$Root`""
        "if errorlevel 1 exit /b 1"
        "cmake --build . --config $Config --parallel"
        "if errorlevel 1 exit /b 1"
    ) -join ' && '
    cmd.exe /c $cfgCmd2
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Build FAILED.' -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

$exeCandidates = @(
    (Join-Path $BuildDir "$Config\Lexis.Ipc.Producer.Native.exe"),
    (Join-Path $BuildDir "Lexis.Ipc.Producer.Native\$Config\Lexis.Ipc.Producer.Native.exe"),
    (Join-Path $BuildDir 'Lexis.Ipc.Producer.Native.exe')
)
$exe = $exeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $exe) {
    Write-Host 'ERRORE: exe non trovato dopo la build.' -ForegroundColor Red
    Get-ChildItem -LiteralPath $BuildDir -Recurse -Filter 'Lexis.Ipc.Producer.Native.exe' -ErrorAction SilentlyContinue |
        ForEach-Object { Write-Host "  found: $($_.FullName)" }
    exit 1
}

Copy-Item -LiteralPath $exe -Destination (Join-Path $OutDir 'Lexis.Ipc.Producer.Native.exe') -Force

# Copy libzmq shared DLL next to the exe
$dll = Get-ChildItem -LiteralPath $BuildDir -Recurse -Filter 'libzmq*.dll' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match [regex]::Escape($Config) -or $_.DirectoryName -match 'bin' } |
    Select-Object -First 1
if (-not $dll) {
    $dll = Get-ChildItem -LiteralPath $BuildDir -Recurse -Filter 'libzmq*.dll' -ErrorAction SilentlyContinue |
        Select-Object -First 1
}
if ($dll) {
    Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $OutDir $dll.Name) -Force
    Write-Host "  DLL: $($dll.Name)" -ForegroundColor DarkGray
} else {
    Write-Host '  WARN: libzmq DLL non trovata (ok se static link)' -ForegroundColor Yellow
}

Write-Host ''
Write-Host "[OK] $OutDir\Lexis.Ipc.Producer.Native.exe" -ForegroundColor Green
Write-Host 'Run: .\bin\Lexis.Ipc.Producer.Native.exe'
Write-Host ''
