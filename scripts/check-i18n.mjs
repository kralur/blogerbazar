import { readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
// Each app keeps its own RU/UZ dictionaries in src/i18n; the same rules apply to both.
const apps = [
  { name: "frontend", alwaysReferenced: ["taxonomy.category.", "taxonomy.city.", "admin.role."] },
  { name: "admin-web", alwaysReferenced: ["taxonomy.category.", "taxonomy.city."] }
];
// City and category names (shared/taxonomy) and the admin funnel texts (shared/funnel) are merged into both apps'
// dictionaries; an app never repeats a shared key.
const sharedPaths = (language) => ["taxonomy", "funnel"].map((name) => join(root, "shared", name, `${language}.json`));

const flatten = (value, prefix = "") => Object.entries(value).flatMap(([key, item]) =>
  item && typeof item === "object" && !Array.isArray(item) ? flatten(item, `${prefix}${key}.`) : [`${prefix}${key}`]);
const files = (directory) => readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
  const path = join(directory, entry.name);
  return entry.isDirectory() ? files(path) : /\.(ts|tsx)$/.test(entry.name) ? [path] : [];
});
// Every JSON key in file order, also when several share a line; JSON.parse would silently keep only the last.
const keysInOrder = (text) => {
  const keys = [];
  for (let index = 0; index < text.length; index++) {
    if (text[index] !== "\"") continue;
    let end = index + 1;
    while (text[end] !== "\"") end += text[end] === "\\" ? 2 : 1;
    let next = end + 1;
    while (/\s/.test(text[next] ?? "")) next++;
    if (text[next] === ":") keys.push(JSON.parse(text.slice(index, end + 1)));
    index = end;
  }
  return keys;
};
const duplicateKeys = (path) => {
  const keys = keysInOrder(readFileSync(path, "utf8"));
  return [...new Set(keys.filter((key, index) => keys.indexOf(key) !== index))];
};

function audit({ name, alwaysReferenced }) {
  const sourceRoot = join(root, name, "src");
  const dictionaryRoot = join(sourceRoot, "i18n");
  const dictionaryPath = (language) => join(dictionaryRoot, `${language}.json`);
  const readShared = (language) => Object.assign({}, ...sharedPaths(language).map((path) => JSON.parse(readFileSync(path, "utf8"))));
  const readDictionary = (language) => ({ ...readShared(language), ...JSON.parse(readFileSync(dictionaryPath(language), "utf8")) });
  const ru = new Set(flatten(readDictionary("ru")));
  const uz = new Set(flatten(readDictionary("uz")));
  const sourceFiles = files(sourceRoot).filter((path) => !relative(sourceRoot, path).replaceAll("\\", "/").startsWith("i18n/"));
  const source = sourceFiles.map((path) => ({ path, text: readFileSync(path, "utf8") }));
  const references = new Set();
  const hardcoded = [];
  for (const key of ru) {
    if (alwaysReferenced.some((prefix) => key.startsWith(prefix))) references.add(key);
  }

  for (const file of source) {
    for (const match of file.text.matchAll(/(?:\bt|\btranslate)\(\s*["']([^"']+)["']/g)) references.add(match[1]);
    for (const [lineIndex, line] of file.text.split(/\r?\n/).entries()) {
      if (/[А-Яа-яЁёЎўҚқҒғҲҳ]/.test(line)) hardcoded.push(`${relative(root, file.path)}:${lineIndex + 1}`);
    }
  }

  const missingInUz = [...ru].filter((key) => !uz.has(key));
  const missingInRu = [...uz].filter((key) => !ru.has(key));
  const missingReferences = [...references].filter((key) => !ru.has(key) || !uz.has(key));
  const duplicates = ["ru", "uz"].flatMap((language) => {
    const shared = sharedPaths(language).flatMap((path) => keysInOrder(readFileSync(path, "utf8")));
    return [
      ...duplicateKeys(dictionaryPath(language)).map((key) => `${language}:${key}`),
      ...[...new Set(shared.filter((key, index) => shared.indexOf(key) !== index))].map((key) => `shared ${language}:${key}`),
      ...keysInOrder(readFileSync(dictionaryPath(language), "utf8")).filter((key) => key.startsWith("taxonomy.") || shared.includes(key))
        .map((key) => `${language}:${key} (belongs in shared)`)
    ];
  });
  const failures = [];
  if (missingInUz.length || missingInRu.length) failures.push(`dictionary mismatch (uz: ${missingInUz.join(", ") || "none"}; ru: ${missingInRu.join(", ") || "none"})`);
  if (duplicates.length) failures.push(`duplicate dictionary keys: ${duplicates.join(", ")}`);
  if (missingReferences.length) failures.push(`missing translation keys: ${missingReferences.join(", ")}`);
  // Owner rule: user-facing texts do not use the em dash.
  const emDashKeys = ["ru", "uz"].flatMap((language) => Object.entries(readDictionary(language))
    .filter(([, value]) => typeof value === "string" && value.includes("\u2014")).map(([key]) => `${language}:${key}`));
  if (emDashKeys.length) failures.push(`em dash in dictionary texts: ${emDashKeys.join(", ")}`);
  // Owner rule: Uzbek writes o‘, g‘ and the tutuq belgisi with ‘ ’, never with ' ʻ ʼ or `.
  const apostropheKeys = Object.entries(readDictionary("uz"))
    .filter(([, value]) => typeof value === "string" && /\p{L}['ʻʼ`]\p{L}/u.test(value)).map(([key]) => `uz:${key}`);
  if (apostropheKeys.length) failures.push(`Uzbek apostrophes other than ‘ ’: ${apostropheKeys.join(", ")}`);
  if (hardcoded.length) failures.push(`hardcoded Cyrillic/Uzbek UI text outside i18n: ${hardcoded.join(", ")}`);
  return { name, keys: ru.size, references: references.size, failures };
}

const results = apps.map(audit);
const failed = results.filter((result) => result.failures.length);
if (failed.length) {
  throw new Error(`i18n audit failed\n${failed.map((result) => `[${result.name}]\n- ${result.failures.join("\n- ")}`).join("\n")}`);
}
console.log(`i18n audit passed: ${results.map((result) => `${result.name} ${result.keys} keys, ${result.references} references`).join("; ")}; no hardcoded RU/UZ text.`);
