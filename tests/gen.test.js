import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {generate, randomInt, parseWordList, parseTerms, withoutTerms, strengthOf, crackTime, looksLikeDate, hasSequence, MIN_BITS, PACKS} from '../extension/src/gen.js';
import {estimatePassword} from '../extension/src/strength.js';
import {DEFAULTS, sanitizeState} from '../extension/src/settings.js';

const readList = name =>
  readFileSync(new URL(`../tools/lists/${name}`, import.meta.url), 'utf8').split('\n').filter(Boolean);

const eff = readList('eff_large.txt');
const words = readList('words_clean.txt');
const common = readList('words_common.txt');

const make = patch => ({...structuredClone(DEFAULTS), mode: 'password', ...patch});
const withPassword = (patch, site = {}) =>
  make({password: {...DEFAULTS.password, ...patch}, site: {...DEFAULTS.site, ...site}});
const withPassphrase = (patch, extra = {}) =>
  make({mode: 'passphrase', passphrase: {...DEFAULTS.passphrase, ...patch}, ...extra});

test('bundled lists are large and free of duplicates', () => {
  assert.equal(eff.length, 7776);
  assert.equal(new Set(eff).size, 7776);
  assert.ok(words.length > 100000);
  assert.equal(new Set(words).size, words.length);
});

test('big list has no rude words', () => {
  const rude = ['shit', 'fuck', 'cunt', 'bitch', 'nigger', 'faggot', 'whore', 'penis', 'rape'];
  for (const bad of rude) {
    assert.ok(!words.some(w => w.includes(bad)), bad);
  }
});

test('randomInt covers its range evenly', () => {
  const counts = new Array(7).fill(0);
  for (let i = 0; i < 7000; i++) counts[randomInt(7)]++;
  for (const count of counts) assert.ok(count > 800 && count < 1200, `count ${count}`);
});

