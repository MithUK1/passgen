import {ext} from './ext.js';
import {activeSettings, load} from './settings.js';
import {generate} from './gen.js';
import {activeWords} from './wordlist.js';
import {copyAndSchedule} from './clipboard.js';

const usesOffscreen = Boolean(ext.offscreen);
let offscreenPending = null;
let keepAlive = null;

async function ensureOffscreen() {
  const open = await ext.runtime.getContexts({contextTypes: ['OFFSCREEN_DOCUMENT']});
  if (open.length) return;
  offscreenPending ??= ext.offscreen
    .createDocument({
      url: 'offscreen.html',
      reasons: ['CLIPBOARD'],
      justification: 'Copy generated passwords and clear them again later'
    })
    .finally(() => {
      offscreenPending = null;
    });
  await offscreenPending;
}

// Firefox runs this script as an event page that is unloaded when idle, which
// would drop the clear timer, so a cheap API call keeps it alive until then.
async function copyHere(text, seconds) {
  clearInterval(keepAlive);
  await copyAndSchedule(text, seconds, () => clearInterval(keepAlive));
  if (seconds > 0) {
    keepAlive = setInterval(() => ext.runtime.getPlatformInfo(), 20000);
  }
}

async function copy(text, seconds) {
  if (!usesOffscreen) return copyHere(text, seconds);
  await ensureOffscreen();
  const reply = await ext.runtime.sendMessage({target: 'offscreen', text, seconds});
  if (!reply?.ok) throw new Error(reply?.error ?? 'No answer from the clipboard page.');
}

function flash(ok) {
  ext.action.setBadgeBackgroundColor({color: ok ? '#2e7d32' : '#c62828'});
  ext.action.setBadgeText({text: ok ? 'OK' : '!'});
  setTimeout(() => ext.action.setBadgeText({text: ''}), 1500);
}

async function newPassword() {
  const settings = activeSettings(await load());
  const {items} = generate({...settings, count: 1}, await activeWords(settings));
  return {text: items[0], seconds: settings.clearSeconds};
}

// Runs inside the page, so it cannot use anything from this file.
function fillField(text) {
  const field = document.activeElement;
  const isText = field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement;
  if (!isText) return false;
  const proto = field instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(proto, 'value').set.call(field, text);
  field.dispatchEvent(new Event('input', {bubbles: true}));
  field.dispatchEvent(new Event('change', {bubbles: true}));
  return true;
}

ext.action.onClicked.addListener(async () => {
  try {
    const {text, seconds} = await newPassword();
    await copy(text, seconds);
    flash(true);
  } catch (err) {
    console.error(err);
    flash(false);
  }
});

ext.runtime.onMessage.addListener((message, sender, respond) => {
  if (message?.type !== 'copy') return;
  copy(message.text, message.seconds).then(
    () => respond({ok: true}),
    err => respond({ok: false, error: String(err)})
  );
  return true;
});

ext.runtime.onInstalled.addListener(async details => {
  await ext.contextMenus.removeAll();
  // Chrome already lists Options on the icon menu, Firefox only offers Manage Extension.
  if (!usesOffscreen) {
    ext.contextMenus.create({id: 'open-options', title: 'Passgen options', contexts: ['action']});
  }
  ext.contextMenus.create({id: 'insert-password', title: 'Insert a Passgen password here', contexts: ['editable']});
  if (details.reason === 'install') ext.runtime.openOptionsPage();
});

ext.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId === 'open-options') {
    ext.runtime.openOptionsPage();
    return;
  }
  if (info.menuItemId !== 'insert-password') return;
  try {
    const {text, seconds} = await newPassword();
    await copy(text, seconds);
    const [{result}] = await ext.scripting.executeScript({
      target: {tabId: tab.id, frameIds: [info.frameId ?? 0]},
      func: fillField,
      args: [text]
    });
    flash(result);
  } catch (err) {
    console.error(err);
    flash(false);
  }
});

ext.commands.onCommand.addListener(command => {
  if (command === 'open-options') ext.runtime.openOptionsPage();
});
