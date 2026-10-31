# Third-party software: v1.1.4
The application's Unlicense covers only the independent sender and audio helper.

Included tools/clock/ffmpeg.exe is GPLv3 or later. It uses upstream FFmpeg 49a77d37be plus gfx-auto-sync.patch. Corresponding sources, dependency sources, licenses and build instructions are offered alongside the binaries as ffmpeg-autosync1-source.zip. Keep this source archive available when sharing the native binaries. Compiler and dependency versions differ from BtbN; see tools/clock/BUILD-INFO.txt.

Native license text is in FFmpeg-clock-GPLv3.txt and native/. x264: GPLv2+; SRT: MPL-2.0; opus: BSD; OpenSSL: Apache-2.0; GCC runtime: GPL with GCC Runtime Library Exception. The included source packages and MinGW/header notices retain their respective licenses.

The public ZIP excludes original unmodified tools/ffmpeg.exe and tools/ffplay.exe. Setup downloads those directly from BtbN and verifies SHA-256. Sessions encode with tools/clock/ffmpeg.exe. The offline-test ZIP additionally includes original BtbN tools for private testing; it is not the public distribution package. Redistributing those original tools requires their corresponding source/dependencies as well.

Pinned BtbN release: https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-05-31-13-22
FFmpeg licensing: https://ffmpeg.org/legal.html
