import { lazy, memo, Suspense, useCallback, useEffect, useRef, useState } from "react";
import { browserSignInCodeFrom } from "./api/browserSessions";
import { teamJoinCodeFrom } from "./api/team";
import { ApiError, sessionExpiredEvent } from "./api/client";
import { getCurrentPlatformUser, getMyBloggerProfile, getMyBrandFaceProfile, getMyBusinessProfile, normalizeMarketplaceRole, updateInterfaceLanguage, type MarketplaceRole } from "./api/marketplace";
import { useI18n } from "./i18n";
import { PullToRefresh } from "./components/PullToRefresh";
import { SessionExpiredNotice } from "./components/SessionExpiredNotice";
import { ActionCountsProvider } from "./features/actionCounts/ActionCountsProvider";
import { requestScreenRefresh } from "./hooks/useScreenRefresh";
import { LoadingState } from "./components/ui";
import { LaunchScreen } from "./components/LaunchScreen";
import { useTelegram, telegramBridge } from "./telegram/TelegramProvider";
import { FavoritesProvider } from "./features/favorites/FavoritesProvider";
import { RootScreenVisibility } from "./navigation/RootScreenVisibility";
import { useTelegramBackHandler } from "./hooks/useTelegramBackHandler";
import { requestGuardedNavigation } from "./navigation/guardedNavigation";
import { replaceHistoryRoute } from "./navigation/hashNavigation";
import { clearMyCampaignCache } from "./data/myCampaignCache";
import { clearPublicDetailCache } from "./data/publicDetailCache";
import { clearCampaignApplicationCache } from "./data/campaignApplicationCache";
import { clearDealCache } from "./data/dealCache";
import { isDeepLinkRoute } from "./lib/deepLink";
import { setBrowserSession } from "./lib/browserSession";
import { DesktopLayoutContext, useDesktopLayout } from "./lib/desktopLayout";
import { DesktopNav } from "./components/DesktopNav";

const onboardingWelcomeKey = "bloggerbazar.onboarding.welcomeViewed";
// The role of the last finished start. A returning user sees that role's screens at once while the account check runs
// in the background (three trips to the server before the first screen made every start slow).
const lastRoleKey = "bloggerbazar.lastRole";
function rememberedRole(): MarketplaceRole | undefined {
  const value = localStorage.getItem(lastRoleKey);
  return value === "Blogger" || value === "BrandFace" || value === "Business" ? value : undefined;
}
const onboardingCompletedKey = "bloggerbazar.onboarding.completed";
// browser: D64 stage B, the page outside Telegram asks a company member to sign this browser in.
// mode and teamWelcome: D64 stage C1, "alone or with a team" after the Business role, and the window after a company
// with a team is created.
type OnboardingStep = "welcome" | "telegram" | "browser" | "checking" | "phone" | "role" | "mode" | "profile" | "success" | "teamWelcome" | "complete";

