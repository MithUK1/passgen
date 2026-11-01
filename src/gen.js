export const MIN_BITS = 80;
const MAX_COUNT = 100;
export const MIN_CUSTOM_WORDS = 1000;

const LOWER = 'abcdefghijklmnopqrstuvwxyz';
const UPPER = LOWER.toUpperCase();
const DIGITS = '0123456789';
const AMBIGUOUS = new Set('Il1O0');
const SEPARATORS = Array.from('-_.:;,+=');
const SYMBOLS = Array.from('!@#$%^&*-_=+?.');
const HASH_BITS = [128, 160, 256, 384, 512];

const range = (from, to) =>
  Array.from({length: to - from + 1}, (_, i) => String.fromCodePoint(from + i));

export const PACKS = {
  music: [...range(0x2669, 0x266f), ...Array.from('🎵🎶🎼🎷🎸🎹🎺🎻🥁')],
  chess: range(0x2654, 0x265f),
  emoji: [...range(0x1f600, 0x1f64f), ...range(0x1f400, 0x1f43e)]
};

export function randomInt(max) {
  const limit = Math.floor(0x100000000 / max) * max;
  const buf = new Uint32Array(1);
  do {
    crypto.getRandomValues(buf);
  } while (buf[0] >= limit);
  return buf[0] % max;
}

const pick = (list, rnd = randomInt) => list[rnd(list.length)];
const fastInt = n => Math.floor(Math.random() * n);

const clamp = (value, low, high) =>
  Math.min(high, Math.max(low, Math.round(Number(value)) || low));

export function strengthOf(bits) {
  if (bits >= 128) return {label: 'Excellent', level: 4};
  if (bits >= MIN_BITS) return {label: 'Strong', level: 3};
  if (bits >= 60) return {label: 'Fair', level: 2};
  if (bits >= 40) return {label: 'Weak', level: 1};
  return {label: 'Very weak', level: 0};
}

const SECONDS_PER_YEAR = 31557600;

export function crackTime(bits, rate = 1e10) {
  const seconds = 2 ** (bits - 1) / rate;
  if (seconds < 1) return 'less than a second';
  const years = seconds / SECONDS_PER_YEAR;
  if (years >= 1.38e10) return 'longer than the age of the universe';
  if (years >= 1e9) return `about ${Math.round(years / 1e9).toLocaleString('en-GB')} billion years`;
  if (years >= 1e6) return `about ${Math.round(years / 1e6).toLocaleString('en-GB')} million years`;
  if (years >= 1) {
    const whole = Math.round(years);
    return `about ${whole.toLocaleString('en-GB')} ${whole === 1 ? 'year' : 'years'}`;
  }
  for (const [size, name] of [[86400, 'day'], [3600, 'hour'], [60, 'minute'], [1, 'second']]) {
    if (seconds >= size) {
      const count = Math.round(seconds / size);
      return `about ${count} ${name}${count === 1 ? '' : 's'}`;
    }
  }
}

export function parseTerms(text) {
  return text.toLowerCase().split(/[\s,;]+/).filter(Boolean);
}

export function withoutTerms(words, terms) {
  if (!terms.length) return words;
  const exact = new Set(terms);
  const parts = terms.filter(term => term.length >= 4);
  return words.filter(word => {
    const lower = word.toLowerCase();
    return !exact.has(lower) && !parts.some(term => lower.includes(term));
  });
}

export function parseWordList(text) {
  const words = new Set();
  for (const line of text.split(/\r?\n/)) {
    const word = line.trim().split(/\s+/).pop();
    if (word) words.add(word);
  }
  return [...words];
}

const isLetter = ch => /^[A-Za-z]$/.test(ch);
const isDigit = ch => ch >= '0' && ch <= '9';
const isSymbol = ch => !isLetter(ch) && !isDigit(ch);
const isLetterClass = chars => chars.every(isLetter);
const twoToTen = Array.from({length: 9}, (_, i) => i + 2);

function characterClasses(p, site) {
  const raw = [];
  const caseRule = site.letterCase;
  if (caseRule === 'any' ? p.lower : caseRule !== 'upper') raw.push(Array.from(LOWER));
  if (caseRule === 'any' ? p.upper : caseRule !== 'lower') raw.push(Array.from(UPPER));
  if (p.digits || site.minDigits > 0) raw.push(Array.from(DIGITS));
  if (p.symbols || site.minSymbols > 0) raw.push(Array.from(p.symbolSet || SYMBOLS.join('')));
  for (const [name, chars] of Object.entries(PACKS)) {
    if (p.packs[name]) raw.push(chars);
  }
  const excluded = new Set(Array.from(site.exclude));
  const seen = new Set();
  const classes = [];
  for (const chars of raw) {
    const kept = [];
    for (const ch of chars) {
      if (seen.has(ch) || excluded.has(ch) || ch.trim() === '') continue;
      if (p.avoidAmbiguous && AMBIGUOUS.has(ch)) continue;
      seen.add(ch);
      kept.push(ch);
    }
    if (kept.length) classes.push(kept);
  }
  return classes;
}

