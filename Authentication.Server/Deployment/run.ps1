$ErrorActionPreference = "Stop"
$serviceRoot = Split-Path -Parent $PSScriptRoot
Push-Location $serviceRoot
try {
    & dotnet (Join-Path $serviceRoot "Authentication.Server.dll") @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
