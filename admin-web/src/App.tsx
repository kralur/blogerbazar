import { useCallback, useEffect, useState } from "react";
import { ApiError } from "./api/client";
import { signOutEverywhere } from "./api/admin";
import { endAdminWebSession, getAdminWebSession, type AdminWebSession } from "./api/session";
import { callbackPath } from "./auth/telegramCallback";
import { NavigateContext } from "./components/Link";
import { Button, Spinner } from "./components/ui";
import { useI18n } from "./i18n";
import { Shell } from "./layout/Shell";
import { browser } from "./lib/browser";
import { AuthCallbackPage } from "./pages/AuthCallbackPage";
import { AnalyticsPage } from "./pages/AnalyticsPage";
import { AuditLogPage } from "./pages/AuditLogPage";
import { ComplaintsPage } from "./pages/ComplaintsPage";
import { DealsPage } from "./pages/DealsPage";
import { FunnelPage } from "./pages/FunnelPage";
import { BloggerModerationPage, BusinessModerationPage, CampaignModerationPage } from "./pages/ModerationPage";
import { NotFoundPage, OverviewPage } from "./pages/OverviewPage";
import { SecurityPage } from "./pages/SecurityPage";
import { UserPage } from "./pages/UserPage";
import { UsersPage } from "./pages/UsersPage";

type SessionState = { status: "loading" } | { status: "ready"; session: AdminWebSession } | { status: "error" };
type Page = { id: string; title: string; content: React.ReactNode };

// Which page an address opens; the id marks the menu item. A user card (/users/<Telegram id>) belongs to "Users".
function pageFor(path: string, session: AdminWebSession, navigate: (path: string) => void, t: (key: string) => string): Page {
  const userCard = path.match(/^\/users\/(\d{1,19})$/);
  if (userCard) return { id: "users", title: t("user.title"), content: <UserPage key={userCard[1]} telegramUserId={Number(userCard[1])} session={session} /> };
  switch (path) {
    case "/": return { id: "overview", title: t("overview.title"), content: <OverviewPage session={session} onNavigate={navigate} /> };
    case "/analytics": return { id: "analytics", title: t("nav.analytics"), content: <AnalyticsPage /> };
    case "/funnel": return { id: "funnel", title: t("admin.funnelTitle"), content: <FunnelPage /> };
    case "/users": return { id: "users", title: t("nav.users"), content: <UsersPage /> };
    case "/moderation/bloggers": return { id: "moderation-bloggers", title: t("moderation.bloggersPage"), content: <BloggerModerationPage /> };
    case "/moderation/businesses": return { id: "moderation-businesses", title: t("moderation.businessesPage"), content: <BusinessModerationPage /> };
    case "/moderation/campaigns": return { id: "moderation-campaigns", title: t("moderation.campaignsPage"), content: <CampaignModerationPage /> };
    case "/complaints": return { id: "complaints", title: t("nav.complaints"), content: <ComplaintsPage session={session} /> };
    case "/deals": return { id: "deals", title: t("nav.deals"), content: <DealsPage /> };
    case "/audit-log": return { id: "audit-log", title: t("nav.auditLog"), content: <AuditLogPage /> };
    case "/security": return { id: "security", title: t("nav.security"), content: <SecurityPage /> };
    default: return { id: "", title: t("notFound.title"), content: <NotFoundPage onBack={() => navigate("/")} /> };
  }
}

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
    window.scrollTo?.(0, 0);
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

  // Every browser of this admin: the server ends them all, this one included.
  const signOutAll = async () => {
    await signOutEverywhere();
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

  const page = pageFor(path, state.session, navigate, t);
  return (
    <NavigateContext.Provider value={navigate}>
      <Shell session={state.session} activeId={page.id} title={page.title} onNavigate={navigate} onSignOut={() => void signOut()} onSignOutAll={signOutAll} signingOut={signingOut}>
        {page.content}
      </Shell>
    </NavigateContext.Provider>
  );
}
