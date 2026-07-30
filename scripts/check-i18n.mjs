import { readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
const sourceRoot = join(root, "frontend", "src");
const dictionaryRoot = join(sourceRoot, "i18n");
const readDictionary = (language) => JSON.parse(readFileSync(join(dictionaryRoot, `${language}.json`), "utf8"));
const flatten = (value, prefix = "") => Object.entries(value).flatMap(([key, item]) =>
  item && typeof item === "object" && !Array.isArray(item) ? flatten(item, `${prefix}${key}.`) : [`${prefix}${key}`]);
const files = (directory) => readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
  const path = join(directory, entry.name);
  return entry.isDirectory() ? files(path) : /\.(ts|tsx)$/.test(entry.name) ? [path] : [];
});
const duplicateKeys = (path) => {
  const keys = [...readFileSync(path, "utf8").matchAll(/^\s*"([^"\\]+)"\s*:/gm)].map((match) => match[1]);
  return [...new Set(keys.filter((key, index) => keys.indexOf(key) !== index))];
};

const ru = new Set(flatten(readDictionary("ru")));
const uz = new Set(flatten(readDictionary("uz")));
const sourceFiles = files(sourceRoot).filter((path) => !relative(sourceRoot, path).replaceAll("\\", "/").startsWith("i18n/"));
const source = sourceFiles.map((path) => ({ path, text: readFileSync(path, "utf8") }));
const references = new Set();
const hardcoded = [];
for (const key of ru) {
  if (key.startsWith("taxonomy.category.")) references.add(key);
  if (key.startsWith("taxonomy.city.")) references.add(key);
  if (key.startsWith("admin.role.")) references.add(key);
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
const duplicates = ["ru", "uz"].flatMap((language) => duplicateKeys(join(dictionaryRoot, `${language}.json`)).map((key) => `${language}:${key}`));
const failures = [];
if (missingInUz.length || missingInRu.length) failures.push(`dictionary mismatch (uz: ${missingInUz.join(", ") || "none"}; ru: ${missingInRu.join(", ") || "none"})`);
if (duplicates.length) failures.push(`duplicate dictionary keys: ${duplicates.join(", ")}`);
if (missingReferences.length) failures.push(`missing translation keys: ${missingReferences.join(", ")}`);
if (hardcoded.length) failures.push(`hardcoded Cyrillic/Uzbek UI text outside i18n: ${hardcoded.join(", ")}`);
if (failures.length) throw new Error(`i18n audit failed\n- ${failures.join("\n- ")}`);
console.log(`i18n audit passed: ${ru.size} keys, ${references.size} references, no hardcoded RU/UZ text.`);
