# Downloader.io PowerShell Launcher
$dotnetPath = if (Test-Path "$env:LocalAppData\Microsoft\dotnet\dotnet.exe") {
    "$env:LocalAppData\Microsoft\dotnet\dotnet.exe"
} else {
    "dotnet"
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "        Downloader.io - Modern Download Manager         " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Using SDK: $dotnetPath" -ForegroundColor DarkGray
Write-Host ""

& $dotnetPath run --project "$PSScriptRoot\Downloader.csproj"
