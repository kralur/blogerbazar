// Telegram opens the Mini App with its launch data in the URL fragment (#tgWebAppData=…&tgWebAppVersion=…). Its script
// has read and stored that data before our code runs, but the fragment stays, and the hash router took it for an
// unknown screen: returning users opened on a fallback Home without their role (a blogger saw the business Home)
// and with a Back button (pre-launch audit 2026-11-04). Anything that is not one of our routes ("#/…") becomes "#/".
export function normalizeLaunchHash(location: Pick<Location, "hash" | "pathname" | "search">, history: Pick<History, "replaceState" | "state">) {
  if (!location.hash || location.hash.startsWith("#/")) return false;
  history.replaceState(history.state, "", `${location.pathname}${location.search}#/`);
  return true;
}