// Entropy of a string drawn uniformly from the pool and accepted only when it
// holds at least one character of every class (inclusion-exclusion over classes),
// and starts with a letter when that is asked for.
function passwordBits(classes, length, startLetter) {
  const sizes = classes.map(c => c.length);
  const letters = classes.map(isLetterClass);
  const total = sizes.reduce((a, b) => a + b, 0);
  const letterTotal = sizes.reduce((sum, size, i) => sum + (letters[i] ? size : 0), 0);
  let accept = 0;
  for (let mask = 0; mask < 1 << sizes.length; mask++) {
    let removed = 0;
    let removedLetters = 0;
    let members = 0;
    sizes.forEach((size, i) => {
      if (!(mask & (1 << i))) return;
      removed += size;
      members++;
      if (letters[i]) removedLetters += size;
    });
    const rest = ((total - removed) / total) ** (startLetter ? length - 1 : length);
    const first = startLetter ? (letterTotal - removedLetters) / total : 1;
    accept += (members % 2 ? -1 : 1) * first * rest;
  }
  return length * Math.log2(total) + Math.log2(Math.max(accept, 1e-12));
}

function grouped(text, size) {
  if (!size) return text;
  const chars = Array.from(text);
  const blocks = [];
  for (let i = 0; i < chars.length; i += size) blocks.push(chars.slice(i, i + size).join(''));
  return blocks.join('-');
}

const charKind = ch => (isLetter(ch) ? 1 : isDigit(ch) ? 2 : 0);

export function hasSequence(text) {
  const chars = Array.from(text.toLowerCase());
  for (let i = 2; i < chars.length; i++) {
    const kind = charKind(chars[i]);
    if (!kind || charKind(chars[i - 1]) !== kind || charKind(chars[i - 2]) !== kind) continue;
    const [a, b, c] = [chars[i - 2], chars[i - 1], chars[i]].map(ch => ch.codePointAt(0));
    if (b - a === c - b && Math.abs(b - a) === 1) return true;
  }
  return false;
}

const dayMonth = (a, b) => (a >= 1 && a <= 31 && b >= 1 && b <= 12) || (a >= 1 && a <= 12 && b >= 1 && b <= 31);
const yearLike = text => text.length === 2 || (text.length === 4 && /^(19|20)/.test(text));

// Dates in day-first or month-first order, written 31/12/24 or 311224 or 12312024 and so on.
export function looksLikeDate(text) {
  for (const [, a, , b, c] of text.matchAll(/(\d{1,4})([/.\-])(\d{1,2})\2(\d{1,4})/g)) {
    if (a.length <= 2 && c.length >= 2 && yearLike(c) && dayMonth(+a, +b)) return true;
    if (a.length === 4 && c.length <= 2 && yearLike(a) && dayMonth(+b, +c)) return true;
  }
  for (const [run] of text.matchAll(/\d{6,}/g)) {
    for (let i = 0; i + 6 <= run.length; i++) {
      const [x, y, z] = [run.slice(i, i + 2), run.slice(i + 2, i + 4), run.slice(i + 4, i + 6)];
      if ((yearLike(z) && dayMonth(+x, +y)) || (yearLike(x) && dayMonth(+y, +z))) return true;
      if (i + 8 > run.length) continue;
      const first = run.slice(i, i + 4);
      const last = run.slice(i + 4, i + 8);
      if (yearLike(last) && dayMonth(+first.slice(0, 2), +first.slice(2))) return true;
      if (yearLike(first) && dayMonth(+last.slice(0, 2), +last.slice(2))) return true;
    }
  }
  return false;
}

function ruleCheck(site) {
  const repeat = site.maxRepeat > 0 ? new RegExp(`(.)\\1{${Math.floor(site.maxRepeat)}}`, 'su') : null;
  return text =>
    Boolean(repeat?.test(text)) || (site.noSequences && hasSequence(text)) || (site.noDates && looksLikeDate(text));
}

const hasTextRules = site => site.maxRepeat > 0 || site.noSequences || site.noDates;

// Rules that throw results away change how many results are possible, so the
// share that survives is measured once and remembered, which keeps the figure steady.
const acceptCache = new Map();

