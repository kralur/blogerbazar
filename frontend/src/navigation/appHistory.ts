// The pages this tab has walked through inside the app, so "Back" on the website returns to where the user came from
// (Search with its filters, a company page, a deal) instead of a fixed page. Going back to the previous hash pops it;
// any other move pushes. After a reload the list starts again and Back falls back to the page's fixed parent.
const visited: string[] = [];
let started = false;
let skipNext = false;

export function startAppHistory() {
  if (started) return;
  started = true;
  visited.push(window.location.hash);
  window.addEventListener("hashchange", () => {
    const hash = window.location.hash;
    if (skipNext) { skipNext = false; visited[visited.length - 1] = hash; return; }
    if (visited.length > 1 && visited[visited.length - 2] === hash) visited.pop();
    else if (visited[visited.length - 1] !== hash) visited.push(hash);
  });
}

// A replaced entry (replaceHistoryRoute) takes the place of the current one instead of adding a step.
export function noteHistoryReplace() {
  if (started) skipNext = true;
}

export const canGoBackInApp = () => visited.length > 1;
