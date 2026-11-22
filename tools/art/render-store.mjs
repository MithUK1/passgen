// Rebuilds the store art in store/: icon, promo tile, promo marquee and screenshots.
// Needs Playwright with Chromium (not a project dependency): run `node tools/art/render-store.mjs`
// after `npm run build`, from a folder where `require('playwright')` resolves (set NODE_PATH).
import {createRequire} from 'node:module';
import {readFileSync, writeFileSync, mkdirSync, rmSync} from 'node:fs';
import {join, dirname, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

const require = createRequire(import.meta.url);
const {chromium} = require('playwright');
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const store = join(root, 'store');
const shots = join(store, 'screenshots');
mkdirSync(shots, {recursive: true});

const iconSvg = readFileSync(join(root, 'tools/art/icon.svg'), 'utf8');
const inlineIcon = iconSvg.replace(/<!--.*?-->/, '');
writeFileSync(join(store, 'icon.svg'), iconSvg);

const browser = await chromium.launch();

async function render(html, width, height, file, scale = 1) {
  const ctx = await browser.newContext({viewport: {width, height}, deviceScaleFactor: scale});
  const page = await ctx.newPage();
  await page.setContent(html);
  await page.waitForTimeout(300);
  await page.screenshot({path: file});
  await ctx.close();
}

// Icon, tile and marquee come from the HTML sources next to this script.
await render(`<body style="margin:0;background:transparent">${inlineIcon.replace('width="128" height="128"', 'width="512" height="512"')}</body>`, 512, 512, join(store, 'icon-512.png'));
for (const [src, w, h, out] of [['tile.html', 440, 280, 'promo-tile-440x280.png'], ['banner.html', 1400, 560, 'promo-marquee-1400x560.png']]) {
  const html = readFileSync(join(root, 'tools/art', src), 'utf8').replace('<!--ICON-->', inlineIcon);
  await render(html, w, h, join(store, out));
}

// Screenshots: the real options page, loaded as the unpacked extension, set into a card.
const ext = join(root, 'extension/chrome');
const ctx = await chromium.launchPersistentContext('/tmp/passgen-art-profile', {
  headless: false,
  args: ['--headless=new', `--disable-extensions-except=${ext}`, `--load-extension=${ext}`],
  viewport: {width: 1280, height: 1300}
});
let [worker] = ctx.serviceWorkers();
if (!worker) worker = await ctx.waitForEvent('serviceworker');
const id = worker.url().split('/')[2];
const page = await ctx.newPage();
await page.goto(`chrome-extension://${id}/options.html`);
await page.evaluate(() => chrome.storage.local.set({welcomed: true}));
await page.reload();
await page.waitForTimeout(800);

const box = async selector => (await page.locator(selector).first().boundingBox());
const clip = async (name, selectors, pad = 12) => {
  const boxes = [];
  for (const s of selectors) boxes.push(await box(s));
  const x = Math.min(...boxes.map(b => b.x)) - pad, y = Math.min(...boxes.map(b => b.y)) - pad;
  const x2 = Math.max(...boxes.map(b => b.x + b.width)) + pad, y2 = Math.max(...boxes.map(b => b.y + b.height)) + pad;
  const file = `/tmp/passgen-crop-${name}.png`;
  await page.screenshot({path: file, clip: {x, y, width: x2 - x, height: y2 - y}});
  return {file, w: x2 - x, h: y2 - y};
};

const frame = (crop, title, text, out) => {
  const data = readFileSync(crop.file).toString('base64');
  const cardW = 520, imgW = cardW - 40, imgH = Math.round(crop.h * imgW / crop.w);
  const html = `<!doctype html><meta charset="utf-8"><style>
*{box-sizing:border-box;margin:0}
body{width:1280px;height:800px;overflow:hidden;font-family:Inter,system-ui,sans-serif;color:#fff;position:relative;
 background:radial-gradient(900px 700px at 85% 15%,#2c4fc9 0%,rgba(44,79,201,0) 60%),radial-gradient(700px 500px at 0% 100%,#1a3a9a 0%,rgba(26,58,154,0) 60%),linear-gradient(135deg,#0d1636,#122255)}
body:before{content:"";position:absolute;inset:0;background-image:radial-gradient(rgba(255,255,255,.07) 1.2px,transparent 1.2px);background-size:28px 28px}
.left{position:absolute;left:90px;top:0;bottom:0;width:470px;display:flex;flex-direction:column;justify-content:center}
h2{font-size:64px;font-weight:800;letter-spacing:-1.8px;line-height:1.05}
p{margin-top:26px;font-size:28px;line-height:1.35;font-weight:500;color:#d5e0ff}
.card{position:absolute;right:90px;top:50%;transform:translateY(-50%);width:${cardW}px;background:#f4f5f7;border-radius:22px;padding:20px;box-shadow:0 40px 80px rgba(3,10,40,.55),0 0 0 1px rgba(255,255,255,.4)}
.card img{display:block;width:${imgW}px;height:${imgH}px;border-radius:10px}
</style><div class="left"><h2>${title}</h2><p>${text}</p></div><div class="card"><img src="data:image/png;base64,${data}"></div>`;
  return render(html, 1280, 800, join(shots, out));
};

// 1. One click: a passphrase result with its options.
await page.locator('#modes button[data-mode="passphrase"]').click();
await page.waitForTimeout(500);
await frame(await clip('one', ['#result', 'fieldset[data-panel="passphrase"]']),
  'One click, one strong password', 'Click the toolbar icon and a new password is on your clipboard. It clears itself afterwards.', 'shot1-one-click.png');

// 2. Fussy sites: rules filled in, password made to fit.
await page.locator('#modes button[data-mode="password"]').click();
const set = async (name, value) => page.locator(`[name="${name}"]`).fill(String(value));
await set('site.minLength', 14); await set('site.maxLength', 16);
await set('site.minDigits', 2); await set('site.minSymbols', 1);
await page.locator('[name="site.noSequences"]').check();
await page.waitForTimeout(600);
await frame(await clip('site', ['fieldset[data-panel="site"]']),
  'Made to fit fussy sites', 'Length limits, numbers and symbols, no runs, nothing like a birth date. Presets keep each set of rules.', 'shot2-site-rules.png');

// 3. Checker.
await page.locator('#checker summary').click();
await page.locator('#check-input').fill('Summer2024!');
await page.locator('#check-toggle').click();
await page.waitForTimeout(500);
await frame(await clip('check', ['#checker']),
  'Check one you already use', 'See a rough strength for any password. It is checked on your computer only and never saved.', 'shot3-checker.png');

// 4. Tokens and PINs.
await page.locator('#modes button[data-mode="hash"]').click();
await page.waitForTimeout(500);
await frame(await clip('hash', ['#result', 'fieldset[data-panel="hash"]']),
  'Random tokens too', 'Hex or Base64 values from 128 to 512 bits for keys, secrets and salts. PINs and passphrases as well.', 'shot4-tokens.png');

await ctx.close();
await browser.close();
rmSync('/tmp/passgen-art-profile', {recursive: true, force: true});
console.log('store art written');
