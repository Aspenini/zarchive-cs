# ZArchive.NET build automation: `just build` produces a fully working,
# tested build for the current machine.

set windows-shell := ["powershell", "-NoLogo", "-Command"]

# Runtime identifier of the current machine, e.g. win-x64, osx-arm64.
os_part := if os() == "windows" { "win" } else if os() == "macos" { "osx" } else { "linux" }
arch_part := if arch() == "aarch64" { "arm64" } else { "x64" }
rid := os_part + "-" + arch_part

# Pull submodules, build and stage the native bridge, then build and test
# the managed library against it.
build:
    git submodule update --init --recursive
    cmake -S native -B native/build/{{rid}} -DCMAKE_BUILD_TYPE=Release
    cmake --build native/build/{{rid}} --config Release
    cmake --install native/build/{{rid}} --config Release --prefix artifacts/native/{{rid}}
    dotnet test ZArchive.NET.slnx -c Release

# Cross-compile the native bridge for osx-x64 (Intel) from an arm64 Mac.
# Stages to artifacts/native/osx-x64; does not run tests (x64 dylib cannot
# load in a native arm64 process).
build-osx-x64:
    git submodule update --init --recursive
    cmake -S native -B native/build/osx-x64 -DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_ARCHITECTURES=x86_64
    cmake --build native/build/osx-x64 --config Release
    cmake --install native/build/osx-x64 --config Release --prefix artifacts/native/osx-x64
