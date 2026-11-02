# Release checklist

Project repository: https://github.com/yaoningshun/openai-balance-widget

1. Use the `yaoningshun/openai-balance-widget` repository, with the description: **OpenAI 悬浮窗 / API 余额悬浮球 · A tiny native Windows widget for OpenAI API credits.**
2. Push this source tree. It is an independent project; do not copy the previous monitor's config or runtime into it.
3. Suggested topics: `openai`, `api-balance`, `windows`, `desktop-widget`, `wpf`, `csharp`, `privacy`, `minimal`.
4. Let the build/test workflow complete. Check the artifact and verify that the checksum matches the executable.
5. Tag a version such as `v0.1.0`. The workflow publishes `Saldo.exe` and `SHA256SUMS.txt` to a GitHub Release. Review generated notes and state the unsigned-binary and unofficial-endpoint limitations.
6. Keep demo screenshots and the GIF in the README. Use the hero image as the repository's social preview if desired.

Earn attention through a working first experience, clear limitations, a short demo and thoughtful issue responses. Avoid fake metrics, star exchanges and unsolicited promotion.

Before broader promotion: test on a clean Windows machine and another DPI setting; obtain feedback on the manual session-connection flow; consider code signing. Current validation covers local compilation, offline tests, native demo renders and the maintainer's local environment, not a clean-machine compatibility guarantee.
