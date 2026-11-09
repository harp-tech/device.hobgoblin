# == DO NOT EDIT THE FOLLOWING LINES for the Raspberry Pi Pico VS Code Extension to work ==
if(WIN32)
    set(USERHOME $ENV{USERPROFILE})
else()
    set(USERHOME $ENV{HOME})
endif()
set(sdkVersion 2.1.1)
set(toolchainVersion 14_2_Rel1)
set(picotoolVersion 2.1.1)
set(picoVscode ${USERHOME}/.pico-sdk/cmake/pico-vscode.cmake)
if (EXISTS ${picoVscode})
    include(${picoVscode})
endif()
# ====================================================================================
set(PICO_BOARD pico CACHE STRING "Board type")

cmake_minimum_required(VERSION 3.13)

include(device.cmake)
message(STATUS "Firmware version: ${FW_VERSION_MAJOR}.${FW_VERSION_MINOR}.${FW_VERSION_PATCH}")
message(STATUS "Hardware version: ${HW_VERSION_MAJOR}.${HW_VERSION_MINOR}.${HW_VERSION_PATCH}")
message(STATUS "Interface hash: ${INTERFACE_HASH_STRING}")

find_package(Git REQUIRED)
execute_process(COMMAND "${GIT_EXECUTABLE}" rev-parse --short HEAD OUTPUT_VARIABLE COMMIT_ID OUTPUT_STRIP_TRAILING_WHITESPACE)
message(STATUS "Computed Git Hash: ${COMMIT_ID}")
add_definitions(-DGIT_HASH="${COMMIT_ID}") # Usable in source code.

#add_definitions(-DDEBUG) # Uncomment for debugging

# Specify USB Manufacturer and Product descriptions.
add_definitions(-DUSBD_MANUFACTURER="harp-tech")
add_definitions(-DUSBD_PRODUCT="${DEVICE_NAME}")

include(pico_sdk_import.cmake)

set(CMAKE_CXX_STANDARD 23)

project(hobgoblin)

pico_sdk_init()
add_subdirectory(lib/core.pico/firmware lib_build/core.pico)

add_executable(${PROJECT_NAME}
    src/main.cpp
)

include_directories(inc)

target_compile_definitions(${PROJECT_NAME} PRIVATE
    FW_VERSION_MAJOR=${FW_VERSION_MAJOR}
    FW_VERSION_MINOR=${FW_VERSION_MINOR}
    FW_VERSION_PATCH=${FW_VERSION_PATCH}
    HW_VERSION_MAJOR=${HW_VERSION_MAJOR}
    HW_VERSION_MINOR=${HW_VERSION_MINOR}
    HW_VERSION_PATCH=${HW_VERSION_PATCH}
    "INTERFACE_HASH={${INTERFACE_HASH}}"
)

target_link_libraries(${PROJECT_NAME}
    harp_c_app
    harp_sync
    pico_stdlib
    hardware_adc
    hardware_dma
)

pico_add_extra_outputs(${PROJECT_NAME})

if(DEBUG)
    message(WARNING "Debug printf() messages from harp core to UART with baud \
            rate 921600.")
    pico_enable_stdio_uart(${PROJECT_NAME} 1) # UART stdio for printf.
    # Additional libraries need to have stdio init also.
endif()
