# UYA Launcher

A custom launcher for Ratchet & Clank: Up Your Arsenal that provides enhanced features, and seamless integration with PCSX2.

## Features

- **First Run Setup** - Select your ISO, and other settings on the first run.  Once set, you'll never see this window again
- **Auto-Launch ISO** - Auto runs your selected ISO (any time running after first setup)
- **Custom Maps Auto-Update** - Automatically download/update Horizon custom maps
- **Post-shutdown Cleanup** - Cleanup tasks after PCSX2 closes
- **Memory Access API** - Read/write PS2 RAM directly from C++ code for cheats and game manipulation
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

## Memory API

UYALauncher now includes a powerful memory access system that allows you to read and write to PCSX2's emulated PS2 RAM directly from C++ code.

### Quick Example

```cpp
#include "memory.h"

// Read player health
uint32_t health;
if (Memory::Read32(0x001A0000, health)) {
    std::cout << "Health: " << health << std::endl;
}

// Write infinite ammo
Memory::Write32(0x001A0100, 999);
```

### Features

- Direct memory access to PS2 RAM through PCSX2
- Automatic base address detection
- Thread-safe operations
- Type-safe read/write functions (8/16/32-bit, float, buffers, strings)
- Easy integration for cheats and game manipulation

For complete documentation and examples, see [MEMORY_API.md](MEMORY_API.md)
