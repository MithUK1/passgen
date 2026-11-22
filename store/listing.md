# Store listing text

## Name
Passgen

## Summary (Chrome, max 132 characters)
Strong passwords and passphrases in one click. Nothing saved, nothing sent, clipboard cleared for you.

## Summary (Firefox, max 250 characters)
Click the toolbar icon to copy a strong password or passphrase. Presets, rules for fussy sites, a password checker and automatic clipboard clearing. Everything stays on your computer.

## Description
Passgen makes strong passwords, passphrases, PINs and random tokens, and gets out of your way.

Click the toolbar icon and a new password goes on your clipboard, made with your chosen options. About a minute and a half later the clipboard is wiped, unless you have copied something else since, in which case it is left alone.

- Four types: passwords, passphrases, PINs and hash-sized random tokens (hex or Base64, 128 to 512 bits).
- Presets, so "Work", "Home" and "Fussy bank site" each keep their own options. The icon copies from the selected one.
- Rules for fussy sites: shortest and longest length, how many numbers and symbols, upper and lower case, no repeated characters, no runs like abc or 123, nothing that looks like a birth date, characters to avoid.
- Passphrases from everyday words, with word length, separator, capitals and a number on the end all adjustable, a profanity filter and your own words to exclude. You can also load your own word list.
- The strength is shown in bits for every result, with an estimate of how long it would take to crack, and a suggestion when you pick something weak. It never changes what you asked for.
- A password checker for ones you already use. It runs on your computer only.
- Right-click any text or password box and choose "Insert a Passgen password here" to fill it.
- Save a batch of up to 100 as a text or CSV file, and back up or restore your settings as a file.
- Alt+Shift+P copies a new password (same as the icon). Alt+Shift+G opens the settings page.

Private by design: passwords are made with the browser's own secure random number source, nothing is sent anywhere, and passwords are never stored. Only your settings are saved, on your computer.

## Category
Productivity (Chrome) / Privacy & Security (Firefox)

## Chrome Web Store: privacy tab
Single purpose: Generate strong passwords and passphrases and copy them to the clipboard.

Permission justifications:
- storage: Saves the user's settings and presets on their computer.
- clipboardWrite: Copies the generated password to the clipboard.
- clipboardRead: Checks the clipboard still holds the generated password before clearing it, so something else the user copied is never wiped.
- contextMenus: Adds "Insert a Passgen password here" to the right-click menu on text boxes.
- activeTab: Lets the right-click menu item fill the text box that was clicked, on that tab only. No page content is read.
- scripting: Runs the small fill step in the clicked text box when the user chooses the menu item.
- offscreen: Chrome's service worker cannot use the clipboard, so a hidden page does the copying and the timed clearing.

Data usage: no user data is collected, transmitted or sold. It is not used for anything unrelated to the single purpose, or to decide creditworthiness. Tick all three certifications. Remote code: none.
Privacy policy URL: https://github.com/MithUK1/passgen#privacy

## Firefox (AMO)
Data collection: none required (declared in the manifest).
Source code: https://github.com/MithUK1/passgen. Nothing is minified or bundled. `extension/src` is copied as written, and `npm run build` (Node 20 or newer, no dependencies to install) also writes the word lists in `tools/lists` out as the `words-*.js` files. Output is in `extension/firefox`.

## Release notes
1.0.0: First full release. Passwords, passphrases, PINs and random tokens, presets, rules for fussy sites, a password checker, right-click fill, settings backup and restore, a help link and a version label on the settings page.

## Images
- Icon: `icon-512.png` (source `icon.svg`).
- Small promo tile: `promo-tile-440x280.png`.
- Marquee promo: `promo-marquee-1400x560.png`.
- Screenshots (1280x800): `screenshots/shot1-one-click.png` to `shot4-tokens.png`. Made with `node tools/art/render-store.mjs`.