const Admin = lazy(async () => ({ default: (await import("./pages/Admin")).Admin }));
const AdminUserCard = lazy(async () => ({ default: (await import("./pages/AdminUserCard")).AdminUserCard }));
const BloggerDetails = lazy(async () => ({ default: (await import("./pages/BloggerDetails")).BloggerDetails }));
const BusinessProfileForm = lazy(async () => ({ default: (await import("./pages/BusinessProfileForm")).BusinessProfileForm }));
const BloggerProfileForm = lazy(async () => ({ default: (await import("./pages/BloggerProfileForm")).BloggerProfileForm }));
const BloggerSearch = lazy(async () => ({ default: (await import("./pages/BloggerSearch")).BloggerSearch }));
const BrandFaceDetails = lazy(async () => ({ default: (await import("./pages/BrandFaceDetails")).BrandFaceDetails }));
const AllReviews = lazy(async () => ({ default: (await import("./pages/AllReviews")).AllReviews }));
const BusinessDetails = lazy(async () => ({ default: (await import("./pages/BusinessDetails")).BusinessDetails }));
const BrandFaceProfileForm = lazy(async () => ({ default: (await import("./pages/BrandFaceProfileForm")).BrandFaceProfileForm }));
const CampaignDetails = lazy(async () => ({ default: (await import("./pages/CampaignDetails")).CampaignDetails }));
const Campaigns = lazy(async () => ({ default: (await import("./pages/Campaigns")).Campaigns }));
const MyCampaignDetails = lazy(async () => ({ default: (await import("./pages/MyCampaignDetails")).MyCampaignDetails }));
const MyCampaignEdit = lazy(async () => ({ default: (await import("./pages/MyCampaignEdit")).MyCampaignEdit }));
const MyCampaigns = lazy(async () => ({ default: (await import("./pages/MyCampaigns")).MyCampaigns }));
const Home = lazy(async () => ({ default: (await import("./pages/Home")).Home }));
const MyRequests = lazy(async () => ({ default: (await import("./pages/MyRequests")).MyRequests }));
const OfferDetails = lazy(async () => ({ default: (await import("./pages/OfferDetails")).OfferDetails }));
const DealDetails = lazy(async () => ({ default: (await import("./pages/DealDetails")).DealDetails }));
const MyApplicationDetails = lazy(async () => ({ default: (await import("./pages/MyApplicationDetails")).MyApplicationDetails }));
const MyCampaignApplications = lazy(async () => ({ default: (await import("./pages/MyCampaignApplications")).MyCampaignApplications }));
const Onboarding = lazy(async () => ({ default: (await import("./pages/Onboarding")).Onboarding }));
const OnboardingSuccess = lazy(async () => ({ default: (await import("./pages/OnboardingSuccess")).OnboardingSuccess }));
const ProfileDashboard = lazy(async () => ({ default: (await import("./pages/ProfileDashboard")).ProfileDashboard }));
const Settings = lazy(async () => ({ default: (await import("./pages/Settings")).Settings }));
const BrowserSignIn = lazy(async () => ({ default: (await import("./pages/BrowserSignIn")).BrowserSignIn }));
const BrowserSignInConfirm = lazy(async () => ({ default: (await import("./pages/BrowserSignInConfirm")).BrowserSignInConfirm }));
const BusinessModeChoice = lazy(async () => ({ default: (await import("./pages/BusinessModeChoice")).BusinessModeChoice }));
const Team = lazy(async () => ({ default: (await import("./pages/Team")).Team }));
const TeamJoin = lazy(async () => ({ default: (await import("./pages/TeamJoin")).TeamJoin }));
const TeamWelcome = lazy(async () => ({ default: (await import("./pages/TeamWelcome")).TeamWelcome }));
const TelegramAuthorization = lazy(async () => ({ default: (await import("./pages/TelegramAuthorization")).TelegramAuthorization }));
const PhoneVerification = lazy(async () => ({ default: (await import("./pages/PhoneVerification")).PhoneVerification }));
const Welcome = lazy(async () => ({ default: (await import("./pages/Welcome")).Welcome }));
const Favorites = lazy(async () => ({ default: (await import("./pages/Favorites")).Favorites }));
const CachedHome = memo(Home);
const CachedSearch = memo(BloggerSearch);
const CachedCampaigns = memo(Campaigns);
const CachedMyCampaigns = memo(MyCampaigns);
const CachedFavorites = memo(Favorites);
const CachedRequests = memo(MyRequests);
const CachedProfile = memo(ProfileDashboard);
const rootRoutes = ["/", "/search", "/campaigns", "/requests", "/profile"];

function useRoute() {
  const [hash, setHash] = useState(() => window.location.hash.replace("#", "") || "/");
  useEffect(() => {
    const handleRouteChange = () => setHash(window.location.hash.replace("#", "") || "/");
    window.addEventListener("hashchange", handleRouteChange);
    return () => window.removeEventListener("hashchange", handleRouteChange);
  }, []);
  const [pathname] = hash.split("?");
  const [path, id] = pathname.split("/").filter(Boolean);
  return { path: path ? `/${path}` : "/", id };
}

// Bot notifications open the Mini App with ?bb_route=<route>; keep that route after sign-in.
function readInitialDeepLink() {
  if (typeof window === "undefined") return null;
  const route = new URLSearchParams(window.location.search).get("bb_route");
  return isDeepLinkRoute(route) ? route : null;
}
const initialDeepLink = readInitialDeepLink();

