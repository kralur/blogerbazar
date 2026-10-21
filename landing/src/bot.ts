// Every button opens the bot. The start label tells the admin's "Sources" report who came from the site and as whom
// (t.me/<bot>?start=<label>; labels are letters, digits and "_").
export type StartLabel = "site" | "site_blogger" | "site_business";

const configured = import.meta.env.VITE_BOT_USERNAME?.trim().replace(/^@/, "") ?? "";
export const botUsername = /^[A-Za-z0-9_]{5,32}$/.test(configured) ? configured : "";

export function botLink(start: StartLabel) {
  return botUsername ? `https://t.me/${botUsername}?start=${start}` : "https://t.me/";
}
