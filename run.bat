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

:: Target executable paths
set "TARGET_EXE=%~dp0bin\Debug\net8.0\DownloaderApp.exe"
set "TARGET_DLL=%~dp0bin\Debug\net8.0\DownloaderApp.dll"

:: Rebuild only if binary does not exist or user explicitly asked for --build / -b
if "%1"=="--build" goto do_build
if "%1"=="-b" goto do_build
if not exist "%TARGET_DLL%" goto do_build
goto do_run

:do_build
echo [BUILD] Building Downloader.io...
"%DOTNET_CMD%" build "%~dp0Downloader.csproj" -v q
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

:do_run
echo [LAUNCH] Launching Downloader.io...
echo.

if exist "%TARGET_EXE%" (
    "%TARGET_EXE%"
) else (
    "%DOTNET_CMD%" "%TARGET_DLL%"
)

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Application exited with error code %errorlevel%.
    pause
)