function acceptRate(key, trial, runs) {
  if (!acceptCache.has(key)) {
    let kept = 0;
    for (let i = 0; i < runs; i++) if (trial()) kept++;
    if (acceptCache.size > 200) acceptCache.clear();
    acceptCache.set(key, kept ? kept / runs : 0);
  }
  const rate = acceptCache.get(key);
  if (!rate) throw new Error('Those rules cannot all be met together. Loosen one of them.');
  return rate;
}

function between(value, least, most) {
  return value >= least && (!most || value <= most);
}

function checkRange(least, most, what) {
  if (most && least > most) throw new Error(`The least ${what} is more than the most.`);
}

function planPassword(p, site) {
  const classes = characterClasses(p, site);
  if (!classes.length) throw new Error('Select at least one character type.');
  const pool = classes.flat();
  if (site.startLetter && !classes.some(isLetterClass)) {
    throw new Error('Tick lowercase or uppercase letters to start with a letter.');
  }
  if (site.minDigits > 0 && !pool.some(isDigit)) throw new Error('Every digit is on the never-use list.');
  if (site.minSymbols > 0 && !pool.some(isSymbol)) throw new Error('Every symbol is on the never-use list.');
  checkRange(site.minDigits, site.maxDigits, 'numbers');
  checkRange(site.minSymbols, site.maxSymbols, 'symbols');
  checkRange(site.minLength, site.maxLength, 'length');

  const group = clamp(p.group, 0, 8);
  const shown = n => n + (group ? Math.floor((n - 1) / group) : 0);
  const maxLength = site.maxLength ? clamp(site.maxLength, 1, 128) : 0;
  let cap = 128;
  while (maxLength && cap > 1 && shown(cap) > maxLength) cap--;
  let floor = Math.max(classes.length, site.minDigits + site.minSymbols, 1);
  while (site.minLength && floor < 128 && shown(floor) < site.minLength) floor++;
  if (floor > cap) throw new Error('Those length and character rules cannot all fit together.');
  const requested = clamp(p.length, 4, 128);
  const length = Math.min(cap, Math.max(floor, requested));

  const starts = site.startLetter ? classes.filter(isLetterClass).flat() : pool;
  const sets = classes.map(c => new Set(c));
  const breaks = ruleCheck(site);
  const counted = site.minDigits > 0 || site.maxDigits > 0 || site.minSymbols > 0 || site.maxSymbols > 0;
  const attempt = rnd => Array.from({length}, (_, i) => pick(i === 0 ? starts : pool, rnd));
  const valid = chars => {
    if (!sets.every(set => chars.some(ch => set.has(ch)))) return false;
    if (counted) {
      const digits = chars.filter(isDigit).length;
      const symbols = chars.filter(isSymbol).length;
      if (!between(digits, site.minDigits, site.maxDigits) || !between(symbols, site.minSymbols, site.maxSymbols)) {
        return false;
      }
    }
    return !breaks(grouped(chars.join(''), group));
  };

  let bits;
  if (counted || hasTextRules(site)) {
    const proposals = (site.startLetter ? Math.log2(starts.length) : Math.log2(pool.length)) + (length - 1) * Math.log2(pool.length);
    const key = JSON.stringify([pool.join(''), length, group, site]);
    bits = proposals + Math.log2(acceptRate(key, () => valid(attempt(fastInt)), 20000));
  } else {
    bits = passwordBits(classes, length, site.startLetter);
  }

  const make = () => {
    for (let tries = 0; tries < 200000; tries++) {
      const chars = attempt(randomInt);
      if (valid(chars)) return grouped(chars.join(''), group);
    }
    throw new Error('Could not meet every rule at this length.');
  };
  return {
    make,
    bits,
    adjusted: length === requested ? null : `Length changed from ${requested} to ${length} to fit the site's rules.`,
    used: length,
    detail: `${pool.length} possible characters`,
    perUnit: Math.log2(pool.length),
    unit: 'characters',
    canGrow: length < cap
  };
}

