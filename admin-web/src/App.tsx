import { useCallback, useEffect, useState } from "react";
import { ApiError } from "./api/client";
import { endAdminWebSession, getAdminWebSession, type AdminWebSession } from "./api/session";
import { callbackPath } from "./auth/telegramCallback";
import { Button, Spinner } from "./components/ui";
import { useI18n } from "./i18n";
import { Shell } from "./layout/Shell";
import { browser } from "./lib/browser";
import { AuthCallbackPage } from "./pages/AuthCallbackPage";
import { AnalyticsPage } from "./pages/AnalyticsPage";
import { FunnelPage } from "./pages/FunnelPage";
import { NotFoundPage, OverviewPage } from "./pages/OverviewPage";

type SessionState = { status: "loading" } | { status: "ready"; session: AdminWebSession } | { status: "error" };

function usePath() {
  const [path, setPath] = useState(() => window.location.pathname);
  useEffect(() => {
    const follow = () => setPath(window.location.pathname);
    window.addEventListener("popstate", follow);
    return () => window.removeEventListener("popstate", follow);
  }, []);
  const navigate = useCallback((next: string) => {
    window.history.pushState(null, "", next);
    setPath(next);
  }, []);
  return { path, navigate };
}

export function App() {
  const { path, navigate } = usePath();
  if (path === callbackPath) return <AuthCallbackPage />;
  return <AdminApp path={path} navigate={navigate} />;
}

// Every admin page needs a live web session; without one the browser goes to the sign-in page.
function AdminApp({ path, navigate }: { path: string; navigate: (path: string) => void }) {
  const { t } = useI18n();
  const [state, setState] = useState<SessionState>({ status: "loading" });
  const [signingOut, setSigningOut] = useState(false);

  const load = useCallback(() => {
    setState({ status: "loading" });
    getAdminWebSession()
      .then((session) => setState({ status: "ready", session }))
      .catch((error: unknown) => {
        if (error instanceof ApiError && error.status === 403) browser.replace("/login?error=denied");
        else if (error instanceof ApiError && (error.status === 401 || error.status === 404)) browser.replace("/login");
        else setState({ status: "error" });
      });
  }, []);
  useEffect(load, [load]);

  const signOut = async () => {
    setSigningOut(true);
    try {
      await endAdminWebSession();
    } catch {
      // The server drops the session anyway when it expires; the sign-in page is next either way.
    }
    browser.replace("/login");
  };

  if (state.status === "loading") {
    return <div className="grid min-h-screen place-items-center bg-brand-bg px-4"><Spinner label={t("app.loading")} /></div>;
  }

  if (state.status === "error") {
    return (
      <div className="grid min-h-screen place-items-center bg-brand-bg px-4 text-brand-ink">
        <div className="max-w-sm space-y-4 text-center">
          <p className="text-sm leading-6">{t("app.loadError")}</p>
          <Button variant="primary" onClick={load}>{t("common.retry")}</Button>
        </div>
      </div>
    );
  }

  const page = path === "/"
    ? { id: "overview", title: t("overview.title"), content: <OverviewPage session={state.session} onNavigate={navigate} /> }
    : path === "/analytics"
      ? { id: "analytics", title: t("nav.analytics"), content: <AnalyticsPage /> }
      : path === "/funnel"
        ? { id: "funnel", title: t("admin.funnelTitle"), content: <FunnelPage /> }
        : { id: "", title: t("notFound.title"), content: <NotFoundPage onBack={() => navigate("/")} /> };
  return (
    <Shell session={state.session} activeId={page.id} title={page.title} onNavigate={navigate} onSignOut={() => void signOut()} signingOut={signingOut}>
      {page.content}
    </Shell>
  );
}
