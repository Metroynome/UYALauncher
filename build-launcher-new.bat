@echo off
echo UYA Launcher - Quick Build (Launcher Only)
echo ===========================================
echo.

cd launcher
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (
    echo.
    echo BUILD FAILED!
    cd ..
    pause
    exit /b 1
)

cd ..

echo.
echo Copying to release folder...
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

echo.
echo BUILD SUCCESSFUL!
echo.
echo Release location: release\UYALauncher\
echo.
pause
