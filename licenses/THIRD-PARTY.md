# Third-party software

The root LICENSE applies to the independently implemented windowsapp-va-sender application and its source code. It does not change third-party software licenses or rights in the game, its branding, or captured content.

FFmpeg and ffplay are separate programs. The public release ZIP does not include their binaries. The application downloads the pinned official BtbN build on first launch and verifies its SHA-256.

- Release: https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-05-31-13-22
- Build: N-124714-g49a77d37be-20260531, win64-gpl
- License details: https://ffmpeg.org/legal.html

The official build's license is retained under licenses/FFmpeg-GPL.txt after extraction. This GPL license is not replaced by the application's Unlicense.

Do not add downloaded FFmpeg binaries to the source repository. If you choose to redistribute them yourself, satisfy the applicable GPL requirements, including the corresponding source for that build and its dependencies.
