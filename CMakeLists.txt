cmake_minimum_required(VERSION 3.10)
project(UYALauncher)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Set output directory to project root
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR})
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_SOURCE_DIR})
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_SOURCE_DIR})

# Source files
add_executable(UYALauncher WIN32 main.cpp resource.rc)

# Link Windows libraries
target_link_libraries(UYALauncher)

# Windows subsystem settings
if(MSVC)
    # Use /SUBSYSTEM:WINDOWS for GUI app, /SUBSYSTEM:CONSOLE for debug
    set_target_properties(UYALauncher PROPERTIES
        LINK_FLAGS "/SUBSYSTEM:WINDOWS"
    )
endif()

# For MinGW
if(MINGW)
    set_target_properties(UYALauncher PROPERTIES
        LINK_FLAGS "-mwindows"
    )
endif()
