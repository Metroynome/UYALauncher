@echo off
echo UYA Launcher - Complete Build Script
echo ======================================
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Step 1: Building UYALauncher...
echo.
cd launcher
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (
    echo.
    echo BUILD FAILED: UYALauncher
    cd ..
    pause
    exit /b 1
)

cd ..

echo.
echo Step 2: Building UYALauncherUpdater...
echo.
cd updater
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

if errorlevel 1 (
    echo.
    echo BUILD FAILED: UYALauncherUpdater
    cd ..
    pause
    exit /b 1
)

cd ..

echo.
echo Step 3: Building UYALauncherSetup (Installer)...
echo.
cd installer
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

if errorlevel 1 (
    echo.
    echo BUILD FAILED: UYALauncherSetup
    cd ..
    pause
    exit /b 1
)

cd ..

echo.
echo Step 4: Copying to release folder...
echo.

REM Create release directory structure
if not exist "release" mkdir release
if not exist "release\UYALauncher" mkdir release\UYALauncher

REM Copy launcher exe
copy "launcher\bin\Release\net8.0-windows\win-x64\publish\UYALauncher.exe" "release\UYALauncher\" >nul

REM Copy data folder if it exists
if exist "installer\data" (
    echo Copying data folder...
    xcopy "installer\data" "release\UYALauncher\data\" /E /I /Y >nul
) else (
    echo WARNING: installer\data folder not found, skipping...
)

REM Copy installer exe to release root
copy "installer\bin\Release\net8.0-windows\win-x64\publish\UYALauncherSetup.exe" "release\" >nul

REM Copy updater exe to release root (for GitHub releases)
copy "updater\bin\Release\net8.0-windows\win-x64\publish\UYALauncherUpdater.exe" "release\" >nul

echo.
echo ======================================
echo BUILD SUCCESSFUL!
echo ======================================
echo.
echo Release folder structure:
echo release\
echo   UYALauncherSetup.exe (installer - for new users)
echo   UYALauncherUpdater.exe (updater - for GitHub releases)
echo   UYALauncher\
echo     UYALauncher.exe
echo     data\
echo.
pause
