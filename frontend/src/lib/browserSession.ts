// D64 stage B: true once this page signed in through a browser session (outside Telegram). The screens then hide
// what only Telegram can do: switching roles, deleting the account, the admin panel, sharing the phone.
let signedInInBrowser = false;

export const isBrowserSession = () => signedInInBrowser;

export function setBrowserSession(value: boolean) {
  signedInInBrowser = value;
}
