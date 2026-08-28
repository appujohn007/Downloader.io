@echo off
setlocal enabledelayedexpansion
title Downloader.io - Modern Download Manager

echo ========================================================
echo         Downloader.io - Modern Download Manager
echo ========================================================
echo.

:: Prioritize .NET 8 SDK location in LocalAppData first
set "DOTNET_CMD="
if exist "%LocalAppData%\Microsoft\dotnet\dotnet.exe" (
    set "DOTNET_CMD=%LocalAppData%\Microsoft\dotnet\dotnet.exe"
) else (
    where dotnet >nul 2>nul
    if !errorlevel! equ 0 (
        set "DOTNET_CMD=dotnet"
    ) else if exist "%ProgramFiles%\dotnet\dotnet.exe" (
        set "DOTNET_CMD=%ProgramFiles%\dotnet\dotnet.exe"
    ) else (
        echo [ERROR] .NET SDK not found.
        echo Please ensure .NET 8 SDK is installed.
        pause
        exit /b 1
    )
)

echo Starting Downloader.io with: %DOTNET_CMD%
echo.

"%DOTNET_CMD%" run --project "%~dp0Downloader.csproj"

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Application exited with error code %errorlevel%.
    pause
)
