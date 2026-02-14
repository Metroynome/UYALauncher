# UYA Launcher

A modern C# WPF launcher for Ratchet & Clank: Up Your Arsenal (UYA) on PCSX2.

## Features

- 🎮 Launch PCSX2 with UYA automatically
- 🪟 Embed PCSX2 window inside the launcher (optional)
- 🎨 Modern Windows 10/11 UI with WPF
- 🔧 Automatic game patches (widescreen, boot to multiplayer, etc.)
- 🗺️ Custom multiplayer map downloader
- 🔄 Auto-update functionality
- ⚙️ Easy configuration with tabbed settings dialog
- 🎮 Hotkey support (F11 for map updates, Ctrl+F11 for settings)

## Requirements

- Windows 10/11
- .NET 8.0 SDK (for building)
- .NET 8.0 Runtime (for running)
- PCSX2 emulator
- Ratchet & Clank: Up Your Arsenal ISO (NTSC or PAL)

## Building

### Option 1: Visual Studio (Easiest)
1. Open `UYALauncher.csproj` in Visual Studio 2022
2. Press F5 to build and run
3. Or Build → Build Solution (Ctrl+Shift+B)

### Option 2: Command Line
```bash
# Make sure you're in the UYALauncher directory
cd UYALauncher

# Restore dependencies and build
dotnet build

# Or build in Release mode for better performance
dotnet build -c Release

# Run the application
dotnet run

# Or run the built executable directly
.\bin\Debug\net8.0-windows\UYALauncher.exe
```

### Creating a Standalone Executable

To create a single-file executable that doesn't require .NET to be installed:

```bash
# Publish as a self-contained single-file exe
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# The executable will be in:
# bin\Release\net8.0-windows\win-x64\publish\UYALauncher.exe
```

## First Run

1. Launch the application
2. Select your UYA ISO file
3. Select your PCSX2 executable (pcsx2.exe or pcsx2-qt.exe)
4. Choose your region (NTSC or PAL)
5. Configure patches and settings
6. Click "Launch"

## Configuration

Configuration is stored as JSON in:
```
%APPDATA%\UYALauncher\config.json
```

## Hotkeys

- **F11**: Update custom maps
- **Ctrl+F11**: Open settings dialog

## Patches

The launcher can apply the following patches to PCSX2:

- **Boot to Multiplayer**: Skip the single-player intro and boot directly to multiplayer
- **Widescreen**: Enable 16:9 widescreen support
- **Progressive Scan**: Enable progressive scan mode

Patches are automatically written to PCSX2's `.pnach` files.

## Custom Maps

The launcher automatically downloads custom multiplayer maps from the Horizon server:
- Maps are downloaded to `<ISO directory>\uya\`
- Updated automatically on launch
- Manual update via F11 hotkey

## Updates

The launcher checks GitHub for updates on startup (if auto-update is enabled).
