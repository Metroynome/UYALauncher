@echo off
echo ========================================
echo Building UYA Launcher Web Installer
echo ========================================
echo.

cd webinstaller
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
echo Output: webinstaller\bin\Release\net8.0-windows\win-x64\publish\UYALauncherWebInstall.exe
echo.
echo This is a tiny (~2MB) web installer that downloads the full installer from GitHub.
echo Perfect for quick downloads and always gets the latest version!
echo.
pause
