# Passgen store listing text

Version 1.1.0

## Name
Passgen

## Summary (Chrome, 132 characters max)
Strong passwords and passphrases in one click. Nothing saved, nothing sent, clipboard cleared for you.

## Summary (Firefox, 250 characters max)
Click the toolbar icon to copy a strong password or passphrase. Presets, rules for fussy sites, a password checker and automatic clipboard clearing. Everything stays on your computer.

## Description
Passgen makes strong passwords, passphrases, PINs and random tokens, and gets out of your way.

Click the toolbar icon and a new password goes on your clipboard, made with your chosen options. About a minute and a half later the clipboard is wiped, unless you have copied something else since, in which case it is left alone.

What it does
- Passwords, passphrases, PINs and hash-sized random tokens.
- Presets, so "Work", "Home" and "Fussy bank site" each keep their own options. The icon copies from the selected one.
- Rules for fussy sites: shortest and longest length, how many numbers and symbols, upper and lower case, no repeated characters, no runs like abc or 123, nothing that looks like a birth date, characters to avoid.
- Passphrases from everyday words, with word length, separator, capitals and a number on the end all adjustable, a profanity filter and your own words to exclude.
- The strength is shown in bits for every result, with an estimate of how long it would take to crack, and a suggestion when you pick something weak. It never changes what you asked for.
- A password checker for ones you already use. It runs on your computer only.
- Right click in any text or password box to fill it with a new password.
- Save a batch of up to 100 as a text or CSV file.
- Back up and restore your settings as a file.

Private by design
- Passwords are made with the browser's own secure random number source.
- Nothing is sent anywhere. There is no network access, no account and no tracking.
- Passwords are never stored. Only your options are saved, on your computer.

Shortcuts
- Alt+Shift+P copies a new password (same as the icon).
- Alt+Shift+G opens the options page.

## Category
Chrome: Productivity (or Tools). Firefox: Privacy & Security.

## Single purpose (Chrome)
Generate strong passwords and passphrases and copy them to the clipboard.

## Permission justifications (Chrome)
- storage: saves your options and presets on your computer.
- clipboardWrite: copies the generated password to the clipboard.
- clipboardRead: checks the clipboard still holds the generated password before clearing it, so something else you copied is never wiped.
- contextMenus: adds "Insert a Passgen password here" to the right click menu on text boxes.
- activeTab and scripting: fill the text box you right clicked when you choose that menu item. Nothing is read from the page.
- offscreen: Chrome's service worker cannot use the clipboard, so a hidden page does the copying and clearing.

## Data usage (Chrome form)
Collects no user data. Does not sell or transfer user data. Does not use data for unrelated purposes. Tick all three certifications. No privacy policy page is needed when no data is collected, but a one line statement can be linked if the form asks.

## Notes to reviewer (Firefox, source code submission)
Upload passgen-source.zip. No dependencies need installing. With Node 20 or newer, run `npm run build`, then the packaged extension is in dist/firefox. The build only copies src/ and the matching manifest, and writes the word and filter lists in tools/lists/ out as JavaScript data files. The extension makes no network requests. The add-on ID is passgen@mithsys.xyz and it collects no data.

## Release notes
1.1.0: presets, rules for fussy sites, password checker, right click fill, settings backup and restore, new options page and icons.
