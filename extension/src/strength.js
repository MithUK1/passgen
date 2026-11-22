const COMMON_PASSWORDS = new Set([
  'password', 'password1', 'password123', '123456', '1234567', '12345678', '123456789', '1234567890',
  'qwerty', 'qwerty123', 'qwertyuiop', 'abc123', 'letmein', 'welcome', 'monkey', 'dragon', 'football',
  'iloveyou', 'admin', 'login', 'princess', 'sunshine', 'master', 'shadow', 'superman', 'trustno1',
  'baseball', 'passw0rd', 'starwars', 'whatever', 'freedom', 'michael', 'charlie', 'donald', 'hello',
  '111111', '000000', '121212', '654321', '666666', '696969', '123123', '123321', '1q2w3e4r', 'zaq12wsx'
]);

const RUNS = /[A-Za-z]+|\d+|[^A-Za-z\d]+/gu;

function poolSize(text) {
  let pool = 0;
  if (/[a-z]/.test(text)) pool += 26;
  if (/[A-Z]/.test(text)) pool += 26;
  if (/\d/.test(text)) pool += 10;
  if (/[ -/:-@[-`{-~]/.test(text)) pool += 33;
  if (/[^ -~]/.test(text)) pool += 150;
  return pool;
}

// Every estimate starts from "each character picked freely from the pool" and
// only goes lower where it can see a pattern: a word, a year, a repeat or a run.
function letterBits(run, wordSet, perChar) {
  const lower = run.toLowerCase();
  const perWord = Math.log2(wordSet.size);
  const best = new Array(lower.length + 1).fill(Infinity);
  best[0] = 0;
  for (let end = 1; end <= lower.length; end++) {
    best[end] = best[end - 1] + perChar;
    for (let start = Math.max(0, end - 10); start <= end - 4; start++) {
      if (wordSet.has(lower.slice(start, end))) {
        best[end] = Math.min(best[end], best[start] + perWord);
      }
    }
  }
  const capitalised = run === lower.charAt(0).toUpperCase() + lower.slice(1);
  const caseBits = run === lower ? 0 : capitalised ? 1 : run.length;
  const found = best[lower.length] + caseBits;
  return Math.min(found, run.length * perChar);
}

function digitBits(run, perChar) {
  const size = run.length;
  const free = size * perChar;
  if (/^(19|20)\d\d$/.test(run)) return Math.min(free, 7);
  if (size > 2 && new Set(run).size === 1) return Math.min(free, perChar + Math.log2(size));
  if (size > 2) {
    const step = run.charCodeAt(1) - run.charCodeAt(0);
    const steady = Math.abs(step) === 1 && Array.from(run).every((ch, i) => ch.charCodeAt(0) === run.charCodeAt(0) + i * step);
    if (steady) return Math.min(free, 5);
  }
  return free;
}

function symbolBits(run, perChar) {
  const chars = Array.from(run);
  const free = chars.length * perChar;
  if (chars.length > 2 && new Set(chars).size === 1) return Math.min(free, perChar + Math.log2(chars.length));
  return free;
}

export function estimatePassword(text, wordSet) {
  if (!text) return null;
  if (COMMON_PASSWORDS.has(text.toLowerCase())) {
    return {bits: 7, note: 'This is one of the most common passwords there is.'};
  }
  const perChar = Math.log2(poolSize(text));
  const random = Array.from(text).length * perChar;
  let bits = 0;
  for (const run of text.match(RUNS)) {
    if (/^[A-Za-z]+$/.test(run)) bits += letterBits(run, wordSet, perChar);
    else if (/^\d+$/.test(run)) bits += digitBits(run, perChar);
    else bits += symbolBits(run, perChar);
  }
  return {
    bits,
    note: bits < random - 8 ? 'It contains parts that are easy to guess, like everyday words, years or runs of numbers.' : ''
  };
}