test('default password is 20 characters with every class and is strong', () => {
  const {items, bits} = generate(make({}), words);
  const [pw] = items;
  assert.equal(pw.length, 20);
  assert.match(pw, /[a-z]/);
  assert.match(pw, /[A-Z]/);
  assert.match(pw, /[0-9]/);
  assert.match(pw, /[!@#$%^&*\-_=+?.]/);
  assert.ok(bits >= MIN_BITS);
});

test('short lengths are used as asked, with a suggestion to improve', () => {
  const short = generate(withPassword({length: 6}), words);
  assert.equal(short.items[0].length, 6);
  assert.ok(short.bits < MIN_BITS);
  assert.match(short.suggest, /characters or more/);
  assert.equal(generate(withPassword({length: 20}), words).suggest, null);
});

test('music, chess and emoji packs are all represented', () => {
  const settings = withPassword({length: 60, packs: {music: true, chess: true, emoji: true}});
  for (let i = 0; i < 20; i++) {
    const chars = Array.from(generate(settings, words).items[0]);
    for (const pack of Object.values(PACKS)) {
      assert.ok(chars.some(ch => pack.includes(ch)));
    }
  }
});

test('look-alike characters are removed when asked', () => {
  const settings = withPassword({length: 200, avoidAmbiguous: true});
  for (let i = 0; i < 20; i++) {
    assert.doesNotMatch(generate(settings, words).items[0], /[Il1O0]/);
  }
});

test('no character type selected is an error', () => {
  const settings = withPassword({lower: false, upper: false, digits: false, symbols: false});
  assert.throws(() => generate(settings, words), /at least one/);
});

test('default passphrase is strong, capitalised and ends in two digits', () => {
  const settings = withPassphrase({separator: '.'});
  const {items, bits} = generate(settings, words);
  const parts = items[0].split('.');
  assert.equal(parts.length, DEFAULTS.passphrase.words + 1);
  for (const part of parts.slice(0, -1)) {
    assert.ok(words.includes(part.toLowerCase()));
    assert.match(part, /^[A-Z]/);
  }
  assert.match(parts.at(-1), /^\d\d$/);
  assert.ok(bits >= MIN_BITS);
});

test('passphrase word count is used as asked, with a suggestion to improve', () => {
  const {items, bits, suggest, adjusted} = generate(withPassphrase({words: 3, separator: '.'}), words);
  assert.equal(items[0].split('.').length, 4);
  assert.ok(bits < MIN_BITS);
  assert.match(suggest, /more word/);
  assert.equal(adjusted, null);
});

test('random separators add entropy', () => {
  const fixed = generate(withPassphrase({separator: '.'}), words).bits;
  const random = generate(withPassphrase({separator: 'random'}), words).bits;
  assert.ok(random > fixed);
});

test('PIN respects length and character type', () => {
  const digits = generate(make({mode: 'pin', pin: {length: 4, type: 'digits'}}), words);
  assert.match(digits.items[0], /^\d{4}$/);
  const mixed = generate(make({mode: 'pin', pin: {length: 12, type: 'alnum'}}), words);
  assert.match(mixed.items[0], /^[A-Z0-9]{12}$/);
});

test('hash tokens have the expected size and format', () => {
  const hex = generate(make({mode: 'hash', hash: {bits: 256, format: 'hex'}}), words);
  assert.match(hex.items[0], /^[0-9a-f]{64}$/);
  assert.equal(hex.bits, 256);
  const url = generate(make({mode: 'hash', hash: {bits: 128, format: 'base64url'}}), words);
  assert.match(url.items[0], /^[A-Za-z0-9_-]{22}$/);
});

test('batches are capped at 100 and all different', () => {
  const {items} = generate(make({count: 500}), words);
  assert.equal(items.length, 100);
  assert.equal(new Set(items).size, 100);
});

test('word list parsing drops dice numbers and duplicates', () => {
  assert.deepEqual(parseWordList('11111\tabacus\r\n11112 abdomen\nabacus\n\n'), ['abacus', 'abdomen']);
});

test('strength labels', () => {
  assert.equal(strengthOf(MIN_BITS).label, 'Strong');
  assert.equal(strengthOf(30).label, 'Very weak');
  assert.equal(strengthOf(200).label, 'Excellent');
});

test('exclusion terms are parsed and applied', () => {
  assert.deepEqual(parseTerms(' Foo, bar;baz\nqux '), ['foo', 'bar', 'baz', 'qux']);
  const list = ['class', 'assassin', 'ass', 'planet', 'Planetary', 'moon'];
  assert.deepEqual(withoutTerms(list, ['ass']), ['class', 'assassin', 'planet', 'Planetary', 'moon']);
  assert.deepEqual(withoutTerms(list, ['planet']), ['class', 'assassin', 'ass', 'moon']);
  assert.equal(withoutTerms(list, []), list);
});

test('profanity list catches rude words that the unfiltered list holds', () => {
  const all = readList('words_all.txt');
  const terms = readList('profanity.txt');
  assert.ok(all.length > words.length);
  assert.ok(withoutTerms(all, terms).length <= words.length + 5);
  assert.ok(!withoutTerms(all, terms).some(w => w.includes('fuck')));
});

test('word length limits are respected and swapped when reversed', () => {
  for (const [minLength, maxLength] of [[5, 5], [4, 6], [6, 4]]) {
    const settings = withPassphrase({minLength, maxLength, separator: '.', number: false, words: 12});
    const sizes = generate(settings, words).items[0].split('.').map(w => w.length);
    for (const size of sizes) {
      assert.ok(size >= Math.min(minLength, maxLength) && size <= Math.max(minLength, maxLength));
    }
  }
});

test('random word length mixes lengths, fixed pool does not change the range', () => {
  const settings = withPassphrase({minLength: 4, maxLength: 7, randomLength: true, separator: '.', number: false, words: 20});
  const sizes = new Set(generate(settings, words).items[0].split('.').map(w => w.length));
  assert.ok(sizes.size >= 3);
  const plain = generate({...settings, passphrase: {...settings.passphrase, randomLength: false}}, words);
  assert.ok(plain.bits > 0);
});

test('any length allows every word', () => {
  const settings = withPassphrase({minLength: 0, maxLength: 0, separator: '.', number: false});
  assert.ok(generate(settings, words).bits >= MIN_BITS);
});

test('an empty length range is an error', () => {
  assert.throws(() => generate(withPassphrase({minLength: 40, maxLength: 40}), words), /No words of that length/);
});

test('number length can be fixed or random between 2 and 10 digits', () => {
  const fixed = generate(withPassphrase({digits: 5, separator: '.'}), words).items[0];
  assert.match(fixed.split('.').pop(), /^\d{5}$/);
  const seen = new Set();
  for (let i = 0; i < 400; i++) {
    const tail = generate(withPassphrase({digits: 0, separator: '.'}), words).items[0].split('.').pop();
    assert.match(tail, /^\d{2,10}$/);
    seen.add(tail.length);
  }
  assert.ok(seen.size >= 7);
});

test('default passphrase on the common list is strong without being raised', () => {
  const {bits, adjusted, items} = generate(withPassphrase({separator: '.'}), common);
  assert.ok(bits >= MIN_BITS);
  assert.equal(adjusted, null);
  assert.equal(items[0].split('.').length, DEFAULTS.passphrase.words + 1);
});

test('common list is a clean subset of the big list', () => {
  const big = new Set(words);
  assert.ok(common.length > 20000);
  assert.ok(common.every(w => big.has(w)));
});

test('longest-allowed limit is respected, with grouping counted', () => {
  const plain = generate(withPassword({length: 40}, {maxLength: 16}), words);
  assert.ok(plain.items[0].length <= 16);
  const grouped = generate(withPassword({length: 40, group: 4, symbolSet: '!@#$'}, {maxLength: 19}), words);
  assert.ok(grouped.items[0].length <= 19);
  assert.match(grouped.items[0], /^[^-]{4}(-[^-]{1,4})+$/);
});

test('must start with a letter', () => {
  for (let i = 0; i < 100; i++) {
    assert.match(generate(withPassword({}, {startLetter: true}), words).items[0], /^[A-Za-z]/);
  }
});

test('excluded characters never appear', () => {
  for (let i = 0; i < 50; i++) {
    assert.doesNotMatch(generate(withPassword({}, {exclude: 'aeiou@#'}), words).items[0], /[aeiou@#]/);
  }
});

test('grouping puts hyphens between blocks', () => {
  const pw = generate(withPassword({length: 12, group: 4, symbols: false}), words).items[0];
  assert.match(pw, /^.{4}-.{4}-.{4}$/);
});

test('crack time phrases', () => {
  assert.equal(crackTime(10), 'less than a second');
  assert.match(crackTime(60), /years/);
  assert.equal(crackTime(300), 'longer than the age of the universe');
});

test('password checker spots common and wordy passwords', () => {
  const set = new Set(common);
  assert.equal(estimatePassword('password', set).bits, 7);
  assert.ok(estimatePassword('correcthorsebattery', set).bits < 60);
  const random = generate(withPassword({length: 20}), words).items[0];
  assert.ok(estimatePassword(random, set).bits > 90);
  assert.equal(estimatePassword('', set), null);
});

test('stored state is cleaned', () => {
  assert.throws(() => sanitizeState({presets: []}), /no presets/);
  const state = sanitizeState({
    active: 'b',
    presets: [
      {id: 'a', name: 'One', settings: {mode: 'nonsense', count: 'lots', pin: {length: 9}}},
      {id: 'b', name: ' Two ', settings: {}}
    ]
  });
  assert.equal(state.active, 'b');
  assert.equal(state.presets[1].name, 'Two');
  assert.equal(state.presets[0].settings.mode, DEFAULTS.mode);
  assert.equal(state.presets[0].settings.count, DEFAULTS.count);
  assert.equal(state.presets[0].settings.pin.length, 9);
  assert.equal(state.presets[0].settings.pin.type, 'digits');
});

test('passphrases are cut to fit a length limit and say so', () => {
  const settings = withPassphrase({separator: '-', number: true, digits: 2, words: 7});
  settings.site = {...DEFAULTS.site, maxLength: 24};
  for (let i = 0; i < 100; i++) {
    assert.ok(generate(settings, common).items[0].length <= 24);
  }
  const result = generate(settings, common);
  assert.match(result.adjusted, /Words changed from 7 to/);
  assert.match(result.suggest, /rules keep this at \d+ bits/);
});

test('passphrases can be asked for symbols, numbers and avoid characters', () => {
  const settings = withPassphrase({separator: ' ', number: false});
  settings.site = {...DEFAULTS.site, minSymbols: 2, minDigits: 3, exclude: 'e5'};
  const {items} = generate({...settings, count: 50}, common);
  for (const item of items) {
    assert.match(item, /[!@#$%^&*\-_=+?.]{2}$/);
    assert.match(item, /\d{3}[^\d]*$/);
    assert.doesNotMatch(item, /[eE5]/);
  }
  const dash = withPassphrase({separator: '-'});
  dash.site = {...DEFAULTS.site, exclude: '-'};
  assert.throws(() => generate(dash, common), /separator/);
  const tooMany = withPassphrase({separator: '-', words: 7});
  tooMany.site = {...DEFAULTS.site, maxSymbols: 2};
  assert.throws(() => generate(tooMany, common), /Choose Space or None/);
});

test('letter case rules shape passwords and passphrases', () => {
  for (const [rule, pattern] of [['upper', /^[^a-z]+$/], ['lower', /^[^A-Z]+$/]]) {
    assert.match(generate(withPassword({length: 40}, {letterCase: rule}), words).items[0], pattern);
    assert.match(generate(withPassphrase({}, {site: {...DEFAULTS.site, letterCase: rule}}), common).items[0], pattern);
  }
  const both = withPassword({length: 30, upper: false}, {letterCase: 'both'});
  for (let i = 0; i < 30; i++) {
    const pw = generate(both, words).items[0];
    assert.match(pw, /[a-z]/);
    assert.match(pw, /[A-Z]/);
  }
});

test('number and symbol counts are held for passwords', () => {
  const settings = withPassword({length: 14}, {minDigits: 3, maxDigits: 4, minSymbols: 2, maxSymbols: 2});
  for (let i = 0; i < 100; i++) {
    const pw = generate(settings, words).items[0];
    const digits = (pw.match(/\d/g) ?? []).length;
    const symbols = (pw.match(/[^A-Za-z\d]/g) ?? []).length;
    assert.ok(digits >= 3 && digits <= 4, pw);
    assert.equal(symbols, 2, pw);
  }
});

test('repeats and runs are kept out when asked', () => {
  const settings = withPassword({length: 40}, {maxRepeat: 1, noSequences: true});
  for (let i = 0; i < 100; i++) {
    const pw = generate(settings, words).items[0];
    assert.doesNotMatch(pw, /(.)\1/s);
    assert.ok(!hasSequence(pw), pw);
  }
  assert.ok(hasSequence('xabcx') && hasSequence('x321x') && hasSequence('CBA'));
  assert.ok(!hasSequence('abd') && !hasSequence('a1b'));
});

test('rules that throw results away lower the bits, and impossible ones say so', () => {
  const plain = generate(withPassword({length: 12}), words).bits;
  const tight = generate(withPassword({length: 12}, {minDigits: 3, minSymbols: 2}), words).bits;
  assert.ok(tight < plain);
  assert.throws(() => generate(withPassword({length: 8}, {minDigits: 6, minSymbols: 6, maxLength: 10}), words), /cannot all fit/);
  assert.throws(() => generate(withPassword({}, {minLength: 20, maxLength: 10}), words), /length is more/);
});

test('date of birth detection', () => {
  for (const text of ['31/12/24', '12/31/24', '1/1/2024', '31-12-2024', '2024-12-31', '311224', '123124', '20241231', '31122024', 'x1.2.99y']) {
    assert.ok(looksLikeDate(text), text);
  }
  for (const text of ['abc', 'Word-12-05', 'k7123456x', '99/99/99', '1357']) {
    assert.ok(!looksLikeDate(text), text);
  }
});

test('no-dates rule never lets a date through', () => {
  const settings = withPassphrase({digits: 8, separator: '-'});
  settings.site = {...DEFAULTS.site, noDates: true};
  for (let i = 0; i < 200; i++) {
    assert.ok(!looksLikeDate(generate(settings, common).items[0]));
  }
});

test('site minimum and maximum lengths are both met', () => {
  const settings = withPassword({length: 4}, {minLength: 14, maxLength: 16});
  for (let i = 0; i < 50; i++) {
    const size = generate(settings, words).items[0].length;
    assert.ok(size >= 14 && size <= 16, size);
  }
  const phrase = withPassphrase({words: 3, minLength: 6, maxLength: 6, separator: '-', number: false});
  phrase.site = {...DEFAULTS.site, minLength: 30};
  assert.ok(generate(phrase, common).items[0].length >= 30);
});
