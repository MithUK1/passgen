# How Passgen works

A password generator for Chrome and Firefox. I wanted one click to get a properly strong password, with nothing sent anywhere and nothing kept afterwards. So that's what it does.

## Using it

Left click the toolbar icon and a new password goes on your clipboard. The icon shows OK for a second to tell you it worked. Ninety seconds later the clipboard is wiped, unless you've copied something else since, in which case it leaves that alone.

Right click the icon and pick Options (Passgen options in Firefox) for everything you can change. It opens as a normal page in a tab, with the result and strength on the left staying put while you change things on the right. Alt+Shift+G opens it as well, and Alt+Shift+P does the same as clicking the icon.

The first time you install it, the options page opens with a short welcome that says the same thing: whatever you set is saved and is what the icon copies. Press Got it and it doesn't show again.

Out of the box it opens on a passphrase, so the icon copies a passphrase until you pick another type.

Every option you change is saved straight away and becomes what the icon copies. There is no separate save button because I kept forgetting to press it. If you want to start again, there is a Reset to defaults button at the bottom. It lists exactly what it is about to set and you have to click it a second time to go ahead.

There are four types: password, passphrase, PIN and hash.

### Presets

A preset is a full set of options with a name, like "Work" or "Fussy bank site". Pick one from the Preset box in the Options section, and that is the one the icon copies from. The plus button saves what you have now as a new preset, the pencil renames the current one and the bin deletes it (click twice, same as reset). You can have up to 20. Changing any option changes the selected preset, so make a new one first if you want to keep the old one as it was.

### Right click to fill a box

Right click in any text box or password box and pick "Insert a Passgen password here". It fills the box with a new password from the selected preset and also puts it on the clipboard, so you can paste it into a "confirm password" box. The clipboard clears on the same timer as normal.

### Check a password

The "Check a password" box under the result gives a rough strength and crack time for a password you already have. It runs on your computer only, nothing is stored or sent, and the box is emptied when you close the tab. It looks for very common passwords, ordinary words, years and runs of numbers, and otherwise counts it as random. That makes it an estimate, and it can still be a bit generous.

### Settings backup

"Back up or move your settings" saves all your presets to a JSON file and loads them back. It never contains passwords, and a word list you loaded is not in it. Importing replaces what you have now.

## How it works, and why I did it this way

### Randomness

Everything comes from the browser's own secure random number source. The obvious way to turn a random number into "pick one of N things" slightly favours some choices, so the code throws away the numbers that would cause that and asks again. It's a tiny thing but it means every character and every word is equally likely.

### Passwords

You tick which kinds of character you want and it picks from all of them together. If the result is missing one of the kinds you ticked, it throws it away and starts again, so you always get at least one of each. There are also music, chess and emoji sets for fun, but plenty of websites reject those, so they are off by default. "Avoid look-alikes" drops I, l, 1, O and 0 for when you have to read a password out or type it from a screen.

Some sites are fussy, so there is a "For fussy sites" section on the Password and Passphrase tabs. It does what the site demands and tells you when it had to change something:

- Length: at least and at most. A password is made that long, and a passphrase uses fewer or more words to fit. If a maximum is too short to reach 80 bits it keeps to the maximum and tells you how many bits you got.
- Numbers and symbols: the least and most of each. A password gets the numbers or symbols it needs even if you had ticked them off. A passphrase gets a number on the end and symbols on the end, and a separator that is a symbol counts as symbols too.
- Letters: upper and lower case together, uppercase only or lowercase only. In a passphrase that capitalises, upper cases or lower cases every word.
- Same character in a row: the most identical characters side by side. 1 means no doubles.
- No runs like abc or 123: three letters or digits in a row going up or down are thrown away.
- Nothing like a birth date: any result containing a date is thrown away and made again. It looks for day first, month first and year first, with or without separators, in 2 or 4 digit years, so 31/12/24, 12/31/24, 311224, 123124 and 20241231 are all caught.
- Start with a letter, for passwords. Passphrases always do.
- Never use these: characters the site refuses. Passphrase words containing them are skipped.

Rules that throw results away make the number of possible results a bit smaller. I measure how many survive and take that off the bit figure, so the figure still matches what you get. Rules that can never be met together, like a minimum longer than the maximum, give an error instead.

"Group into blocks" puts hyphens in every 3 to 6 characters, like `K7vP-m2Qx-R9ze`, which is easier to read out and type. The hyphens count towards the length limits but not towards the strength.

### Passphrases

Random words joined together, with a few choices around it:

- Separator: a hyphen, dot, underscore, space, nothing, or a random symbol each time.
- Capitals on every word, or not.
- A number on the end, 2 to 10 digits, or a random length between 2 and 10.
- Word length: shortest and longest from 3 to 10 letters, or "any". I default to exactly 6 letters. It looks tidy and it's easy to type.
- Random word length: when this is on, it picks a length first and then a word of that length, so you get a mix of short and long words. When it's off, it just picks from every word that fits.

There are three built in lists. Common words is the default and holds everyday words, ranked by how often people actually use them, so you get things you can recognise and type. The large list has far more words, including a lot of rare ones. The short list is a set of easy, familiar words. The default passphrase is 7 words of exactly 6 letters from the common list, which gives about 93 bits. "Filter profanities" is on by default and strips the rude words out. You can also type your own words to exclude. A word of 4 or more letters removes every word that contains it, so blocking "moon" also takes out "moonlight". Shorter ones only remove an exact match, otherwise blocking "ass" would take out "class". You can load your own list from a text file as well, one word per line, and it shows up in the dropdown under the file's name.

