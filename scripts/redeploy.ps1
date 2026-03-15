param()

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $scriptPath = $MyInvocation.MyCommand.Definition
    Start-Process PowerShell -ArgumentList "-NoExit -File `"$scriptPath`"" -Verb RunAs
    exit
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectFile = Join-Path $RepoRoot 'src\Lucia.Server\Lucia.Server\Lucia.Server.csproj'
$PublishDir = Join-Path $RepoRoot 'publish\Lucia'
$InstallerPath = Join-Path $PublishDir 'Installer.ps1'
$ServiceName = 'LuciaServer'

Write-Host ''
Write-Host '=== Lucia redeploy ===' -ForegroundColor Cyan

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host ''
    Write-Host '>>> service found. uninstalling...' -ForegroundColor Yellow
    & $InstallerPath -Action uninstall
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'uninstall failed.' -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host ''
    Write-Host '>>> service not found. fresh install.' -ForegroundColor Yellow
}

Write-Host ''
Write-Host '>>> dotnet publish...' -ForegroundColor Cyan
dotnet publish $ProjectFile --configuration Release --runtime win-x64 --self-contained false --output $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host 'publish failed.' -ForegroundColor Red
    exit 1
}
Write-Host 'publish ok: ' + $PublishDir -ForegroundColor Green

Write-Host ''
Write-Host '>>> installing service...' -ForegroundColor Cyan
& $InstallerPath -Action install
if ($LASTEXITCODE -ne 0) {
    Write-Host 'install failed.' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host '=== redeploy done ===' -ForegroundColor Green