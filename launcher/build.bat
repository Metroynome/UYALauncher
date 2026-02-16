@echo off
echo UYA Launcher - Build Script
echo ============================
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo .NET SDK found!
echo.

REM Build the project
echo Building in Release mode...
dotnet build -c Release

if errorlevel 1 (
    echo.
    echo BUILD FAILED!
    pause
    exit /b 1
)

echo.
echo BUILD SUCCESSFUL!
echo.
echo Executable location:
echo bin\Release\net8.0-windows\UYALauncher.exe
echo.
echo To create a standalone exe, run: build-standalone.bat
echo.
pause
