# Passgen

A password generator for Chrome, Edge and Firefox, with a matching Windows app. One click puts a strong password on your clipboard, nothing is sent anywhere, and nothing is kept afterwards.

- Browsers: Chrome, Edge and Brave (Manifest V3, the same build) and Firefox 140 or newer.
- The Windows app has the same types, sizes and options as the extension, and the two always share one version number.

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

**Windows app:** one exe, `Passgen.exe`, in the top of the Passgen folder. It has the four types, every option, presets, the rules for fussy sites, the password checker, bulk save to .txt or .csv, and settings backup as JSON. The word lists are built in and it makes no network requests.

- Run `Passgen.exe`. The exe isn't signed, so Windows may warn you the first time. If it does, choose **More info**, then **Run anyway**.
- It installs itself for your user, with a shortcut on the Desktop, in the Start menu and in the folder you ran it from.
- To update, run a newer exe. It closes the running copy, replaces the program and keeps your settings. An older exe never replaces a newer install.
- Run the exe with `-Uninstall` to remove the program, shortcuts and settings.

**Browser extension:** load the ready-built copy from the `extension` folder.

- Chrome: open `chrome://extensions`, turn on Developer mode, choose Load unpacked and pick `extension\chrome`.
- Edge and Brave: open `edge://extensions` or `brave://extensions`, turn on Developer mode, choose Load unpacked and pick `extension\chrome`.
- Firefox, quick test: open `about:debugging#/runtime/this-firefox`, choose Load Temporary Add-on and pick `extension\firefox\manifest.json`. It is removed when Firefox restarts.
- Firefox, permanent: Firefox release builds only install add-ons Mozilla has signed. Upload the Firefox zip to the [Add-on Developer Hub](https://addons.mozilla.org/developers/) as an unlisted add-on ("On your own"), then install the signed `.xpi` from `about:addons`. Each upload needs a new version number.

## Using it

| Action | How |
| --- | --- |
| Copy a password | Left click the toolbar icon, or Alt+Shift+P |
| Open the options page | Right click the icon and choose Options, or Alt+Shift+G |
| Fill a box | Right click it and choose "Insert a Passgen password here" |

**Help** at the top right of the options page opens this page. The **?** beside each section shows a short explanation of it. The version is shown small in the bottom left corner.

Every change on the options page is saved straight away and becomes what the icon copies. There is no save button. Reset to defaults is at the bottom of the page and needs a second click to confirm.

In the Windows app:

- The version shown bottom left is `v<version> - <checksum>`. The checksum is the start of the exe's SHA-256, so two builds of one version can be told apart.
- Copying keeps the result out of Windows clipboard history, and the clipboard is cleared after the set time if it still holds what was copied.

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

In the Windows app:

- Settings has an App tab with three options, all on by default: minimise to the tray, close to the tray, and start with Windows. Start with Windows puts a shortcut in your Startup folder and opens Passgen hidden in the tray. Click the tray icon to bring the window back. To exit, right-click the tray icon and choose Exit, or use Exit Passgen on the App tab. With close to the tray off, the close button exits.
- Everything you change is saved at once. Settings has the site rules, how many to make, how long the clipboard keeps the result, presets, backup and reset.

## Where things are stored

- Extension: presets and settings in the browser's own extension storage.
- Windows app: settings file, log and installed copy in `%LOCALAPPDATA%\Passgen`.

## Privacy

- Passwords are never saved, in any form. They are held in memory while shown and dropped when the tab closes or the clear timer ends.
- Only your presets and settings are stored, in the browser's own extension storage.
- Save .txt and Save .csv write plain, unencrypted files, and only when you press the button.

Permissions used: `storage`, `clipboardWrite`, `clipboardRead`, `contextMenus`, `activeTab`, `scripting`, and `offscreen` on Chrome only. There is no website access. The extension loads no remote code and makes no network requests.

## Troubleshooting

- Right click fill does not work on pages where the browser blocks extensions, such as its own settings pages. The password is still copied.
- Some sites reject symbols, emoji or very long passwords. Turn those options down for them.

## Build from source

You need the .NET SDK for the Windows app and Node for the extension.

    build.cmd        builds Passgen.exe and both extension folders
    npm run build    builds the extension only, into extension\chrome and extension\firefox
    npm test         runs the tests

Layout:

    app/                 Windows app (C#)
    extension/src/       extension code (options page, background, generator, strength, clipboard)
    extension/manifests/ one manifest per browser
    tools/               build script, word list sources and artwork
    tests/               tests, run with npm test
    store/               listing text and artwork for the extension stores
    docs/                how it works and the conventions

`Passgen.exe`, the folder shortcut and the built `extension\chrome` and `extension\firefox` folders are not kept in the repo.

More detail: [docs/HOW_IT_WORKS.md](docs/HOW_IT_WORKS.md) explains how each part works and why. [docs/CONVENTIONS.md](docs/CONVENTIONS.md) covers the look, help text, versioning, settings handling and code style.

## Licence

MIT, see [LICENSE](LICENSE).
