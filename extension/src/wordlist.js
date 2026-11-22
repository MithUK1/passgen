import {ext} from './ext.js';
import eff from './words-eff.js';
import big from './words-big.js';
import bigClean from './words-clean.js';
import common from './words-common.js';
import commonAll from './words-common-all.js';
import profanity from './profanity.js';
import {MIN_CUSTOM_WORDS, parseTerms, withoutTerms} from './gen.js';

let last = {key: '', words: null};

export async function activeWords(settings) {
  const p = settings.passphrase;
  let base = null;
  let preCleaned = false;
  if (p.list === 'custom') {
    const {customWords} = await ext.storage.local.get('customWords');
    if (customWords?.length >= MIN_CUSTOM_WORDS) base = customWords;
  }
  if (!base && p.list === 'eff') base = eff;
  if (!base) {
    const [cleaned, everything] = p.list === 'big' ? [bigClean, big] : [common, commonAll];
    base = p.filterProfanity ? cleaned : everything;
    preCleaned = p.filterProfanity;
  }
  const terms = [...(p.filterProfanity && !preCleaned ? profanity : []), ...parseTerms(p.blocked)];
  const key = `${p.list}|${base.length}|${p.filterProfanity}|${terms.length}|${p.blocked}`;
  if (last.key !== key) last = {key, words: withoutTerms(base, terms)};
  return last.words;
}