// D64 stage B: the Mini App opened from a browser's sign-in link (startapp=web_<code>), once per code.
const browserSignInSeenKey = "bloggerbazar.browserSignIn.seen";
function readBrowserSignInCode() {
  if (typeof window === "undefined") return null;
  const code = browserSignInCodeFrom(window.Telegram?.WebApp?.initDataUnsafe?.start_param);
  try {
    return code && sessionStorage.getItem(browserSignInSeenKey) !== code ? code : null;
  } catch {
    return code;
  }
}

// D64 stage C1: the Mini App opened from a team invitation (startapp=join_<code>), once per code.
const teamJoinSeenKey = "bloggerbazar.teamJoin.seen";
function readTeamJoinCode() {
  if (typeof window === "undefined") return null;
  const code = teamJoinCodeFrom(window.Telegram?.WebApp?.initDataUnsafe?.start_param);
  try {
    return code && sessionStorage.getItem(teamJoinSeenKey) !== code ? code : null;
  } catch {
    return code;
  }
}

function initialOnboardingStep(): OnboardingStep {
  if (localStorage.getItem(onboardingCompletedKey) === "true") return "complete";
  return localStorage.getItem(onboardingWelcomeKey) === "true" ? "telegram" : "welcome";
}

function profileRoute(role: MarketplaceRole) {
  return role === "Blogger" ? "/blogger-form" : role === "BrandFace" ? "/brand-face" : "/business";
}

async function selectedProfileExists(role: MarketplaceRole) {
  try {
    if (role === "Blogger") await getMyBloggerProfile();
    else if (role === "BrandFace") await getMyBrandFaceProfile();
    else await getMyBusinessProfile();
    return true;
  } catch {
    return false;
  }
}

