import {ext} from './ext.js';
import {DEFAULTS, MAX_PRESETS, load, save, sanitizeState} from './settings.js';
import {generate, strengthOf, crackTime, parseWordList, MIN_CUSTOM_WORDS} from './gen.js';
import {estimatePassword} from './strength.js';
import {HELP} from './help.js';
import {activeWords} from './wordlist.js';
import commonWords from './words-common.js';

const $ = id => document.getElementById(id);
const form = $('options');
const actionButtons = ['copy', 'save-txt', 'save-csv'].map($);
const listSelect = $('list-select');
const fileRow = $('file-row');

const presetSelect = $('preset-select');
const commonSet = new Set(commonWords);

let state;
let settings;
let customName = null;
let result = null;
let renderRun = 0;
let wipeTimer = null;
let countdown = null;
let renaming = false;
let deleteArmed = null;

const getPath = (obj, path) => path.split('.').reduce((o, key) => o[key], obj);

function setPath(obj, path, value) {
  const keys = path.split('.');
  const last = keys.pop();
  keys.reduce((o, key) => o[key], obj)[last] = value;
}

function readForm() {
  const next = structuredClone(settings);
  for (const el of form.elements) {
    if (!el.name) continue;
    let value = el.value;
    if (el.type === 'checkbox') value = el.checked;
    else if (el.type === 'range' || el.type === 'number' || 'number' in el.dataset) value = Number(el.value);
    setPath(next, el.name, value);
  }
  return next;
}

function syncOutputs() {
  for (const range of form.querySelectorAll('input[type="range"]')) {
    range.nextElementSibling.value = range.value;
  }
}

function syncListSelect() {
  listSelect.querySelector('option[value="custom"]')?.remove();
  if (customName) {
    const option = document.createElement('option');
    option.value = 'custom';
    option.textContent = customName;
    listSelect.insertBefore(option, listSelect.querySelector('option[value="load"]'));
  }
  listSelect.value = customName || settings.passphrase.list !== 'custom' ? settings.passphrase.list : 'common';
  fileRow.hidden = true;
}

const activePreset = () => state.presets.find(preset => preset.id === state.active);

function persist() {
  activePreset().settings = settings;
  return save(state);
}

function renderPresets() {
  presetSelect.replaceChildren(
    ...state.presets.map(preset => {
      const option = document.createElement('option');
      option.value = preset.id;
      option.textContent = preset.name;
      return option;
    })
  );
  presetSelect.value = state.active;
  $('preset-name-row').hidden = !renaming;
  $('preset-new').disabled = state.presets.length >= MAX_PRESETS;
  $('preset-delete').disabled = state.presets.length < 2;
  disarmDelete();
}

function disarmDelete() {
  deleteArmed = null;
  $('preset-delete').classList.remove('armed');
  $('preset-delete').title = 'Delete this preset';
}

function startNaming(kind) {
  renaming = kind;
  $('preset-name').value = kind === 'rename' ? activePreset().name : '';
  $('preset-name-row').hidden = false;
  $('preset-name').focus();
}

function endNaming() {
  renaming = false;
  $('preset-name-row').hidden = true;
}

function syncDisabled() {
  form.elements['passphrase.digits'].disabled = !settings.passphrase.number;
  form.elements['password.symbolSet'].disabled = !settings.password.symbols;
  form.elements['passphrase.randomLength'].disabled = settings.passphrase.minLength > 0 && settings.passphrase.minLength === settings.passphrase.maxLength;
}

function writeForm() {
  for (const el of form.elements) {
    if (!el.name) continue;
    const value = getPath(settings, el.name);
    if (el.type === 'checkbox') el.checked = value;
    else el.value = String(value);
  }
  syncOutputs();
  syncDisabled();
  syncListSelect();
  form.dataset.mode = settings.mode;
  for (const button of document.querySelectorAll('#modes button')) {
    button.setAttribute('aria-pressed', String(button.dataset.mode === settings.mode));
  }
}

function setStatus(text) {
  $('status').textContent = text;
}

function setActionsEnabled(enabled) {
  for (const button of actionButtons) button.disabled = !enabled;
}

