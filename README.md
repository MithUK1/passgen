# Passgen

A password generator for Chrome, Edge and Firefox, with a matching Windows app. One click puts a strong password on your clipboard, nothing is sent anywhere, and nothing is kept afterwards.

- Version: 1.1.0
- Repo: https://github.com/MithUK1/Passgen
- Browsers: Chrome and Edge (Manifest V3, the same build) and Firefox 140 or newer
- The Windows app has the same types, sizes and options as the extension. See Desktop app below.

## What it does

- Left click the toolbar icon to copy a new password. The clipboard is wiped after a timer (90 seconds by default), unless you have copied something else since.
- Four types: password, passphrase, PIN and hash (random token).
- Named presets. The icon always copies from the selected one.
- Right click in any text box and choose "Insert a Passgen password here" to fill it.
- Rules for fussy sites: length limits, minimum and maximum numbers and symbols, letter case, repeats, runs, birth dates, characters to avoid.
- Strength is worked out from how the password was made, with a crack time estimate and a separate "Check a password" box.
- Bulk generate and save to .txt or .csv, and back up or restore your settings as JSON.
- Everything runs locally. No network access, no tracking, no accounts.

## Install

You need Node to build it.

    npm run build
    npm test

The build writes a ready-to-load copy for each browser to `dist/chrome` and `dist/firefox`.

- Chrome: open `chrome://extensions`, turn on Developer mode, choose Load unpacked and pick `dist/chrome`.
- Edge: open `edge://extensions`, turn on Developer mode, choose Load unpacked and pick `dist/chrome`.
- Firefox: open `about:debugging`, choose This Firefox, then Load Temporary Add-on and pick `dist/firefox/manifest.json`.

## Use

| Action | How |
| --- | --- |
| Copy a password | Left click the toolbar icon, or Alt+Shift+P |
| Open the options page | Right click the icon and choose Options, or Alt+Shift+G |
| Fill a box | Right click it and choose "Insert a Passgen password here" |

Every change on the options page is saved straight away and becomes what the icon copies. There is no save button. Reset to defaults is at the bottom of the page and needs a second click to confirm.

## Settings

| Setting | Default |
| --- | --- |
| Type | Passphrase |
| Passphrase | 7 words of 6 letters, capitalised, hyphen separated, 2 digit number on the end |
| Password | 20 characters, upper, lower, numbers and symbols |
| PIN | 6 digits |
| Hash | 256 bits, hex |
| Clear clipboard after | 90 seconds |

Strong means 80 bits or more. The figure is shown under every result. It never changes what you asked for, it only suggests how to improve it.

## Desktop app

A Windows version of the same generator, in one exe. It has the four types, every option, presets, the rules for fussy sites, the password checker, bulk save to .txt or .csv, and settings backup as JSON. The word lists are built in and it makes no network requests.

- Run `Passgen.exe`. The exe isn't signed, so Windows may warn you the first time. If it does, choose **More info**, then **Run anyway**.
- It installs itself for your user, with a shortcut on the Desktop, in the Start menu and in the folder you ran it from.
- To update, run a newer exe. It closes the running copy, replaces the program and keeps your settings. An older exe never replaces a newer install.
- The version shown bottom left is `v<version> - <checksum>`. The checksum is the start of the exe's SHA-256, so two builds of one version can be told apart.
- Settings has an App tab with three options, all on by default: minimise to the tray, close to the tray, and start with Windows. Start with Windows puts a shortcut in your Startup folder and opens Passgen hidden in the tray. Click the tray icon to bring the window back. To exit, right-click the tray icon and choose Exit, or use Exit Passgen on the App tab. With close to the tray off, the close button exits.
- Everything you change is saved at once. Settings has the site rules, how many to make, how long the clipboard keeps the result, presets, backup and reset.
- Copying keeps the result out of Windows clipboard history, and the clipboard is cleared after the set time if it still holds what was copied.
- Run the exe with `-Uninstall` to remove the program, shortcuts and settings.

To build it you need the .NET SDK. Then run `desktop\build.cmd`. `Passgen.exe` lands in the `desktop` folder. The settings file, log and installed copy live in `%LOCALAPPDATA%\Passgen`.

## Privacy

- Passwords are never saved, in any form. They are held in memory while shown and dropped when the tab closes or the clear timer ends.
- Only your presets and settings are stored, in the browser's own extension storage.
- Save .txt and Save .csv write plain, unencrypted files, and only when you press the button.

Permissions used: `storage`, `clipboardWrite`, `clipboardRead`, `contextMenus`, `activeTab`, `scripting`, and `offscreen` on Chrome only. There is no website access.

## More detail

- [HOW_IT_WORKS.md](HOW_IT_WORKS.md) explains how each part works and why it was done that way.
- [CONVENTIONS.md](CONVENTIONS.md) covers the look, help text, versioning, settings handling and code style.

## Layout

    src/          extension code (options page, background, generator, strength, clipboard)
    desktop/      Windows app (C#), built with desktop\build.cmd
    manifests/    one manifest per browser
    tools/        build script and word list sources
    test/         tests, run with npm test
    store/        listing text and artwork for the extension stores

## Known limits

- Right click fill does not work on pages where the browser blocks extensions, such as its own settings pages. The password is still copied.
- Some sites reject symbols, emoji or very long passwords. Turn those options down for them.

## Licence

MIT, see [LICENSE](LICENSE).
