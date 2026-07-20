# ZArchive.NET

Idiomatic .NET bindings for the [ZArchive](https://github.com/Exzap/ZArchive)
archive format (`.zar` / `.wua`).

ZArchive.NET wraps the official ZArchive C++ implementation behind a normal
.NET API. Archives are zstd-compressed in 64 KiB blocks and support true
random-access reads inside archived files — you can `Seek` and `Read` in a
multi-gigabyte archived file without extracting it.

> ZArchive.NET is an independent .NET binding for the ZArchive library and is
> not an official upstream project.

## Why no C++ toolchain is required

The NuGet package ships a prebuilt native bridge library
(`zarchive_dotnet_native`) for each supported platform under
`runtimes/<rid>/native/`. The upstream ZArchive implementation and zstd are
statically linked into that one library. Consumers only ever call the managed
`ZArchive.dll`; no CMake, C++ compiler, or zstd installation is needed.

## Installation

```
dotnet add package ZArchive.NET
```

Supported target framework: .NET 8.0+.

### Supported platforms

| Runtime identifier | Status |
|---|---|
| `win-x64` | Built and tested |
| `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64` | Planned (native builds pending CI runners) |

A runtime identifier is only advertised once CI has executed the test suite
on that platform.

## Reading an archive

```csharp
using ZArchive;

using ZArchiveReader archive = ZArchiveReader.Open("game.zar");

// List entries
foreach (ZArchiveEntry entry in archive.EnumerateEntries(recursive: true))
{
    Console.WriteLine($"{entry.EntryType}: {entry.FullName}");
}

// Random-access read of one archived file
using Stream input = archive.OpenRead("content/example.bin");
input.Seek(1024, SeekOrigin.Begin);
byte[] header = new byte[64];
input.ReadExactly(header);
```

## Extracting safely

```csharp
using ZArchiveReader archive = ZArchiveReader.Open("game.zar");
archive.ExtractToDirectory("extracted", new ZArchiveExtractionOptions
{
    Overwrite = false,
    Progress = progress,          // optional IProgress<ZArchiveExtractionProgress>
    CancellationToken = token,    // optional
});
```

Extraction validates every entry path: rooted paths, `..` traversal segments
and names invalid on the local filesystem are rejected with an `IOException`
before any file is written.

## Creating an archive

```csharp
using ZArchive;

// Convenience: pack a whole directory
ZArchiveFile.CreateFromDirectory("ExtractedGame", "Game.zar");

// Manual control
using ZArchiveWriter writer = ZArchiveWriter.Create("assets.zar");
writer.CreateDirectory("textures");
using (Stream entry = writer.CreateEntry("textures/logo.bin"))
{
    entry.Write(data);
}
writer.Complete(); // the archive is only valid after Complete()
```

Archives are append-only: only one entry stream may be open at a time, and a
writer disposed without `Complete()` deletes its partial output file.

## Format and path notes

- Archive paths use `/` as separator; `\` is accepted and normalized, and a
  leading `/` is optional.
- Lookups are case-insensitive for Latin letters (upstream behavior). Case
  is preserved in stored names.
- Upstream stores names with Windows-1252 conventions; prefer names
  representable in Windows-1252 for maximum compatibility.
- Archives cannot be modified after creation (upstream design).
- Reads are synchronous native operations; the inherited async `Stream`
  methods wrap the synchronous implementation.

## Native dependency information

- Bridge library: `zarchive_dotnet_native` (C ABI, no C++ types exported).
- Bundled upstream ZArchive and zstd versions: see
  [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), or query
  `ZArchiveInfo.UpstreamVersion` at runtime.
- On Windows the bridge statically links the MSVC runtime; no VC++
  redistributable is needed.

### Troubleshooting native loading

If loading fails, the thrown `DllNotFoundException` includes the OS, process
architecture and runtime identifier. Common causes:

- The platform/architecture has no native asset in the package (see the
  supported platforms table).
- The app was published without runtime assets (e.g. some single-file
  configurations); ensure `runtimes/<rid>/native/` content is preserved.

## Building from source

With [just](https://github.com/casey/just) installed, `just build` runs the
whole sequence below (submodules, native build, staging, tests) for the
current platform.

Manually: build the native bridge for the current platform, stage it, then
build the managed side (`<rid>` is the runtime identifier, e.g. `win-x64`,
`linux-arm64`, `osx-arm64`):

```
git clone --recurse-submodules https://github.com/Aspenini/zarchive-cs.git
cd zarchive-cs

cmake -S native -B native/build/<rid> -DCMAKE_BUILD_TYPE=Release
cmake --build native/build/<rid> --config Release
cmake --install native/build/<rid> --config Release --prefix artifacts/native/<rid>

dotnet test
```

### Packing the cross-platform NuGet package

The package includes one native asset per RID: everything staged under
`artifacts/native/<rid>/` is packed into `runtimes/<rid>/native/`. To produce
the full cross-platform package, run the three `cmake` commands above on each
target platform (or cross-compile), collect the staged `artifacts/native/<rid>`
directories onto one machine, then pack once:

```
dotnet pack src/ZArchive/ZArchive.csproj -c Release -o artifacts/packages
```

## License

ZArchive.NET is licensed under [MIT No Attribution](LICENSE). Bundled
third-party components (ZArchive, its SHA-256 implementation, and zstd) are
covered in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