export function App() {
  const route = useRoute();
  const { isTelegram, setBackButtonHandler } = useTelegram();
  const [onboardingStep, setOnboardingStep] = useState<OnboardingStep>(initialOnboardingStep);
  const [quickStart] = useState(() => isTelegram && localStorage.getItem(onboardingWelcomeKey) === "true"
    && localStorage.getItem(onboardingCompletedKey) === "true" && rememberedRole() !== undefined);
  const [initialDestinationResolved, setInitialDestinationResolved] = useState(quickStart);
  const initialBootstrapStarted = useRef(false);
  const [selectedRole, setSelectedRole] = useState<MarketplaceRole | undefined>(() => quickStart ? rememberedRole() : undefined);
  const [authorizationFailed, setAuthorizationFailed] = useState(false);
  const [visitedRootRoutes, setVisitedRootRoutes] = useState<Set<string>>(() => new Set(rootRoutes.includes(route.path) ? [route.path] : ["/"]));
  const [visitedMyCampaigns, setVisitedMyCampaigns] = useState(() => ["/my-campaigns", "/my-campaign", "/my-campaign-edit"].includes(route.path));
  const [visitedFavorites, setVisitedFavorites] = useState(() => route.path === "/favorites");
  const [sessionEpoch, setSessionEpoch] = useState(0);
  const [browserSignInCode, setBrowserSignInCode] = useState(() => isTelegram ? readBrowserSignInCode() : null);
  const browserSignInRoleChanged = useRef(false);
  const [teamJoinCode, setTeamJoinCode] = useState(() => isTelegram ? readTeamJoinCode() : null);
  // The company form creates a company "with a team" when that was chosen right before it.
  const [newCompanyTeamMode, setNewCompanyTeamMode] = useState(false);
  // D64 stage B2: a wide browser window gets the website layout; Telegram keeps the phone layout.
  const desktop = useDesktopLayout(!isTelegram);

  useEffect(() => {
    if (onboardingStep === "complete" && rootRoutes.includes(route.path)) {
      setVisitedRootRoutes((current) => current.has(route.path) ? current : new Set([...current, route.path]));
    }
  }, [onboardingStep, route.path]);

  useEffect(() => {
    if (onboardingStep === "complete") telegramBridge.requestBotMessages();
  }, [onboardingStep]);

  useEffect(() => {
    if (onboardingStep === "complete" && selectedRole) localStorage.setItem(lastRoleKey, selectedRole);
  }, [onboardingStep, selectedRole]);

  // Back in the app after a while: data other people changed meanwhile is reloaded without a gesture.
  useEffect(() => {
    if (onboardingStep !== "complete") return;
    let hiddenAt: number | null = null;
    const onVisibility = () => {
      if (document.visibilityState === "hidden") { hiddenAt = Date.now(); return; }
      if (hiddenAt !== null && Date.now() - hiddenAt > 30_000) void requestScreenRefresh();
      hiddenAt = null;
    };
    document.addEventListener("visibilitychange", onVisibility);
    return () => document.removeEventListener("visibilitychange", onVisibility);
  }, [onboardingStep]);

  const { language } = useI18n();
  const syncedLanguage = useRef<string>();
  useEffect(() => {
    if (onboardingStep !== "complete" || syncedLanguage.current === language) return;
    syncedLanguage.current = language;
    // Best effort: a failed sync only means bot messages stay bilingual until the next launch.
    void updateInterfaceLanguage(language).catch(() => { syncedLanguage.current = undefined; });
  }, [language, onboardingStep]);

  useEffect(() => {
    if (onboardingStep === "complete" && ["/my-campaigns", "/my-campaign", "/my-campaign-edit"].includes(route.path)) setVisitedMyCampaigns(true);
  }, [onboardingStep, route.path]);
  useEffect(() => {
    if (onboardingStep === "complete" && route.path === "/favorites") setVisitedFavorites(true);
  }, [onboardingStep, route.path]);

  // quiet: the screens are already shown (quick start), so only a different answer changes them.
  const resolveDestination = useCallback(async ({ quiet = false }: { quiet?: boolean } = {}) => {
    setAuthorizationFailed(false);
    if (!isTelegram) {
      // D64 stage B: outside Telegram only a company member's browser session works; without one, sign in.
      try {
        if (!quiet) setOnboardingStep("checking");
        const user = await getCurrentPlatformUser();
        if (normalizeMarketplaceRole(user.selectedMarketplaceRole) !== "Business") throw new ApiError(401);
        setBrowserSession(true);
        setSelectedRole("Business");
        setOnboardingStep("complete");
        window.location.hash = initialDeepLink ?? (window.location.hash.replace("#", "") || "/");
      } catch {
        setBrowserSession(false);
        setOnboardingStep("browser");
      }
      return;
    }

    try {
      if (!quiet) setOnboardingStep("checking");
      const user = await getCurrentPlatformUser();
      // Everyone, new or returning, confirms the phone from Telegram once before anything else (D41).
      if (!user.verifiedPhone) {
        setOnboardingStep("phone");
        return;
      }
      const marketplaceRole = normalizeMarketplaceRole(user.selectedMarketplaceRole);
      if (!marketplaceRole) {
        setOnboardingStep("role");
        return;
      }

      if (await selectedProfileExists(marketplaceRole)) {
        setSelectedRole(marketplaceRole);
        localStorage.setItem(onboardingCompletedKey, "true");
        setOnboardingStep("complete");
        // Quick start already opened the first screen; the user may have moved on since.
        if (!quiet) window.location.hash = initialDeepLink ?? "/";
        return;
      }

      setSelectedRole(undefined);
      setOnboardingStep("role");
    } catch (error) {
      // A network hiccup during a quick start keeps the screens; each one shows its own retry.
      if (quiet && !(error instanceof ApiError && (error.status === 401 || error.status === 403))) return;
      setAuthorizationFailed(true);
      setOnboardingStep("telegram");
    }
  }, [isTelegram]);

  useEffect(() => {
    if (initialBootstrapStarted.current) return;
    initialBootstrapStarted.current = true;

    if (isTelegram && localStorage.getItem(onboardingWelcomeKey) !== "true") {
      setOnboardingStep("welcome");
      setInitialDestinationResolved(true);
      return;
    }

    if (quickStart && initialDeepLink) window.location.hash = initialDeepLink;
    void resolveDestination({ quiet: quickStart }).finally(() => setInitialDestinationResolved(true));
  }, [isTelegram, quickStart, resolveDestination]);

  // A browser session that ended (signed out elsewhere, removed from the team, 12 hours over): sign in again.
  useEffect(() => {
    if (isTelegram) return;
    const showSignIn = () => {
      setBrowserSession(false);
      setOnboardingStep((current) => current === "browser" ? current : "browser");
    };
    window.addEventListener(sessionExpiredEvent, showSignIn);
    return () => window.removeEventListener(sessionExpiredEvent, showSignIn);
  }, [isTelegram]);

  const authorize = async () => {
    await resolveDestination();
  };

  const beginAuthorization = () => {
    localStorage.setItem(onboardingWelcomeKey, "true");
    setOnboardingStep("telegram");
  };
  const handleRoleSelected = (role: MarketplaceRole) => {
    setSelectedRole(role);
    if (role === "Business") {
      setOnboardingStep("mode");
      return;
    }
    setOnboardingStep("profile");
    window.location.hash = profileRoute(role);
  };
  const handleModeChosen = (teamMode: boolean) => {
    setNewCompanyTeamMode(teamMode);
    setOnboardingStep("profile");
    window.location.hash = profileRoute("Business");
  };
  const handleProfileCompleted = () => setOnboardingStep(selectedRole === "Business" && newCompanyTeamMode ? "teamWelcome" : "success");
  const finishOnboarding = (destination = "/") => {
    localStorage.setItem(onboardingCompletedKey, "true");
    setOnboardingStep("complete");
    window.location.hash = destination;
  };
  const resetToWelcome = useCallback(() => {
    setBackButtonHandler();
    setSelectedRole(undefined);
    setAuthorizationFailed(false);
    setVisitedRootRoutes(new Set(["/"]));
    clearMyCampaignCache();
    clearCampaignApplicationCache();
    clearDealCache();
    clearPublicDetailCache();
    setSessionEpoch((current) => current + 1);
    syncedLanguage.current = undefined;
    setOnboardingStep("welcome");
    if (window.location.hash !== "#/") window.location.hash = "/";
  }, [setBackButtonHandler]);

  const handleBrowserSignedOut = useCallback(() => {
    setBrowserSession(false);
    setSelectedRole(undefined);
    clearMyCampaignCache();
    clearCampaignApplicationCache();
    clearDealCache();
    clearPublicDetailCache();
    setSessionEpoch((current) => current + 1);
    setOnboardingStep("browser");
    if (window.location.hash !== "#/") window.location.hash = "/";
  }, []);

  const closeBrowserSignInConfirm = useCallback(() => {
    try { if (browserSignInCode) sessionStorage.setItem(browserSignInSeenKey, browserSignInCode); } catch { /* shown again next time */ }
    setBrowserSignInCode(null);
    // Switching to Business on that screen changes which screens the app shows.
    if (browserSignInRoleChanged.current) {
      browserSignInRoleChanged.current = false;
      void resolveDestination();
    }
  }, [browserSignInCode, resolveDestination]);

  const closeTeamJoin = useCallback((opened: boolean) => {
    try { if (teamJoinCode) sessionStorage.setItem(teamJoinSeenKey, teamJoinCode); } catch { /* shown again next time */ }
    setTeamJoinCode(null);
    // Already in the team: the app opens as the company, past the welcome screen.
    if (opened) {
      localStorage.setItem(onboardingWelcomeKey, "true");
      void resolveDestination();
    }
  }, [resolveDestination, teamJoinCode]);

  const handleMarketplaceRoleSelected = useCallback((role: MarketplaceRole) => {
    clearMyCampaignCache();
    clearCampaignApplicationCache();
    clearDealCache();
    setSelectedRole(role);
  }, []);

  const goBackFromNestedRoute = useCallback(() => {
    if (route.path === "/my-campaign-edit" && route.id) {
      const destination = `/my-campaign/${route.id}`;
      if (!requestGuardedNavigation(destination)) replaceHistoryRoute(`#${destination}`);
      return;
    }
    if (window.history.length > 1) window.history.back();
    else window.location.hash = "/";
  }, [route.id, route.path]);
  useTelegramBackHandler(goBackFromNestedRoute, onboardingStep === "complete" && !rootRoutes.includes(route.path));

  useEffect(() => {
    if (["/blogger", "/blogger-reviews", "/brand-face-detail", "/brand-face-reviews", "/company", "/company-reviews", "/campaign", "/my-campaign", "/my-campaign-edit", "/my-application", "/my-campaign-applications", "/deal", "/offer", "/team"].includes(route.path)) {
      window.scrollTo(0, 0);
    }
  }, [route.id, route.path]);

  const knownRoutes = ["/", "/profile", "/settings", "/favorites", "/blogger-form", "/business", "/brand-face", "/brand-face-detail", "/brand-face-reviews", "/company", "/company-reviews", "/search", "/blogger", "/blogger-reviews", "/campaigns", "/campaign", "/my-campaigns", "/my-campaign", "/my-campaign-edit", "/my-campaign-applications", "/my-application", "/deal", "/offer", "/requests", "/admin", "/team"];
  const onboardingContent = onboardingStep === "welcome" ? <Welcome onContinue={beginAuthorization} />
    : onboardingStep === "browser" ? <BrowserSignIn onSignedIn={() => void resolveDestination()} />
    : onboardingStep === "telegram" ? <TelegramAuthorization failed={authorizationFailed} isTelegram={isTelegram} loading={false} onContinue={authorize} />
      : onboardingStep === "checking" ? <TelegramAuthorization failed={false} isTelegram={isTelegram} loading onContinue={authorize} />
      : onboardingStep === "phone" ? <PhoneVerification onVerified={() => void resolveDestination()} />
        : onboardingStep === "role" ? <Onboarding onRoleSelected={handleRoleSelected} />
          : onboardingStep === "mode" ? <BusinessModeChoice onBack={() => { setSelectedRole(undefined); setOnboardingStep("role"); }} onChosen={handleModeChosen} />
          : onboardingStep === "profile" && selectedRole === "Blogger" ? <BloggerProfileForm onBackToRole={() => { setSelectedRole(undefined); setOnboardingStep("role"); window.location.hash = "/"; }} onCompleted={handleProfileCompleted} />
            : onboardingStep === "profile" && selectedRole === "BrandFace" ? <BrandFaceProfileForm onBackToRole={() => { setSelectedRole(undefined); setOnboardingStep("role"); window.location.hash = "/"; }} onCompleted={handleProfileCompleted} />
              : onboardingStep === "profile" && selectedRole === "Business" ? <BusinessProfileForm onBackToRole={() => { setOnboardingStep("mode"); window.location.hash = "/"; }} onCompleted={handleProfileCompleted} teamMode={newCompanyTeamMode} />
                : onboardingStep === "success" ? <OnboardingSuccess onContinue={() => finishOnboarding()} />
                  : onboardingStep === "teamWelcome" ? <TeamWelcome onDone={finishOnboarding} />
                  : <div className="screen"><LoadingState /></div>;

  if (browserSignInCode) {
    return <Suspense fallback={<LaunchScreen />}><BrowserSignInConfirm code={browserSignInCode} onClose={closeBrowserSignInConfirm} onRoleChanged={() => { browserSignInRoleChanged.current = true; }} /></Suspense>;
  }

  if (teamJoinCode) {
    return <Suspense fallback={<LaunchScreen />}><TeamJoin code={teamJoinCode} onClose={closeTeamJoin} /></Suspense>;
  }

  if (!initialDestinationResolved) return <LaunchScreen />;

  return <DesktopLayoutContext.Provider value={desktop}><FavoritesProvider enabled={onboardingStep === "complete"} key={sessionEpoch}><ActionCountsProvider enabled={onboardingStep === "complete"} role={selectedRole}><main className={`app-shell ${onboardingStep !== "complete" ? "app-shell--first-run" : ""}`}><div aria-hidden="true" className="app-top-scrim" />{desktop && onboardingStep === "complete" && <DesktopNav />}<PullToRefresh enabled={onboardingStep === "complete"} /><SessionExpiredNotice enabled={isTelegram} /><Suspense fallback={onboardingStep === "complete" ? <div className="screen"><LoadingState /></div> : <LaunchScreen />}>
    {onboardingStep !== "complete" ? onboardingContent : <>
      {(visitedRootRoutes.has("/") || route.path === "/") && <RootScreenVisibility active={route.path === "/"}><CachedHome key={selectedRole ?? "none"} role={selectedRole} /></RootScreenVisibility>}
      {(visitedRootRoutes.has("/profile") || route.path === "/profile") && <RootScreenVisibility active={route.path === "/profile"}><CachedProfile onMarketplaceRoleSelected={handleMarketplaceRoleSelected} /></RootScreenVisibility>}
      {route.path === "/settings" && <Settings onBrowserSignedOut={handleBrowserSignedOut} onSessionReset={resetToWelcome} />}
      {(visitedFavorites || route.path === "/favorites") && <RootScreenVisibility active={route.path === "/favorites"}><CachedFavorites /></RootScreenVisibility>}
      {route.path === "/blogger-form" && <BloggerProfileForm />}
      {route.path === "/business" && <BusinessProfileForm />}
      {route.path === "/team" && <Team />}
      {route.path === "/brand-face" && <BrandFaceProfileForm />}
      {route.path === "/brand-face-detail" && route.id && <BrandFaceDetails id={route.id} />}
      {route.path === "/company" && route.id && <BusinessDetails id={route.id} />}
      {route.path === "/company-reviews" && route.id && <AllReviews id={route.id} key={`business-${route.id}`} target="business" />}
      {route.path === "/blogger-reviews" && route.id && <AllReviews id={route.id} key={`blogger-${route.id}`} target="blogger" />}
      {route.path === "/brand-face-reviews" && route.id && <AllReviews id={route.id} key={`brand-face-${route.id}`} target="brandFace" />}
      {(visitedRootRoutes.has("/search") || route.path === "/search") && <RootScreenVisibility active={route.path === "/search"}><CachedSearch /></RootScreenVisibility>}
      {route.path === "/blogger" && route.id && <BloggerDetails id={route.id} />}
      {(visitedRootRoutes.has("/campaigns") || route.path === "/campaigns") && <RootScreenVisibility active={route.path === "/campaigns"}><CachedCampaigns key={selectedRole ?? "none"} /></RootScreenVisibility>}
      {route.path === "/campaign" && route.id && <CampaignDetails id={route.id} />}
      {(visitedMyCampaigns || route.path === "/my-campaigns") && <RootScreenVisibility active={route.path === "/my-campaigns"}><CachedMyCampaigns key={selectedRole ?? "none"} /></RootScreenVisibility>}
      {route.path === "/my-campaign" && route.id && <MyCampaignDetails id={route.id} />}
      {route.path === "/my-campaign-edit" && route.id && <MyCampaignEdit id={route.id} />}
      {route.path === "/my-campaign-applications" && route.id && <MyCampaignApplications campaignId={route.id} />}
      {(visitedRootRoutes.has("/requests") || route.path === "/requests") && <RootScreenVisibility active={route.path === "/requests"}><CachedRequests activeMarketplaceRole={selectedRole} key={`${selectedRole ?? "none"}-${sessionEpoch}`} /></RootScreenVisibility>}
      {route.path === "/my-application" && route.id && <MyApplicationDetails id={route.id} />}
      {route.path === "/deal" && route.id && <DealDetails id={route.id} viewerRole={selectedRole} />}
      {route.path === "/offer" && route.id && <OfferDetails id={route.id} viewerRole={selectedRole} />}
      {route.path === "/admin" && (route.id === "user" ? <AdminUserCard key={window.location.hash} /> : <Admin section={route.id} />)}
      {!knownRoutes.includes(route.path) && <Home role={selectedRole} />}
    </>}
  </Suspense></main></ActionCountsProvider></FavoritesProvider></DesktopLayoutContext.Provider>;
}
