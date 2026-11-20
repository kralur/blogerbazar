// Uzbek Latin alphabet (law of 21 October 2026): Oʻ, Gʻ, Sh, Ch are written Ö, Ğ, Ş, Ç. "old" is the 1995 alphabet
// the texts are written in; "new" is derived from it, so one dictionary serves both and the user chooses.
export type UzbekScript = "new" | "old";

// Latin words that keep their spelling (brands, products).
const keepWords = new Set(["Chrome", "Shorts", "Twitch", "ChatGPT", "Push", "Pitch"]);
const apostrophes = "ʻ‘'`";

function convertWord(word: string) {
  if (keepWords.has(word)) return word;
  return word
    .replace(new RegExp(`([oOgG])[${apostrophes}]`, "g"), (_, letter: string) => ({ o: "ö", O: "Ö", g: "ğ", G: "Ğ" })[letter as "o"])
    .replace(/SH/g, "Ş").replace(/Sh/g, "Ş").replace(/sh/g, "ş")
    .replace(/CH/g, "Ç").replace(/Ch/g, "Ç").replace(/ch/g, "ç");
}

// Placeholders ({name}), links and @handles stay as they are; everything else is converted word by word.
export function toNewUzbekLatin(text: string) {
  return text.split(/(\{\w+\}|https?:\/\/\S+|@[\w.]+)/).map((part, index) => index % 2 === 1
    ? part
    : part.replace(new RegExp(`[A-Za-z${apostrophes}]+`, "g"), convertWord)).join("");
}

export function toNewUzbekDictionary(dictionary: Record<string, string>) {
  return Object.fromEntries(Object.entries(dictionary).map(([key, value]) => [key, toNewUzbekLatin(value)]));
}
