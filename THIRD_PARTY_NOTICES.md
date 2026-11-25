# Third-party notices

The ActionCache packages declare their dependencies in their `.csproj`
files, and they are restored from NuGet at build time. This file records the
assets the documentation site commits into the repository instead.

## Inter

- **Version:** the Google Fonts build (v20), as packaged by `@fontsource-variable/inter` 5.3.0
- **Source:** https://github.com/rsms/inter
- **License:** SIL Open Font License 1.1
- **Vendored at:** `site/assets/vendor/inter/` (`inter-latin-wght-normal.woff2`, `LICENSE`)

The documentation site's text face. The file is the unmodified variable
font, Latin subset, from Fontsource's npm package; `site/layouts/_partials/oss/fonts.html`
declares it.

## JetBrains Mono

- **Version:** the Google Fonts build (v24), as packaged by `@fontsource-variable/jetbrains-mono` 5.3.0
- **Source:** https://github.com/JetBrains/JetBrainsMono
- **License:** SIL Open Font License 1.1
- **Vendored at:** `site/assets/vendor/jetbrains-mono/` (`jetbrains-mono-latin-wght-normal.woff2`, `LICENSE`)

The documentation site's code face, used for commands and code blocks.
Unmodified, Latin subset, from the same source as Inter.
