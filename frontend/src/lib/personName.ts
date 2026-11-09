// Names typed in Telegram often come in "fancy" Unicode letters (mathematical bold, script, small capitals) that the
// app's fonts do not have, so one name on a list is drawn in another face (owner, 2026-11-09). The backend folds a name
// to plain letters when it is saved; this does the same on the way in, so names saved earlier read the same at once.
const smallCapitals = "ᴀʙᴄᴅᴇꜰɢʜɪᴊᴋʟᴍɴᴏᴘǫʀꜱᴛᴜᴠᴡʏᴢ";
const plain = "abcdefghijklmnopqrstuvwyz";
const invisible = /[\p{Cf}\p{Cc}\p{Co}\p{Cn}]/gu;

export function plainName(value: string) {
  const folded = value.normalize("NFKC").replace(invisible, "");
  let result = "";
  for (const character of folded) {
    const index = smallCapitals.indexOf(character);
    result += index >= 0 ? plain[index] : character;
  }
  result = result.replace(/\s+/g, " ").trim();
  return result.length > 0 ? result : value.trim();
}

// Walks an API answer and folds every name field in place; other text (descriptions, messages) is left as typed.
const nameKey = /(^|[a-z])name$|^company$/i;

export function plainNames<T>(data: T): T {
  if (Array.isArray(data)) { data.forEach(plainNames); return data; }
  if (data && typeof data === "object") {
    const record = data as Record<string, unknown>;
    for (const key of Object.keys(record)) {
      const value = record[key];
      if (typeof value === "string") { if (nameKey.test(key)) record[key] = plainName(value); }
      else if (value && typeof value === "object") plainNames(value);
    }
  }
  return data;
}
