import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/client";
import { browser } from "./browser";

export type Loaded<T> = { status: "loading" } | { status: "ready"; data: T } | { status: "error" };

// Loads one report; a session that ended on the way goes to the sign-in page, an older answer never wins.
// refresh() reloads after a change without hiding what is on screen.
export function useLoad<T>(load: () => Promise<T>, key: string) {
  const [state, setState] = useState<Loaded<T>>({ status: "loading" });
  const latest = useRef(0);
  const run = useCallback((quiet = false) => {
    const ticket = ++latest.current;
    if (!quiet) setState({ status: "loading" });
    load()
      .then((data) => { if (ticket === latest.current) setState({ status: "ready", data }); })
      .catch((error: unknown) => {
        if (ticket !== latest.current) return;
        if (error instanceof ApiError && error.status === 401) browser.replace("/login?error=expired");
        else if (error instanceof ApiError && error.status === 403) browser.replace("/login?error=denied");
        else setState({ status: "error" });
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  useEffect(() => run(), [run]);
  const reload = useCallback(() => run(), [run]);
  const refresh = useCallback(() => run(true), [run]);
  return { state, reload, refresh };
}