function planPassphrase(p, site, words) {
  const shortest = clamp(p.minLength, 0, 64);
  const longest = clamp(p.maxLength, 0, 64);
  const [low, high] =
    shortest && longest && shortest > longest ? [longest, shortest] : [shortest, longest];
  const excluded = new Set(Array.from(site.exclude.toLowerCase()));
  const allowed = word => !excluded.size || !Array.from(word.toLowerCase()).some(ch => excluded.has(ch));
  const byLength = new Map();
  let total = 0;
  for (const word of words) {
    const size = word.length;
    if ((low && size < low) || (high && size > high) || !allowed(word)) continue;
    if (!byLength.has(size)) byLength.set(size, []);
    byLength.get(size).push(word);
    total++;
  }
  if (total < 2) throw new Error('No words of that length in this list. Try a wider range.');
  const lengths = [...byLength.keys()];
  const smallest = Math.min(...[...byLength.values()].map(list => list.length));
  const pool = p.randomLength ? null : [...byLength.values()].flat();
  const nextWord = rnd => (p.randomLength ? pick(byLength.get(pick(lengths, rnd)), rnd) : pick(pool, rnd));
  // Worst case per word, so the figure holds for the most likely word an attacker could try.
  const perWord = p.randomLength ? Math.log2(lengths.length * smallest) : Math.log2(total);
  const shortestWord = Math.min(...lengths);
  const longestWord = Math.max(...lengths);

  const styled = word => {
    if (site.letterCase === 'upper') return word.toUpperCase();
    if (site.letterCase === 'lower') return word.toLowerCase();
    return site.letterCase === 'both' || p.capitalize ? word.charAt(0).toUpperCase() + word.slice(1) : word;
  };

  checkRange(site.minDigits, site.maxDigits, 'numbers');
  checkRange(site.minSymbols, site.maxSymbols, 'symbols');
  checkRange(site.minLength, site.maxLength, 'length');
  const minDigits = clamp(site.minDigits, 0, 10);
  const maxDigits = site.maxDigits ? clamp(site.maxDigits, 1, 10) : 10;
  const wantsNumber = p.number || minDigits > 0;
  const fixed = Number(p.digits) === 0 ? 0 : clamp(p.digits, 2, 10);
  const digitPool = Array.from(DIGITS).filter(ch => !site.exclude.includes(ch));
  if (wantsNumber && digitPool.length < 2) throw new Error('Too many digits are on the never-use list for a number.');
  let numberLengths = wantsNumber ? (fixed ? [fixed] : twoToTen).filter(n => n >= minDigits && n <= maxDigits) : [];
  if (wantsNumber && !numberLengths.length) numberLengths = [Math.min(maxDigits, Math.max(minDigits, fixed || 2))];
  const digitBits = Math.log2(digitPool.length || 1);
  const numberBits = wantsNumber ? Math.log2(numberLengths.length) + Math.min(...numberLengths) * digitBits : 0;
  const makeNumber = rnd =>
    Array.from({length: pick(numberLengths, rnd)}, () => pick(digitPool, rnd)).join('');

  const random = p.separator === 'random';
  const separators = random ? SEPARATORS.filter(ch => !site.exclude.includes(ch)) : [p.separator];
  if (!separators.length) throw new Error('Every separator is on the never-use list.');
  if (!random && p.separator && site.exclude.includes(p.separator)) {
    throw new Error('The separator is on the never-use list. Pick another one.');
  }
  const gapBits = random ? Math.log2(separators.length) : 0;
  const separatorIsSymbol = random || (p.separator !== '' && p.separator !== ' ');
  const sepLength = random ? 1 : p.separator.length;
  const symbolPool = SYMBOLS.filter(ch => !site.exclude.includes(ch));
  const separator = rnd => (random ? pick(separators, rnd) : p.separator);

  const minLength = site.minLength;
  const maxLength = site.maxLength;
  const lay = n => {
    const parts = n + (wantsNumber ? 1 : 0);
    const symbols = separatorIsSymbol ? parts - 1 : 0;
    const extra = Math.max(0, site.minSymbols - symbols);
    if ((site.maxSymbols && symbols > site.maxSymbols) || (extra && !symbolPool.length)) return null;
    const fixedLength = (parts - 1) * sepLength + extra;
    const best = n * shortestWord + fixedLength + (wantsNumber ? Math.min(...numberLengths) : 0);
    const worst = n * longestWord + fixedLength + (wantsNumber ? Math.max(...numberLengths) : 0);
    const safe = (!maxLength || worst <= maxLength) && (!minLength || best >= minLength);
    const possible = (!maxLength || best <= maxLength) && (!minLength || worst >= minLength);
    return {n, parts, extra, safe, possible, fixedLength: fixedLength + (wantsNumber ? (Math.min(...numberLengths) + Math.max(...numberLengths)) / 2 : 0)};
  };
  const requested = clamp(p.words, 3, 20);
  if (separatorIsSymbol && site.maxSymbols && requested + (wantsNumber ? 1 : 0) - 1 > site.maxSymbols) {
    throw new Error('The separator is a symbol and adds more symbols than the site allows. Choose Space or None.');
  }
  const options = Array.from({length: 20}, (_, i) => lay(i + 1)).filter(Boolean);
  const nearest = list =>
    [...list].sort((a, b) => Math.abs(a.n - requested) - Math.abs(b.n - requested) || b.n - a.n)[0];
  // When the limits only fit some of the words, prefer the count whose usual length lands inside them.
  const typical = p.randomLength
    ? lengths.reduce((sum, size) => sum + size, 0) / lengths.length
    : [...byLength].reduce((sum, [size, list]) => sum + size * list.length, 0) / total;
  const miss = o => {
    const usual = o.n * typical + o.fixedLength;
    return Math.max(0, minLength - usual, usual - (maxLength || Infinity));
  };
  const closest = list =>
    [...list].sort((a, b) => miss(a) - miss(b) || Math.abs(a.n - requested) - Math.abs(b.n - requested) || b.n - a.n)[0];
  const layout = nearest(options.filter(o => o.safe)) ?? closest(options.filter(o => o.possible));
  if (!layout) throw new Error('Those rules cannot all be met with these words and separator.');
  const count = layout.n;
  const next = lay(count + 1);

  const breaks = ruleCheck(site);
  const okText = text =>
    (!minLength || text.length >= minLength) && (!maxLength || text.length <= maxLength) && !breaks(text);
  const build = rnd => {
    const parts = Array.from({length: count}, () => styled(nextWord(rnd)));
    if (wantsNumber) parts.push(makeNumber(rnd));
    let text = parts.reduce((out, part, i) => (i ? out + separator(rnd) + part : part), '');
    for (let i = 0; i < layout.extra; i++) text += pick(symbolPool, rnd);
    return text;
  };

  let bits =
    count * perWord + (layout.parts - 1) * gapBits + numberBits + layout.extra * Math.log2(symbolPool.length || 1);
  if (!layout.safe || hasTextRules(site)) {
    const key = JSON.stringify([total, lengths, p, site, count]);
    bits += Math.log2(acceptRate(key, () => okText(build(fastInt)), 4000));
  }
  const make = () => {
    for (let tries = 0; tries < 20000; tries++) {
      const text = build(randomInt);
      if (okText(text)) return text;
    }
    throw new Error('Could not meet every rule with these settings.');
  };
  return {
    make,
    bits,
    adjusted: count === requested ? null : `Words changed from ${requested} to ${count} to fit the site's rules.`,
    used: count,
    detail: `${count} words`,
    perUnit: perWord + gapBits,
    unit: 'words',
    canGrow: Boolean(next?.safe) && count < 20
  };
}

