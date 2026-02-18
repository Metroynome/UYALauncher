@echo off
echo UYA Launcher - Standalone Build Script
echo ========================================
echo.
echo This will create a single EXE file that doesn't require .NET to be installed.
echo The file will be larger (~80MB) but completely standalone.
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Building standalone executable...
echo This may take a minute...
echo.

dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (
    echo.
    echo BUILD FAILED!
    pause
    exit /b 1
)

echo.
echo BUILD SUCCESSFUL!
echo.
echo Standalone executable location:
echo bin\Release\net8.0-windows\win-x64\publish\UYALauncher.exe
echo.
echo This EXE can be distributed and run on any Windows 10/11 PC without installing .NET!
echo.
pause
