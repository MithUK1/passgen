# Conventions

Short notes on how Passgen is put together, so changes stay consistent.

## UI look

- One options page that opens in a full tab. Result and strength on the left stay put while the options scroll on the right.
- Light and dark themes follow the system. All colours are CSS variables at the top of `src/options.css`; nothing is hard coded further down.
- System fonts for text, a monospace face for passwords.
- Icons are inline SVG from a single sprite. No icon fonts, no external files.
- Buttons that destroy something (reset, delete preset) need a second click to confirm.
- Hidden elements use the `hidden` attribute; the stylesheet forces it to win.
- The page must work from keyboard alone and at narrow widths.

## Help text

- Every section with a choice that is not obvious has a `?` button. All the wording lives in `src/help.js`, keyed by section.
- Plain sentences, second person, no jargon. Say what it does, then why you might change it.
- Numbers quoted in help (defaults, limits, bit figures) must match the code. Check them when a default changes.
- Do not state the size or origin of the word lists anywhere.
- Update `HOW_IT_WORKS.md` and `README.md` in the same change as the help.

## Version scheme

- Semantic versioning: MAJOR.MINOR.PATCH.
  - PATCH for fixes, MINOR for new options or features, MAJOR for anything that breaks stored settings.
- The version is set in three places and they must match: `package.json`, `manifests/chrome.json`, `manifests/firefox.json`.
- The current version is also written in `README.md`.
- Store release notes live in `store/listing.md`.

## Config handling

- All settings live under one storage key, `state`, as `{active, presets}`. Presets hold the full set of options, with a maximum of 20.
- Defaults are defined once in `src/settings.js`. Anything loaded from storage or an imported file goes through the sanitiser, which fills gaps from the defaults and checks every value's type and range.
- Changes are saved immediately. There is no save button.
- Never store a generated password, a fingerprint of one that outlives the timer, or any page content.
- The welcome flag and any loaded word list sit in `storage.local`, outside the presets, and are not part of a backup.
- Backup files are plain JSON, contain settings only, and importing replaces the current settings.
- If the storage layout changes, bump MAJOR and migrate from the old key rather than reading it directly.

## Code style

- Plain ES modules, no frameworks, no runtime dependencies.
- Two space indent, single quotes, semicolons, `const` by default.
- `src/gen.js` and `src/strength.js` stay free of browser APIs so the tests can run them in Node.
- Randomness only from `crypto.getRandomValues`, always through the rejection sampling helper. Never `Math.random`.
- No `innerHTML`. Build the page with DOM calls and `textContent`.
- Comments explain why, not what. Keep them short and only where the reason is not obvious.
- No dead code, no commented out code, no unused files.
- Every behaviour change in the generator or settings needs a test. Run `npm test` before committing.
- Browser differences are handled in `src/background.js` and `src/clipboard.js` only.
