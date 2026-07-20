# Third-party notices

ZArchive.NET bundles the following third-party components inside its native
bridge library (`zarchive_dotnet_native`). All components are statically
linked into that single shared library per platform.

## ZArchive

- Source: https://github.com/Exzap/ZArchive
- Bundled version: 0.1.3, commit `965b66c8d67b6b7e30fd63b3b75aa91a99ff303b`
  (pinned as a git submodule at `native/vendor/ZArchive`)
- License: MIT No Attribution (MIT-0)

> Copyright 2022 Exzap
>
> Permission is hereby granted, free of charge, to any person obtaining a copy
> of this software and associated documentation files (the "Software"), to
> deal in the Software without restriction, including without limitation the
> rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
> sell copies of the Software, and to permit persons to whom the Software is
> furnished to do so.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
> FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
> DEALINGS IN THE SOFTWARE.

### Bundled SHA-256 implementation

ZArchive bundles a SHA-256 implementation (`src/sha_256.c`, `src/sha_256.h`)
derived from public-domain code (Alain Mosnier's sha-2 project,
https://github.com/amosnier/sha-2, released under The Unlicense /
public-domain terms). See the upstream repository for details.

## zstd

- Source: https://github.com/facebook/zstd
- Bundled version: v1.5.7, commit `f8745da6ff1ad1e7bab384bd1f9d742439278e99`
  (pinned as a git submodule at `native/vendor/zstd`)
- License: dual-licensed BSD-3-Clause / GPL-2.0. ZArchive.NET distributes
  zstd under the **BSD 3-Clause** license.

> Copyright (c) Meta Platforms, Inc. and affiliates. All rights reserved.
>
> Redistribution and use in source and binary forms, with or without
> modification, are permitted provided that the following conditions are met:
>
> * Redistributions of source code must retain the above copyright notice,
>   this list of conditions and the following disclaimer.
> * Redistributions in binary form must reproduce the above copyright notice,
>   this list of conditions and the following disclaimer in the documentation
>   and/or other materials provided with the distribution.
> * Neither the name Facebook, nor Meta, nor the names of its contributors
>   may be used to endorse or promote products derived from this software
>   without specific prior written permission.
>
> THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
> AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
> IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
> ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
> LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
> CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
> SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
> INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
> CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
> ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
> POSSIBILITY OF SUCH DAMAGE.
