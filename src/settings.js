import {ext} from './ext.js';

export const DEFAULTS = {
  mode: 'passphrase',
  password: {
    length: 20,
    lower: true,
    upper: true,
    digits: true,
    symbols: true,
    symbolSet: '!@#$%^&*-_=+?.',
    packs: {music: false, chess: false, emoji: false},
    avoidAmbiguous: false,
    group: 0
  },
  site: {
    minLength: 0,
    maxLength: 0,
    minDigits: 0,
    maxDigits: 0,
    minSymbols: 0,
    maxSymbols: 0,
    letterCase: 'any',
    startLetter: false,
    maxRepeat: 0,
    noSequences: false,
    noDates: false,
    exclude: ''
  },
  passphrase: {
    words: 7,
    separator: '-',
    capitalize: true,
    number: true,
    digits: 2,
    minLength: 6,
    maxLength: 6,
    randomLength: false,
    list: 'common',
    filterProfanity: true,
    blocked: ''
  },
  pin: {length: 6, type: 'digits'},
  hash: {bits: 256, format: 'hex'},
  count: 1,
  clearSeconds: 90
};

export const MAX_PRESETS = 20;

const MODES = ['password', 'passphrase', 'pin', 'hash'];
const CASES = ['any', 'both', 'upper', 'lower'];

function merge(base, stored) {
  if (typeof base === 'object' && base !== null) {
    const out = {};
    for (const key of Object.keys(base)) {
      out[key] = merge(base[key], stored?.[key]);
    }
    return out;
  }
  return typeof stored === typeof base ? stored : base;
}

function cleanSettings(stored) {
  const settings = merge(DEFAULTS, stored);
  if (!MODES.includes(settings.mode)) settings.mode = DEFAULTS.mode;
  if (!CASES.includes(settings.site.letterCase)) settings.site.letterCase = 'any';
  return settings;
}

function defaultState() {
  const id = crypto.randomUUID();
  return {active: id, presets: [{id, name: 'Default', settings: cleanSettings()}]};
}

export function sanitizeState(data) {
  const given = Array.isArray(data?.presets) ? data.presets.slice(0, MAX_PRESETS) : [];
  const seen = new Set();
  const presets = [];
  let active = null;
  for (const item of given) {
    const named = typeof item?.name === 'string' && item.name.trim();
    if (!named || typeof item.settings !== 'object' || item.settings === null) continue;
    const reuse = typeof item.id === 'string' && item.id && !seen.has(item.id);
    const id = reuse ? item.id : crypto.randomUUID();
    seen.add(id);
    if (item.id === data.active) active = id;
    presets.push({id, name: item.name.trim().slice(0, 30), settings: cleanSettings(item.settings)});
  }
  if (!presets.length) throw new Error('That file has no presets in it.');
  return {active: active ?? presets[0].id, presets};
}

export async function load() {
  const {state} = await ext.storage.local.get('state');
  try {
    return sanitizeState(state);
  } catch {
    return defaultState();
  }
}

export function save(state) {
  return ext.storage.local.set({state});
}

export function activeSettings(state) {
  return state.presets.find(preset => preset.id === state.active).settings;
}
