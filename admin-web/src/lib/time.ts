// The server speaks UTC; the screen shows Tashkent time (UTC+5), the same for every admin.
const tashkentTime = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Tashkent", hour: "2-digit", minute: "2-digit", hour12: false });

export function formatTashkentTime(utc: string) {
  const date = new Date(utc);
  return Number.isNaN(date.getTime()) ? "" : tashkentTime.format(date);
}

const tashkentParts = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Tashkent", year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hourCycle: "h23" });

function parts(utc: string | null | undefined) {
  if (!utc) return null;
  const date = new Date(utc);
  if (Number.isNaN(date.getTime())) return null;
  return Object.fromEntries(tashkentParts.formatToParts(date).map((part) => [part.type, part.value]));
}

// "15.10.2026" in Tashkent; empty for a missing or broken date.
export function formatTashkentDate(utc: string | null | undefined) {
  const value = parts(utc);
  return value ? `${value.day}.${value.month}.${value.year}` : "";
}

// "15.10.2026 21:30" in Tashkent.
export function formatTashkentDateTime(utc: string | null | undefined) {
  const value = parts(utc);
  return value ? `${value.day}.${value.month}.${value.year} ${value.hour}:${value.minute}` : "";
}
