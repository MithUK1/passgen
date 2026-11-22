export const HELP = {
  presets: [
    ['Preset', 'A saved set of options with a name, like "Wi-Fi" or "Strict site".'],
    ['What the icon copies', 'The preset selected here. Pick a different one and the toolbar icon copies from that one instead.'],
    ['Saving', 'Anything you change is saved into the selected preset straight away. The plus button saves the current options as a new preset, the pencil renames it, and the bin deletes it (click it twice).']
  ],
  strength: [
    ['Bits', 'A measure of how many guesses it would take. Every extra bit doubles the work. 80 and over is strong, 128 is the level used for encryption keys. Below 80 it suggests how to improve it, but always makes what you asked for.'],
    ['How it is worked out', 'From how the password was made, not from how it looks, so it is a real figure and not a guess.'],
    ['Crack time', 'How long it would take to try every possibility at 10 billion guesses a second. That is what someone with a stolen database and a fast cracking rig can manage. A website that limits login attempts is far harder to attack than that.']
  ],
  password: [
    ['Length', 'How many characters. Every extra one makes it much harder to guess.'],
    ['Symbols used', 'The symbols it is allowed to pick from. Take out any that a site refuses.'],
    ['Music, chess, emoji', 'Fun extra characters. Plenty of websites reject them, so leave them off unless you know the site accepts them.'],
    ['Avoid look-alikes', 'Leaves out I, l, 1, O and 0 so it is easier to read out or copy by hand.'],
    ['Group into blocks', 'Splits it up with hyphens, like Ab3d-Ef7g-Hk9m. Easier to read aloud or type on a phone or TV. The strength is the same, but the hyphens count towards a site\'s length limit.'],
  ],
  passphrase: [
    ['Words and separator', 'Random words joined together. They are easier to remember and type than a jumble of characters, and just as strong when there are enough of them.'],
    ['Word length', 'Shortest and longest word it may use. Set both the same for words of exactly that length, or choose Any.'],
    ['Random word length', 'Picks a length first and then a word of that length, so you get a mix of short and long words. Switched off, it picks from every word that fits.'],
    ['Capitalise words', 'Puts a capital letter on every word, for sites that want an uppercase letter.'],
    ['Number at the end', 'Adds 2 to 10 digits, or a random amount, for sites that insist on a number.'],
    ['Word list', 'Common words are everyday words you can recognise and type. The large list has far more words, including rare ones. Short, easy words is a small friendly set. You can also load your own.'],
    ['Filter profanities', 'Leaves rude words out.'],
    ['Words to exclude', 'Words you would rather not see, separated by commas. A word of 4 or more letters also removes every word containing it.']
  ],
  pin: [
    ['PIN', 'Short by design, so it will always rate as weak. Use one only where a site or device insists on it.'],
    ['Numbers and capital letters', 'Gives far more combinations than numbers alone for the same length.']
  ],
  hash: [
    ['Hash-style token', 'A random value the same size as a hash, for things like API keys, secrets and salts. It is not a hash of anything.'],
    ['Size and format', 'More bits is stronger. Hex uses 0-9 and a-f, Base64 is shorter, and URL-safe Base64 avoids characters that cause trouble in web addresses.']
  ],
  site: [
    ['Length', 'The shortest and longest the site allows. It uses fewer or more characters, or words, to fit, and tells you when it does. Hyphens from grouping count towards the length.'],
    ['Numbers and symbols', 'The least and most digits and symbols the site allows. A password gets the numbers or symbols it needs even if you ticked them off. A passphrase gets a number on the end and symbols on the end, and a symbol separator counts as symbols too.'],
    ['Letters', 'Upper and lower case means it has both. Uppercase only or lowercase only is for sites that insist on one. In a passphrase it capitalises, upper cases or lower cases every word.'],
    ['Same character in a row', 'The most identical characters allowed next to each other. 1 means no doubles at all, like aa or 11. Leave at 0 for no rule.'],
    ['No runs like abc or 123', 'Throws out any result with three letters or digits in a row going up or down, such as abc, 789 or cba.'],
    ['Nothing like a birth date', 'Throws out any result containing a date, such as 31/12/24, 12/31/24, 311224, 123124 or 20241231, in day first, month first or year first order, so it cannot be guessed from a birthday.'],
    ['Must start with a letter', 'For sites that refuse a password starting with a number or symbol. Passphrases always start with a letter.'],
    ['Never use these', 'Characters to leave out altogether, for sites that refuse certain ones. Words containing them are skipped too.'],
    ['Strength', 'Rules that throw results away shrink the number of possible results a little, and the bits shown already allow for that.']
  ],
  bulk: [
    ['How many', 'How many to make when you Copy or save a file, up to 100. The toolbar icon always copies one.'],
    ['Save files', 'Writes plain text or CSV files to your downloads folder. They are not encrypted, so delete them when you are done.']
  ],
  options: [
    ['Presets', 'Everything you change is saved into the selected preset, and the toolbar icon copies from it. Use the plus, pencil and bin to add, rename and delete.'],
    ['Clear clipboard after', 'Wipes the clipboard after this long, unless you have copied something else since. Never keeps it there until you copy something else.'],
    ['Reset to defaults', 'Puts the selected preset back to the standard options. It shows what it will set first and you click twice to confirm.']
  ]
};
