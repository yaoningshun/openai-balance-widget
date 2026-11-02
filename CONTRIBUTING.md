# Contributing

Saldo should stay small. Prefer fixes that improve accuracy, legibility, trust or the first connection experience. Avoid analytics, bundled browser engines and unrelated dashboards.

Build and run the offline tests with `./build.ps1 -Test`. Test design changes with `--demo`, so screenshots never expose personal data. Include before/after screenshots for visual changes. Check light and dark mode, click versus drag, stale balance, empty state and high-DPI text.

Keep total and paid credit semantics separate. Never add a provider by guessing its endpoint or pretending a monthly budget is a balance. Document a provider's authentication and limitations before implementing it.

Do not commit runtime data or credentials. Keep pull requests focused and include validation notes. English and Chinese documentation improvements are welcome.
