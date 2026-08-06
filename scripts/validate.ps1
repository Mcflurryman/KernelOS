[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Invoke-DotnetStep {
    param(
        [string]$Message,
        [string[]]$Arguments
    )

    Write-Host "==> $Message" -ForegroundColor Cyan
    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Falló la fase: $Message"
    }
}

Push-Location $repositoryRoot
try {
    if (-not $SkipRestore) {
        Invoke-DotnetStep -Message "Restaurando dependencias" -Arguments @("restore", "KernelOS.sln")
    }

    Invoke-DotnetStep -Message "Compilando en configuración $Configuration" -Arguments @("build", "KernelOS.sln", "--configuration", $Configuration, "--no-restore")
    Invoke-DotnetStep -Message "Ejecutando pruebas en configuración $Configuration" -Arguments @("test", "KernelOS.sln", "--configuration", $Configuration, "--no-build", "--no-restore")

    Write-Host "Validación completada correctamente." -ForegroundColor Green
}
finally {
    Pop-Location
}
