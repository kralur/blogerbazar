// Page changes around signing in and out are full loads on purpose: the login page (with the Telegram widget
// script) and the admin pages never share one document. Kept in one object so tests can watch them.
export const browser = {
  replace(url: string) {
    window.location.replace(url);
  },
  // Drops the signed Telegram data from the address bar and the history entry.
  cleanAddress(path: string) {
    window.history.replaceState(null, "", path);
  }
};
