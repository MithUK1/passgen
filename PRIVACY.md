# Passgen Plus: Privacy Policy

Passgen Plus is a password generator for Chrome, Edge, Brave and Firefox, with a matching Windows app. It is made by MithUK1 as an individual developer. This policy covers the browser extension and the Windows app.

## Summary

Passgen Plus does not collect, send, sell or share any data. Passwords are made on your computer with the browser's own secure random number source and are never saved. It has no server, no account, no analytics and no advertising, and it makes no network requests.

## Data the extension handles

| Data | Why | Where it is kept | How long |
| --- | --- | --- | --- |
| Generated passwords, passphrases, PINs and tokens | To copy or insert them for you | In memory only, while shown or while on the clipboard | Dropped when the page closes. The clipboard is cleared after the timer (90 seconds by default) unless you have copied something else since |
| Text you type into the "Check a password" box | To estimate its strength | In memory only, on your computer | Dropped when you close or clear the page. It is never stored or sent |
| Your presets and settings (type, length, site rules, timer) | To remember your choices | The browser's extension storage on your computer. In the Windows app, a settings file in `%LOCALAPPDATA%\Passgen` | Until you reset them or remove the extension or app |

Passwords are never written to storage, logs or any file, unless you press Save .txt or Save .csv, which writes a plain, unencrypted file to a place you choose. Settings backups are saved as JSON only when you press the button.

## Collection, use and sharing

- Nothing is collected by the developer or any third party.
- Nothing is transmitted off your computer. The extension and the app load no remote code and contact no website, including the developer's.
- Nothing is sold, rented, shared or used for advertising, profiling, credit or any purpose unrelated to making passwords.
- Settings are not synced between devices.

## Permissions and why they are needed

- `storage`: keep your presets and settings.
- `clipboardWrite`: put the new password on your clipboard.
- `clipboardRead`: before clearing the clipboard, check that it still holds the password the extension copied, so something you copied later is not erased. The clipboard contents are compared in memory and never stored or sent.
- `contextMenus`: the "Insert a Passgen Plus password here" right-click item.
- `activeTab` and `scripting`: when you choose that item, put the password into the text box you right-clicked, and only then.
- `offscreen` (Chrome only): lets the browser read and clear the clipboard from a background page.

No website (host) access is requested. The extension does not read page content, and does not read or change a page except to fill the one box you chose.

## Your control

Use Reset on the options page, or remove the extension to delete everything it stored. In the Windows app, run the exe with `-Uninstall` to remove the program, shortcuts and settings.

## Children

Passgen Plus collects no personal information from anyone, including children.

## Changes

If this policy changes, the new version will be published at this address with the update that needs it.

## Contact

Questions or concerns: open an issue at https://github.com/MithUK1/passgen-plus/issues
