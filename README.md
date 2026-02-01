# UYA Launcher

A custom launcher for Ratchet & Clank: Up Your Arsenal that provides enhanced features, and seamless integration with PCSX2.

## Features

- **First Run Setup** - Select your ISO, and other settings on the first run.  Once set, you'll never see this window again
- **Auto-Launch ISO** - Auto runs your selected ISO (any time running after first setup)
- **Custom Maps Auto-Update** - Automatically download/update Horizon custom maps
- **Post-shutdown Cleanup** - Cleanup tasks after PCSX2 closes
- **Saves Settings** - Saves your chosen settings in `config.ini`
  - You can edit the `config.ini` file in any text editor.

## Building

Requirements:
- Visual Studio 2019 or later with C++ tools
- CMake 3.10 or later

Step 1: cd into directory
```
cd /path/to/UYALauncher
```

Step 2: make build folder and cd into it
```
mkdir build
cd build
```

Step 3: build
```
cmake ..
cmake --build . --config Release
```