Defaults give you something like `Emoved-Tabors-Louser-Lessor-Crawly-Wrists-01`. The large list has a lot of uncommon words, which is where the strength comes from.

### How strength is worked out

I didn't want a guess from how a password looks, so it works out the number of bits from how the thing was actually made: how many possible characters or words, and how many were picked. The number is shown under every result along with a coloured bar, so you can see for yourself whether an option change helped or hurt.

It never changes what you asked for. If you pick 3 words or a short password, you get exactly that, and a line under the bar suggests how to improve it, like adding 4 more words or making it 14 characters. I picked 80 as the strong level because sites limit how fast you can guess, so 80 is far more than enough for a login. Anything where a leaked copy could be attacked at full speed offline, like a password manager master password or email, I'd want nearer 100 or more.

If a site rule changes the length or word count, the slider keeps what you chose and shows what was used, like "20 (using 16)", with a note saying why.

When the words are different lengths, the figure counts the worst case for each word, meaning the easiest word an attacker could try. It's a bit lower than the "average" number but it's honest. The same goes for a random number length on the end.

Passwords also get a second figure, "This exact password scores...". The main figure is about how the password was made, which is what decides how hard it is to guess. The second one runs the actual text through the same checker as "Check a password", so it can spot an unlucky result with a pattern in it. If it is well below the main figure, just generate another.

Under the strength there is also a line saying how long it would take to crack. That assumes someone with a stolen copy trying 10 billion guesses a second, which is a very well equipped attacker. It's to give you a feel for the numbers, not a promise.

One thing it can't do is promise a green bar on every website. Each site has its own idea of strong. What it does promise is every ticked type is in there, and the bit figure is worked out from how it was made.

### PIN

4 to 16 characters, numbers only or numbers and capital letters. A PIN is short by design, so the strength bar is honest and will usually say weak. Use one only where a site or a device insists on it.

### Hash

This isn't a hash of anything. It's a random token the same size as a hash output, 128 to 512 bits, as hex or base64. It's for API keys, secrets and salts, things you never type or remember.

It has nothing to do with storing passwords. That job belongs to the website, and the right tools for it are slow ones like Argon2id or bcrypt. This extension never stores or checks a password, so it has nothing to hash.

### The clipboard

Copying and clearing is the fiddly bit. To clear safely I keep a fingerprint of what I copied (not the password itself). When the timer ends I read the clipboard, and only wipe it if it still matches. If the browser won't let me read it, I wipe it anyway, because leaving a password sitting there is worse than losing something else you copied.

In Chrome the extension's background script is a service worker, which can't touch the clipboard, so a hidden page does it and holds the timer. In Firefox the background script can do it directly, but Firefox shuts idle background scripts down, which would lose the timer, so it pokes itself every 20 seconds until the clear has happened.

### What gets saved

Only your presets and settings, a word list if you load one (with its file name), and a note that you have seen the welcome message. Passwords are never stored, in any form. They sit in memory while you look at them and are dropped when the tab closes or the clear time passes. JavaScript can't truly wipe memory, so "cleared" means I let go of it and empty the screen.

The one exception is Save .txt and Save .csv. Those write plain unencrypted files to your downloads folder, but only if you press the button.

### Permissions

- `storage` for your settings.
- `clipboardWrite` to copy.
- `clipboardRead` to check the clipboard still holds the password before wiping it.
- `contextMenus` for the Firefox icon menu entry and "Insert a Passgen password here".
- `activeTab` and `scripting` so the insert option can fill the box you right clicked, and only when you pick it. It does not read the page.
- `offscreen` (Chrome only) for the hidden page that does the clipboard work.

No website access, no network, no tracking.

## Building it

You need Node.

    npm run build
    npm test

The build puts a ready-to-load copy for each browser in `extension/chrome` and `extension/firefox`.

- Chrome: go to `chrome://extensions`, turn on Developer mode, Load unpacked, and pick `extension/chrome`.
- Firefox: go to `about:debugging`, This Firefox, Load Temporary Add-on, and pick `extension/firefox/manifest.json`. For something permanent it has to be signed.

## Where things are

- `extension/src/gen.js` - all the generating and the strength maths. No browser bits in it, so the tests can run it in plain Node.
- `extension/src/settings.js` - the defaults, presets, and loading, saving and cleaning of stored settings.
- `extension/src/strength.js` - the estimate for the "Check a password" box.
- `extension/src/help.js` - the text behind the ? buttons.
- `extension/src/wordlist.js` - picks the word list and applies the profanity filter and your excluded words.
- `extension/src/clipboard.js` - copy, timer, and safe clearing.
- `extension/src/background.js` - icon click, right click menu, shortcuts, passes copies to the clipboard code.
- `extension/src/offscreen.html` and `extension/src/offscreen.js` - the hidden page Chrome needs.
- `extension/src/options.html`, `.css` and `.js` - the options page.
- `extension/manifests/` - one manifest per browser. The build copies the right one in.
- `tools/lists/` - the word lists, which the build turns into the files the extension loads.
- `tests/gen.test.js` - tests for the generating, strength numbers, site limits, word lengths, filtering, the password check and stored settings.

## Things I know about

- The logic is covered by tests, but the browser side (clipboard clearing, right click fill, the options page) needs trying in real Chrome and Firefox before you rely on it.
- The right click fill won't work on pages where the browser blocks extensions, such as the browser's own settings pages. The password is still on the clipboard.
- Sites that reject symbols, emoji or very long passwords need the options turned down. The extension can't know what a site will accept.
- Some sites count characters differently, mostly with emoji, so the length might not match what they report.
