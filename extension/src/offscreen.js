import {ext} from './ext.js';
import {copyAndSchedule} from './clipboard.js';

ext.runtime.onMessage.addListener((message, sender, respond) => {
  if (message?.target !== 'offscreen') return;
  copyAndSchedule(message.text, message.seconds).then(
    () => respond({ok: true}),
    err => respond({ok: false, error: String(err)})
  );
  return true;
});