function showMessage(text) {
  const note = document.createElement('div');
  note.className = 'empty';
  note.textContent = text;
  $('items').replaceChildren(note);
}

// Show what was used when a site rule changed the slider's value.
function showUsed(used) {
  syncOutputs();
  const name = settings.mode === 'passphrase' ? 'passphrase.words' : settings.mode === 'password' ? 'password.length' : null;
  const range = name && form.elements[name];
  if (range && used && used !== Number(range.value)) range.nextElementSibling.value = `${range.value} (using ${used})`;
}

// The figure above comes from how the password was made. This one scores the text itself.
function showActual() {
  const text = result.items.length === 1 && settings.mode === 'password' ? result.items[0] : null;
  const found = text && estimatePassword(text, commonSet);
  if (!found) {
    $('actual').textContent = '';
    return;
  }
  const scored = Math.min(found.bits, result.bits);
  const weak = scored < result.bits - 10;
  $('actual').textContent = `This exact password scores about ${Math.floor(scored)} bits in the checker.${weak ? ' It has a pattern that is easy to guess, so generate another.' : ''}`;
}

function show() {
  const {items, bits, adjusted, suggest, detail, short, used} = result;
  if (items.length === 1) {
    const single = document.createElement('div');
    single.className = 'single';
    single.textContent = items[0];
    $('items').replaceChildren(single);
  } else {
    const list = document.createElement('ol');
    for (const item of items) {
      const li = document.createElement('li');
      li.textContent = item;
      list.append(li);
    }
    $('items').replaceChildren(list);
  }
  const strength = strengthOf(bits);
  $('meter-fill').dataset.level = strength.level;
  $('meter-fill').style.width = `${Math.min(100, (bits / 80) * 100)}%`;
  $('strength').textContent = `${strength.label} - ${Math.floor(bits)} bits (${detail})`;
  showUsed(used);
  $('crack').textContent = `Could take ${crackTime(bits)} to crack at 10 billion guesses a second.`;
  showActual();
  const notes = [adjusted, suggest, short ? 'PINs are short by design. Use one only where a site insists on it.' : null];
  $('notice').textContent = notes.filter(Boolean).join(' ');
  setActionsEnabled(true);
}

function clearResult(message, notice = '') {
  result = null;
  showMessage(message);
  $('meter-fill').style.width = '0';
  $('strength').textContent = '';
  $('crack').textContent = '';
  $('actual').textContent = '';
  $('notice').textContent = notice;
  setActionsEnabled(false);
}

async function render() {
  const run = ++renderRun;
  clearTimeout(wipeTimer);
  let next;
  try {
    next = generate(settings, await activeWords(settings));
  } catch (err) {
    if (run === renderRun) clearResult('Nothing to show.', err.message);
    return;
  }
  if (run !== renderRun) return;
  result = next;
  show();
  if (settings.clearSeconds > 0) {
    wipeTimer = setTimeout(() => clearResult('Cleared. Press Generate for a new one.'), settings.clearSeconds * 1000);
  }
}

function startCountdown(seconds) {
  clearInterval(countdown);
  if (!seconds) {
    setStatus('Copied. It stays on the clipboard until you copy something else.');
    return;
  }
  let left = seconds;
  const tick = () => {
    setStatus(left > 0 ? `Copied. Clipboard clears in ${left}s.` : 'Clipboard clear timer finished.');
    if (left-- <= 0) clearInterval(countdown);
  };
  tick();
  countdown = setInterval(tick, 1000);
}

