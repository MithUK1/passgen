import {cpSync, readFileSync, rmSync, writeFileSync} from 'node:fs';
import {basename, dirname, join} from 'node:path';
import {fileURLToPath} from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

function readList(name, minimum) {
  const list = readFileSync(join(root, 'tools/lists', name), 'utf8').split(/\r?\n/).filter(Boolean);
  if (list.length < minimum || new Set(list).size !== list.length) {
    throw new Error(`${name} needs ${minimum}+ different entries, found ${list.length}`);
  }
  return list;
}

const lists = {
  'words-eff.js': readList('eff_large.txt', 7776),
  'words-big.js': readList('words_all.txt', 100000),
  'words-clean.js': readList('words_clean.txt', 100000),
  'words-common.js': readList('words_common.txt', 20000),
  'words-common-all.js': readList('words_common_all.txt', 20000),
  'profanity.js': readList('profanity.txt', 100)
};

const chromeOnly = new Set(['offscreen.html', 'offscreen.js']);

for (const browser of ['chrome', 'firefox']) {
  const out = join(root, 'extension', browser);
  rmSync(out, {recursive: true, force: true});
  cpSync(join(root, 'extension', 'src'), out, {
    recursive: true,
    filter: source => browser === 'chrome' || !chromeOnly.has(basename(source))
  });
  cpSync(join(root, 'extension', 'manifests', `${browser}.json`), join(out, 'manifest.json'));
  for (const [file, words] of Object.entries(lists)) {
    writeFileSync(join(out, file), `export default ${JSON.stringify(words)};\n`);
  }
  console.log(`built extension/${browser}`);
}
