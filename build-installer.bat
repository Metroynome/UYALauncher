@echo off
echo ========================================
echo Building UYA Launcher Installer
echo ========================================
echo.

echo Step 1: Building Launcher...
echo.
cd launcher
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (
    echo.
    echo LAUNCHER BUILD FAILED!
    cd ..
    pause
    exit /b 1
)

cd ..

echo.
echo Step 2: Building Installer...
echo.
if not exist "installer\data\emulator" (
    echo WARNING: installer\data\emulator\ folder not found!
    echo The installer will be built without PCSX2 data.
    echo.
    pause
)

cd installer
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

if errorlevel 1 (
    echo.
    echo BUILD FAILED!
    cd ..
    pause
    exit /b 1
)

cd ..

echo.
echo ========================================
echo BUILD SUCCESSFUL!
echo ========================================
echo.
echo Output: installer\bin\Release\net8.0-windows\win-x64\publish\UYALauncherSetup.exe
echo.
pause