function download(name, type, text) {
  const url = URL.createObjectURL(new Blob([text], {type}));
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

const SEPARATOR_NAMES = {
  '-': 'a hyphen',
  '.': 'a dot',
  _: 'an underscore',
  ' ': 'a space',
  '': 'nothing',
  random: 'a random symbol'
};

function describeDefaults() {
  const p = DEFAULTS.passphrase;
  const size = p.minLength === p.maxLength ? `exactly ${p.minLength} letters` : `${p.minLength} to ${p.maxLength} letters`;
  const lines = [
    `This preset opens on ${DEFAULTS.mode}`,
    `Passphrase: ${p.words} words of ${size}, ${SEPARATOR_NAMES[p.separator]} between them, ${p.capitalize ? 'capitalised' : 'lower case'}, ${p.number ? `${p.digits} digit number on the end` : 'no number'}${p.filterProfanity ? ', profanities filtered' : ''}`,
    `Password: ${DEFAULTS.password.length} characters with letters, numbers and symbols`,
    `PIN: ${DEFAULTS.pin.length} digits`,
    `Hash: ${DEFAULTS.hash.bits} bit, ${DEFAULTS.hash.format}`,
    `${DEFAULTS.count} at a time, clipboard cleared after ${DEFAULTS.clearSeconds} seconds, no site rules`
  ];
  if (customName) lines.push('Your loaded word list stays loaded, but it will not be the one in use');
  return lines;
}

function endReset() {
  $('reset-details').hidden = true;
  $('reset-cancel').hidden = true;
  $('reset').textContent = 'Reset to defaults';
}

$('reset').addEventListener('click', async () => {
  if ($('reset-details').hidden) {
    $('reset-list').replaceChildren(
      ...describeDefaults().map(line => {
        const item = document.createElement('li');
        item.textContent = line;
        return item;
      })
    );
    $('reset-details').hidden = false;
    $('reset-cancel').hidden = false;
    $('reset').textContent = 'Click again to reset';
    return;
  }
  endReset();
  settings = structuredClone(DEFAULTS);
  await persist();
  writeForm();
  setStatus('Back to the defaults.');
  render();
});

$('reset-cancel').addEventListener('click', endReset);

$('generate').addEventListener('click', render);

$('copy').addEventListener('click', async () => {
  if (!result) return;
  try {
    const reply = await ext.runtime.sendMessage({
      type: 'copy',
      text: result.items.join('\n'),
      seconds: settings.clearSeconds
    });
    if (!reply?.ok) throw new Error(reply?.error ?? 'no answer from the background script');
    startCountdown(settings.clearSeconds);
  } catch (err) {
    setStatus(`Copy failed: ${err.message}`);
  }
});

$('save-txt').addEventListener('click', () => {
  if (result) download('passwords.txt', 'text/plain', `${result.items.join('\n')}\n`);
});

$('save-csv').addEventListener('click', () => {
  if (!result) return;
  const rows = result.items.map((item, i) => `${i + 1},"${item.replaceAll('"', '""')}"`);
  download('passwords.csv', 'text/csv', `﻿number,value\r\n${rows.join('\r\n')}\r\n`);
});

$('modes').addEventListener('click', async event => {
  const mode = event.target.dataset?.mode;
  if (!mode) return;
  settings.mode = mode;
  await persist();
  writeForm();
  render();
});

form.addEventListener('input', async event => {
  if (!event.target.name) return;
  settings = readForm();
  syncOutputs();
  syncDisabled();
  await persist();
  render();
});

listSelect.addEventListener('change', async () => {
  if (listSelect.value === 'load') {
    fileRow.hidden = false;
    return;
  }
  fileRow.hidden = true;
  settings.passphrase.list = listSelect.value;
  await persist();
  render();
});

$('custom-file').addEventListener('change', async event => {
  const file = event.target.files[0];
  if (!file) return;
  const words = parseWordList(await file.text());
  event.target.value = '';
  if (words.length < MIN_CUSTOM_WORDS) {
    setStatus(`Need at least ${MIN_CUSTOM_WORDS} different words, found ${words.length}.`);
    return;
  }
  customName = file.name;
  await ext.storage.local.set({customWords: words, customName});
  settings.passphrase.list = 'custom';
  await persist();
  writeForm();
  setStatus(`Loaded ${words.length.toLocaleString('en-GB')} words.`);
  render();
});

presetSelect.addEventListener('change', async () => {
  state.active = presetSelect.value;
  settings = activePreset().settings;
  await save(state);
  endNaming();
  renderPresets();
  writeForm();
  setStatus(`Using preset "${activePreset().name}". The icon copies from it.`);
  render();
});

$('preset-new').addEventListener('click', () => startNaming('new'));
$('preset-rename').addEventListener('click', () => startNaming('rename'));
$('preset-cancel').addEventListener('click', endNaming);

$('preset-save').addEventListener('click', async () => {
  const name = $('preset-name').value.trim().slice(0, 30);
  if (!name) return;
  if (renaming === 'rename') {
    activePreset().name = name;
  } else {
    const preset = {id: crypto.randomUUID(), name, settings: structuredClone(settings)};
    state.presets.push(preset);
    state.active = preset.id;
    settings = preset.settings;
  }
  await save(state);
  endNaming();
  renderPresets();
  setStatus(`Saved preset "${name}".`);
});

$('preset-name').addEventListener('keydown', event => {
  if (event.key === 'Enter') {
    event.preventDefault();
    $('preset-save').click();
  }
});

$('preset-delete').addEventListener('click', async () => {
  const button = $('preset-delete');
  if (deleteArmed !== state.active) {
    deleteArmed = state.active;
    button.classList.add('armed');
    button.title = 'Click again to delete';
    setStatus(`Click the bin again to delete "${activePreset().name}".`);
    return;
  }
  const name = activePreset().name;
  state.presets = state.presets.filter(preset => preset.id !== state.active);
  state.active = state.presets[0].id;
  settings = activePreset().settings;
  await save(state);
  renderPresets();
  writeForm();
  setStatus(`Deleted preset "${name}".`);
  render();
});

document.addEventListener('click', event => {
  const button = event.target.closest('.help');
  if (!button) return;
  const anchor = button.closest('legend, #strength-row, .row');
  const open = anchor.nextElementSibling;
  if (open?.classList.contains('help-text')) {
    open.remove();
    return;
  }
  const box = document.createElement('div');
  box.className = 'help-text';
  for (const [term, text] of HELP[button.dataset.help]) {
    const p = document.createElement('p');
    const strong = document.createElement('strong');
    strong.textContent = `${term}: `;
    p.append(strong, text);
    box.append(p);
  }
  anchor.after(box);
});

function checkPassword() {
  const text = $('check-input').value;
  const found = text ? estimatePassword(text, commonSet) : null;
  $('check-result').hidden = !found;
  if (!found) return;
  const strength = strengthOf(found.bits);
  $('check-fill').dataset.level = strength.level;
  $('check-fill').style.width = `${Math.min(100, (found.bits / 80) * 100)}%`;
  $('check-strength').textContent = `${strength.label} - about ${Math.floor(found.bits)} bits`;
  $('check-crack').textContent = `Could take ${crackTime(found.bits)} to crack at 10 billion guesses a second.`;
  $('check-note').textContent = found.note ?? '';
}

$('check-input').addEventListener('input', checkPassword);

$('check-toggle').addEventListener('click', () => {
  const input = $('check-input');
  input.type = input.type === 'password' ? 'text' : 'password';
});

$('export-settings').addEventListener('click', () => {
  const data = {app: 'passgen', version: 1, active: state.active, presets: state.presets};
  download('passgen-settings.json', 'application/json', `${JSON.stringify(data, null, 2)}\n`);
});

$('import-file').addEventListener('change', async event => {
  const file = event.target.files[0];
  event.target.value = '';
  if (!file) return;
  try {
    const data = JSON.parse(await file.text());
    if (data?.app !== 'passgen') throw new Error('That is not a Passgen settings file.');
    state = sanitizeState(data);
  } catch (err) {
    setStatus(`Import failed: ${err.message}`);
    return;
  }
  settings = activePreset().settings;
  await save(state);
  endNaming();
  renderPresets();
  writeForm();
  setStatus(`Imported ${state.presets.length} preset${state.presets.length === 1 ? '' : 's'}.`);
  render();
});

window.addEventListener('pagehide', () => {
  result = null;
  $('items').replaceChildren();
  $('check-input').value = '';
});

state = await load();
settings = activePreset().settings;
customName = (await ext.storage.local.get('customName')).customName ?? null;
renderPresets();
writeForm();
render();

$('version').textContent = `v${ext.runtime.getManifest().version}`;

$('welcome').hidden = Boolean((await ext.storage.local.get('welcomed')).welcomed);

$('welcome-close').addEventListener('click', async () => {
  $('welcome').hidden = true;
  await ext.storage.local.set({welcomed: true});
});
