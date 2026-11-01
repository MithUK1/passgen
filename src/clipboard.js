let timer = null;
let copiedDigest = null;

async function sha256(text) {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
  return Array.from(new Uint8Array(digest), b => b.toString(16).padStart(2, '0')).join('');
}

function withTextarea(action) {
  const area = document.createElement('textarea');
  area.style.cssText = 'position:fixed;top:0;left:0;opacity:0';
  document.body.append(area);
  try {
    return action(area);
  } finally {
    area.remove();
  }
}

function write(text) {
  const done = withTextarea(area => {
    area.value = text;
    area.select();
    return document.execCommand('copy');
  });
  if (!done) throw new Error('The browser refused the clipboard write.');
}

function read() {
  return withTextarea(area => {
    area.focus();
    return document.execCommand('paste') ? area.value : '';
  });
}

async function clearIfUnchanged() {
  const expected = copiedDigest;
  timer = null;
  copiedDigest = null;
  const current = read();
  if (!current || (await sha256(current)) === expected) write(' ');
}

export async function copyAndSchedule(text, seconds, onCleared = () => {}) {
  write(text);
  clearTimeout(timer);
  copiedDigest = await sha256(text);
  if (seconds > 0) {
    timer = setTimeout(async () => {
      await clearIfUnchanged();
      onCleared();
    }, seconds * 1000);
  }
}
