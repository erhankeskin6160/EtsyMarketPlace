import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

const I18N_ROOT = join(process.cwd(), 'public', 'i18n');
const BASE_LANG = 'en';
const TARGET_LANGS = ['tr'];

function flattenKeys(obj, prefix = '') {
  const result = new Map();
  for (const [key, value] of Object.entries(obj)) {
    const fullKey = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      for (const [k, v] of flattenKeys(value, fullKey)) {
        result.set(k, v);
      }
    } else {
      result.set(fullKey, String(value ?? ''));
    }
  }
  return result;
}

function extractPlaceholders(text) {
  const matches = text.match(/\{\{\s*([a-zA-Z0-9_.-]+)\s*\}\}/g) ?? [];
  return [...new Set(matches.map(m => m.replace(/[\{\}\s]/g, '')))].sort();
}

function collectScopeDirs(dir) {
  const dirs = [dir];
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      dirs.push(...collectScopeDirs(full));
    }
  }
  return dirs;
}

let errorCount = 0;
let checkedFiles = 0;

for (const scopeDir of collectScopeDirs(I18N_ROOT)) {
  const basePath = join(scopeDir, `${BASE_LANG}.json`);
  const baseJson = JSON.parse(readFileSync(basePath, 'utf8'));
  const baseMap = flattenKeys(baseJson);
  checkedFiles++;

  for (const lang of TARGET_LANGS) {
    const targetPath = join(scopeDir, `${lang}.json`);
    const relTarget = relative(process.cwd(), targetPath);
    let targetJson;
    try {
      targetJson = JSON.parse(readFileSync(targetPath, 'utf8'));
    } catch (err) {
      console.error(`[FAIL] Missing or invalid JSON: ${relTarget} (${err.message})`);
      errorCount++;
      continue;
    }

    const targetMap = flattenKeys(targetJson);
    checkedFiles++;

    for (const [key, baseVal] of baseMap) {
      if (!targetMap.has(key)) {
        console.error(`[FAIL] Missing key in ${relTarget}: "${key}"`);
        errorCount++;
        continue;
      }

      const targetVal = targetMap.get(key);
      if (targetVal.trim().length === 0) {
        console.error(`[FAIL] Empty translation in ${relTarget}: "${key}"`);
        errorCount++;
      }

      const baseParams = extractPlaceholders(baseVal);
      const targetParams = extractPlaceholders(targetVal);
      if (baseParams.join(',') !== targetParams.join(',')) {
        console.error(
          `[FAIL] Placeholder mismatch in ${relTarget} for "${key}": expected [${baseParams.join(', ')}], got [${targetParams.join(', ')}]`
        );
        errorCount++;
      }
    }

    for (const key of targetMap.keys()) {
      if (!baseMap.has(key)) {
        console.error(`[FAIL] Extra key in ${relTarget} not present in ${BASE_LANG}.json: "${key}"`);
        errorCount++;
      }
    }
  }
}

if (errorCount > 0) {
  console.error(`\n❌ i18n parity check failed with ${errorCount} issue(s).`);
  process.exit(1);
}

console.log(`✅ i18n parity check passed (${checkedFiles} files verified, 0 missing/mismatched keys).`);
