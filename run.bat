@echo off
setlocal enabledelayedexpansion
title Downloader.io - Modern Download Manager

echo ========================================================
echo         Downloader.io - Modern Download Manager
echo ========================================================
echo.

:: Locate dotnet
set "DOTNET_CMD=dotnet"
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    if exist "%LocalAppData%\Microsoft\dotnet\dotnet.exe" (
        set "DOTNET_CMD=%LocalAppData%\Microsoft\dotnet\dotnet.exe"
    ) else if exist "%ProgramFiles%\dotnet\dotnet.exe" (
        set "DOTNET_CMD=%ProgramFiles%\dotnet\dotnet.exe"
    ) else (
        echo [ERROR] .NET SDK / Runtime not found.
        echo Please ensure .NET 8 SDK is installed.
        pause
        exit /b 1
    )
)

echo Starting Downloader.io...
echo.

"%DOTNET_CMD%" run --project "%~dp0Downloader.csproj"

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Application exited with error code %errorlevel%.
    pause
)
