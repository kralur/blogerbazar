// D64 stage B: true once this page signed in through a browser session (outside Telegram). The screens then hide
// what only Telegram can do: switching roles, deleting the account, the admin panel, sharing the phone.
let signedInInBrowser = false;
// The account's Telegram @username as the server keeps it: a browser has no Telegram launch data to read it from.
let browserUsername: string | null = null;

export const isBrowserSession = () => signedInInBrowser;
export const browserSessionUsername = () => (signedInInBrowser ? browserUsername : null);

export function setBrowserSession(value: boolean, username?: string | null) {
  signedInInBrowser = value;
  browserUsername = value ? username ?? null : null;
}
