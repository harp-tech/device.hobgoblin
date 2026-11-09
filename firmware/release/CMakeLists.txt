# Builds the release image of the device firmware. The firmware is built once
# for each board, and the UF2 images are concatenated into a single file,
# since every UF2 block is tagged with the family of the chip it targets.
cmake_minimum_required(VERSION 3.18)
project(release NONE)

include(ExternalProject)
include(../device.cmake)

set(PICO_BOARDS pico pico2 CACHE STRING "Boards to include in the release image")
set(PREVIEW_VERSION 0 CACHE STRING "Preview number, or v for a release build")

set(FIRMWARE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/..)

# The Harp version reported by core.pico is the protocol version it implements
set(CORE_HEADER ${FIRMWARE_DIR}/lib/core.pico/firmware/inc/harp_core.h)
file(STRINGS ${CORE_HEADER} HARP_PROTOCOL REGEX "HARP_PROTOCOL = {")
if(NOT HARP_PROTOCOL MATCHES "{ *([0-9]+), *([0-9]+)")
    message(FATAL_ERROR "Unable to read the protocol version from ${CORE_HEADER}")
endif()
set(HARP_FORMAT ${CMAKE_MATCH_1}.${CMAKE_MATCH_2})

set(PREVIEW_SUFFIX "")
if(NOT PREVIEW_VERSION MATCHES "^v")
    set(PREVIEW_SUFFIX -preview${PREVIEW_VERSION})
endif()
set(BUILD_NAME ${DEVICE_NAME}-fw${FW_VERSION_MAJOR}.${FW_VERSION_MINOR}-harp${HARP_FORMAT}-hw${HW_VERSION_MAJOR}.${HW_VERSION_MINOR}-ass0${PREVIEW_SUFFIX})

string(TOLOWER ${DEVICE_NAME} EXECUTABLE_NAME)
set(BOARD_ARGS
    -DFIRMWARE_VERSION=${FIRMWARE_VERSION}
    -DHARDWARE_VERSION=${HARDWARE_VERSION}
    -DCMAKE_BUILD_TYPE=${CMAKE_BUILD_TYPE}
)
foreach(VARIABLE PICO_SDK_PATH picotool_DIR)
    if(DEFINED ${VARIABLE})
        list(APPEND BOARD_ARGS -D${VARIABLE}=${${VARIABLE}})
    endif()
endforeach()

set(BOARD_IMAGES)
foreach(BOARD IN LISTS PICO_BOARDS)
    ExternalProject_Add(${BOARD}
        SOURCE_DIR ${FIRMWARE_DIR}
        BINARY_DIR ${CMAKE_CURRENT_BINARY_DIR}/${BOARD}
        CMAKE_ARGS ${BOARD_ARGS} -DPICO_BOARD=${BOARD}
        INSTALL_COMMAND ""
        BUILD_ALWAYS TRUE
    )
    list(APPEND BOARD_IMAGES ${CMAKE_CURRENT_BINARY_DIR}/${BOARD}/${EXECUTABLE_NAME}.uf2)
endforeach()

file(MAKE_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}/bin)
add_custom_target(image ALL
    COMMAND ${CMAKE_COMMAND} -E cat ${BOARD_IMAGES} > bin/${BUILD_NAME}.uf2
    WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
    DEPENDS ${PICO_BOARDS}
    COMMENT "Combining board images into ${BUILD_NAME}.uf2"
)
