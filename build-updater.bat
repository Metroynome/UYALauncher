@echo off
echo ========================================
echo Building UYA Launcher Updater
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
echo Step 2: Building Updater...
echo.
if not exist "updater\updates" (
    echo WARNING: updater\updates\ folder not found!
    echo The updater will be built with only the launcher exe (no data updates).
    echo Create updater\updates\ and add files if you want to update data.
    echo.
    pause
)

cd updater
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
echo Output: updater\bin\Release\net8.0-windows\win-x64\publish\UYALauncherUpdater.exe
echo.
echo IMPORTANT: This updater is release-specific!
echo Upload it to your GitHub release manually.
echo.
pause
