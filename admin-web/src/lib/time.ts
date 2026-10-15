// The server speaks UTC; the screen shows Tashkent time (UTC+5), the same for every admin.
const tashkentTime = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Tashkent", hour: "2-digit", minute: "2-digit", hour12: false });

export function formatTashkentTime(utc: string) {
  const date = new Date(utc);
  return Number.isNaN(date.getTime()) ? "" : tashkentTime.format(date);
}