function planPin(p) {
  const length = clamp(p.length, 4, 16);
  const pool = Array.from(p.type === 'alnum' ? UPPER + DIGITS : DIGITS);
  return {
    make: () => Array.from({length}, () => pick(pool)).join(''),
    bits: length * Math.log2(pool.length),
    adjusted: null,
    detail: `${pool.length} possible characters`
  };
}

function planHash(p) {
  const bits = HASH_BITS.includes(Number(p.bits)) ? Number(p.bits) : 256;
  const make = () => {
    const bytes = crypto.getRandomValues(new Uint8Array(bits / 8));
    if (p.format === 'hex') {
      return Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
    }
    const base64 = btoa(String.fromCharCode(...bytes));
    return p.format === 'base64url'
      ? base64.replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '')
      : base64;
  };
  return {make, bits, adjusted: null, detail: `${bits} random bits`};
}

export function generate(settings, words) {
  const count = clamp(settings.count, 1, MAX_COUNT);
  let plan;
  switch (settings.mode) {
    case 'password':
      plan = planPassword(settings.password, settings.site);
      break;
    case 'passphrase':
      plan = planPassphrase(settings.passphrase, settings.site, words);
      break;
    case 'pin':
      plan = planPin(settings.pin);
      break;
    case 'hash':
      plan = planHash(settings.hash);
      break;
    default:
      throw new Error(`Unknown mode: ${settings.mode}`);
  }
  let suggest = null;
  if (plan.perUnit && plan.bits < MIN_BITS) {
    suggest = plan.canGrow
      ? plan.unit === 'words'
        ? `Add ${Math.ceil((MIN_BITS - plan.bits) / plan.perUnit)} more words to reach the strong level.`
        : `Make it ${plan.used + Math.ceil((MIN_BITS - plan.bits) / plan.perUnit)} characters or more to reach the strong level.`
      : `The site's rules keep this at ${Math.floor(plan.bits)} bits.`;
  }
  return {
    items: Array.from({length: count}, () => plan.make()),
    bits: plan.bits,
    adjusted: plan.adjusted,
    suggest,
    detail: plan.detail,
    used: plan.used,
    short: settings.mode === 'pin'
  };
}
