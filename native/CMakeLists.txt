cmake_minimum_required(VERSION 3.20)

project(zarchive_dotnet_native
    VERSION 0.1.0
    DESCRIPTION "C ABI bridge over ZArchive for the ZArchive.NET bindings"
    LANGUAGES C CXX
)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 11)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_C_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

# Static CRT on Windows so consumers do not need the VC++ redistributable.
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

set(ZAR_VENDOR_DIR "${CMAKE_CURRENT_SOURCE_DIR}/vendor")
set(ZAR_UPSTREAM_DIR "${ZAR_VENDOR_DIR}/ZArchive")
set(ZAR_ZSTD_DIR "${ZAR_VENDOR_DIR}/zstd")

if(NOT EXISTS "${ZAR_UPSTREAM_DIR}/include/zarchive/zarchivereader.h")
    message(FATAL_ERROR "Upstream ZArchive sources not found in ${ZAR_UPSTREAM_DIR}. Run: git submodule update --init --recursive (or clone the vendored repositories).")
endif()
if(NOT EXISTS "${ZAR_ZSTD_DIR}/lib/zstd.h")
    message(FATAL_ERROR "zstd sources not found in ${ZAR_ZSTD_DIR}.")
endif()

# Record the exact bundled upstream commit in the binary.
find_package(Git QUIET)
set(ZAR_UPSTREAM_COMMIT "unknown")
if(GIT_FOUND)
    execute_process(
        COMMAND "${GIT_EXECUTABLE}" -C "${ZAR_UPSTREAM_DIR}" rev-parse --short=12 HEAD
        OUTPUT_VARIABLE ZAR_UPSTREAM_COMMIT_RAW
        OUTPUT_STRIP_TRAILING_WHITESPACE
        RESULT_VARIABLE ZAR_UPSTREAM_COMMIT_RESULT
        ERROR_QUIET
    )
    if(ZAR_UPSTREAM_COMMIT_RESULT EQUAL 0)
        set(ZAR_UPSTREAM_COMMIT "${ZAR_UPSTREAM_COMMIT_RAW}")
    endif()
endif()

# Statically link zstd into the bridge so one shared library is shipped.
set(ZSTD_BUILD_PROGRAMS OFF CACHE BOOL "" FORCE)
set(ZSTD_BUILD_SHARED OFF CACHE BOOL "" FORCE)
set(ZSTD_BUILD_STATIC ON CACHE BOOL "" FORCE)
set(ZSTD_BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(ZSTD_LEGACY_SUPPORT OFF CACHE BOOL "" FORCE)
set(ZSTD_MULTITHREAD_SUPPORT OFF CACHE BOOL "" FORCE)
add_subdirectory("${ZAR_ZSTD_DIR}/build/cmake" zstd EXCLUDE_FROM_ALL)

add_library(zarchive_dotnet_native SHARED
    src/error.cpp
    src/reader.cpp
    src/writer.cpp
    src/version.cpp
    "${ZAR_UPSTREAM_DIR}/src/zarchivereader.cpp"
    "${ZAR_UPSTREAM_DIR}/src/zarchivewriter.cpp"
    "${ZAR_UPSTREAM_DIR}/src/sha_256.c"
)

target_include_directories(zarchive_dotnet_native PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/include"
    "${ZAR_UPSTREAM_DIR}/include"
    "${ZAR_ZSTD_DIR}/lib"
)

target_link_libraries(zarchive_dotnet_native PRIVATE libzstd_static)

target_compile_definitions(zarchive_dotnet_native PRIVATE
    "ZAR_DOTNET_UPSTREAM_VERSION=\"ZArchive 0.1.3 (git ${ZAR_UPSTREAM_COMMIT})\""
)

if(MSVC)
    target_compile_options(zarchive_dotnet_native PRIVATE /W4 /EHsc /utf-8)
else()
    target_compile_options(zarchive_dotnet_native PRIVATE -Wall -Wextra)
endif()

set_target_properties(zarchive_dotnet_native PROPERTIES
    OUTPUT_NAME "zarchive_dotnet_native"
    PREFIX "${CMAKE_SHARED_LIBRARY_PREFIX}"
)

# Optional: the upstream command-line tool, used by compatibility tests to
# verify that binding-created archives interoperate with upstream ZArchive.
option(ZAR_BUILD_UPSTREAM_TOOL "Build the upstream zarchive CLI tool" OFF)
if(ZAR_BUILD_UPSTREAM_TOOL)
    add_executable(zarchive_tool
        "${ZAR_UPSTREAM_DIR}/src/main.cpp"
        "${ZAR_UPSTREAM_DIR}/src/zarchivereader.cpp"
        "${ZAR_UPSTREAM_DIR}/src/zarchivewriter.cpp"
        "${ZAR_UPSTREAM_DIR}/src/sha_256.c"
    )
    target_include_directories(zarchive_tool PRIVATE
        "${ZAR_UPSTREAM_DIR}/include"
        "${ZAR_ZSTD_DIR}/lib"
    )
    target_link_libraries(zarchive_tool PRIVATE libzstd_static)
    set_target_properties(zarchive_tool PROPERTIES OUTPUT_NAME "zarchive")
endif()

install(TARGETS zarchive_dotnet_native
    RUNTIME DESTINATION .
    LIBRARY DESTINATION .
)
