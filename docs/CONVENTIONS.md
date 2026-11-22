# Conventions

Short notes on how Passgen is put together, so changes stay consistent.

## UI look

- One options page that opens in a full tab. Result and strength on the left stay put while the options scroll on the right.
- Light and dark themes follow the system. All colours are CSS variables at the top of `extension/src/options.css`; nothing is hard coded further down.
- System fonts for text, a monospace face for passwords.
- Icons are inline SVG from a single sprite. No icon fonts, no external files.
- Buttons that destroy something (reset, delete preset) need a second click to confirm.
- Hidden elements use the `hidden` attribute; the stylesheet forces it to win.
- The page must work from keyboard alone and at narrow widths.

## Help text

- Every section with a choice that is not obvious has a `?` button. All the wording lives in `extension/src/help.js`, keyed by section.
- Plain sentences, second person, no jargon. Say what it does, then why you might change it.
- Numbers quoted in help (defaults, limits, bit figures) must match the code. Check them when a default changes.
- Do not state the size or origin of the word lists anywhere.
- Update `docs/HOW_IT_WORKS.md` and `README.md` in the same change as the help.

## Version scheme

- Semantic versioning: MAJOR.MINOR.PATCH.
  - PATCH for fixes, MINOR for new options or features, MAJOR for anything that breaks stored settings.
- `extension/manifests/chrome.json` carries a public `key` so the unpacked extension keeps the same id wherever the folder is. The private key stays on the PC, never in the repo. Take the `key` line out of a copy uploaded to the Chrome Web Store.
- The version is set in three places and they must match: `package.json`, `extension/manifests/chrome.json`, `extension/manifests/firefox.json`.
- The current version is also written in `README.md`.
- Store release notes live in `store/listing.md`.

## Config handling

- All settings live under one storage key, `state`, as `{active, presets}`. Presets hold the full set of options, with a maximum of 20.
- Defaults are defined once in `extension/src/settings.js`. Anything loaded from storage or an imported file goes through the sanitiser, which fills gaps from the defaults and checks every value's type and range.
- Changes are saved immediately. There is no save button.
- Never store a generated password, a fingerprint of one that outlives the timer, or any page content.
- The welcome flag and any loaded word list sit in `storage.local`, outside the presets, and are not part of a backup.
- Backup files are plain JSON, contain settings only, and importing replaces the current settings.
- If the storage layout changes, bump MAJOR and migrate from the old key rather than reading it directly.

## Code style

- Plain ES modules, no frameworks, no runtime dependencies.
- Two space indent, single quotes, semicolons, `const` by default.
- `extension/src/gen.js` and `extension/src/strength.js` stay free of browser APIs so the tests can run them in Node.
- Randomness only from `crypto.getRandomValues`, always through the rejection sampling helper. Never `Math.random`.
- No `innerHTML`. Build the page with DOM calls and `textContent`.
- Comments explain why, not what. Keep them short and only where the reason is not obvious.
- No dead code, no commented out code, no unused files.
- Every behaviour change in the generator or settings needs a test. Run `npm test` before committing.
- Browser differences are handled in `extension/src/background.js` and `extension/src/clipboard.js` only.

## Desktop app

The Windows app in `app/` follows the same rules as the other exe tools in the collection, and the generator in `app/Generator.cs` is a port of `extension/src/gen.js`. Change one and check the other.

- C# on .NET Framework 4.8, WinForms drawn in code, no run-time packages. The colours, fonts and flat buttons are defined once in `Theme.cs`. Every window has the accent bar, the app icon and the version label bottom left.
- The button is called Settings. There is one help page, the README, opened by the Help button in Settings.
- The version is `Store.AppVersion` and `<Version>` in the project file, and they must match. It is shown as `v<x.y.z> - <checksum>`, the checksum being the first eight characters of the exe's SHA-256. The Windows app and the extension share one version number and always match.
- Running the exe installs it to `%LOCALAPPDATA%\Passgen` with an icon and shortcuts (Desktop, Start menu, and the folder it was run from). A newer copy, or the same version with different content, upgrades the install. There is no update check and nothing is downloaded.
- Everything the user sets lives in `%LOCALAPPDATA%\Passgen\Passgen.ini`, saved through a temporary file that is read back first. An unreadable file is kept as `.bad` and the app starts with defaults. Nothing is kept in the registry. Export and import use JSON. Reset has two levels.
- The word lists are the same text files the extension is built from, embedded in the exe, so there is one copy of each list.
- Randomness only from `RandomNumberGenerator`, through the rejection sampling in `Generator.RandomInt`. `System.Random` is used only to measure how often a rule accepts a try.
- Copy keeps the result out of clipboard history and clears it after the set time, only if the clipboard still holds it.
