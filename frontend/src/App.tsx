import { lazy, memo, Suspense, useCallback, useEffect, useRef, useState } from "react";
import { getCurrentPlatformUser, getMyBloggerProfile, getMyBrandFaceProfile, getMyBusinessProfile, normalizeMarketplaceRole, type MarketplaceRole } from "./api/marketplace";
import { LoadingState } from "./components/ui";
import { LaunchScreen } from "./components/LaunchScreen";
import { useTelegram } from "./telegram/TelegramProvider";
import { FavoritesProvider } from "./features/favorites/FavoritesProvider";
import { RootScreenVisibility } from "./navigation/RootScreenVisibility";
import { useTelegramBackHandler } from "./hooks/useTelegramBackHandler";

const onboardingWelcomeKey = "bloggerbazar.onboarding.welcomeViewed";
const onboardingCompletedKey = "bloggerbazar.onboarding.completed";
type OnboardingStep = "welcome" | "telegram" | "checking" | "role" | "profile" | "success" | "complete";

const Admin = lazy(async () => ({ default: (await import("./pages/Admin")).Admin }));
const BloggerDetails = lazy(async () => ({ default: (await import("./pages/BloggerDetails")).BloggerDetails }));
const BusinessProfileForm = lazy(async () => ({ default: (await import("./pages/BusinessProfileForm")).BusinessProfileForm }));
const BloggerProfileForm = lazy(async () => ({ default: (await import("./pages/BloggerProfileForm")).BloggerProfileForm }));
const BloggerSearch = lazy(async () => ({ default: (await import("./pages/BloggerSearch")).BloggerSearch }));
const BrandFaceDetails = lazy(async () => ({ default: (await import("./pages/BrandFaceDetails")).BrandFaceDetails }));
const BrandFaceProfileForm = lazy(async () => ({ default: (await import("./pages/BrandFaceProfileForm")).BrandFaceProfileForm }));
const CampaignDetails = lazy(async () => ({ default: (await import("./pages/CampaignDetails")).CampaignDetails }));
const Campaigns = lazy(async () => ({ default: (await import("./pages/Campaigns")).Campaigns }));
const MyCampaignDetails = lazy(async () => ({ default: (await import("./pages/MyCampaignDetails")).MyCampaignDetails }));
const MyCampaignEdit = lazy(async () => ({ default: (await import("./pages/MyCampaignEdit")).MyCampaignEdit }));
const MyCampaigns = lazy(async () => ({ default: (await import("./pages/MyCampaigns")).MyCampaigns }));
const Home = lazy(async () => ({ default: (await import("./pages/Home")).Home }));
const MyRequests = lazy(async () => ({ default: (await import("./pages/MyRequests")).MyRequests }));
const Onboarding = lazy(async () => ({ default: (await import("./pages/Onboarding")).Onboarding }));
const OnboardingSuccess = lazy(async () => ({ default: (await import("./pages/OnboardingSuccess")).OnboardingSuccess }));
const ProfileDashboard = lazy(async () => ({ default: (await import("./pages/ProfileDashboard")).ProfileDashboard }));
const TelegramAuthorization = lazy(async () => ({ default: (await import("./pages/TelegramAuthorization")).TelegramAuthorization }));
const Welcome = lazy(async () => ({ default: (await import("./pages/Welcome")).Welcome }));
const Favorites = lazy(async () => ({ default: (await import("./pages/Favorites")).Favorites }));
const CachedHome = memo(Home);
const CachedSearch = memo(BloggerSearch);
const CachedCampaigns = memo(Campaigns);
const CachedMyCampaigns = memo(MyCampaigns);
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
  const [initialDestinationResolved, setInitialDestinationResolved] = useState(false);
  const initialBootstrapStarted = useRef(false);
  const [selectedRole, setSelectedRole] = useState<MarketplaceRole>();
  const [authorizationFailed, setAuthorizationFailed] = useState(false);
  const [visitedRootRoutes, setVisitedRootRoutes] = useState<Set<string>>(() => new Set(rootRoutes.includes(route.path) ? [route.path] : ["/"]));
  const [visitedMyCampaigns, setVisitedMyCampaigns] = useState(() => ["/my-campaigns", "/my-campaign", "/my-campaign-edit"].includes(route.path));
  const [sessionEpoch, setSessionEpoch] = useState(0);

  useEffect(() => {
    if (onboardingStep === "complete" && rootRoutes.includes(route.path)) {
      setVisitedRootRoutes((current) => current.has(route.path) ? current : new Set([...current, route.path]));
    }
  }, [onboardingStep, route.path]);

  useEffect(() => {
    if (onboardingStep === "complete" && ["/my-campaigns", "/my-campaign", "/my-campaign-edit"].includes(route.path)) setVisitedMyCampaigns(true);
  }, [onboardingStep, route.path]);

  const resolveDestination = useCallback(async () => {
    setAuthorizationFailed(false);
    if (!isTelegram) {
      setOnboardingStep("telegram");
      return;
    }

    try {
      setOnboardingStep("checking");
      const user = await getCurrentPlatformUser();
      const marketplaceRole = normalizeMarketplaceRole(user.selectedMarketplaceRole);
      if (!marketplaceRole) {
        setOnboardingStep("role");
        return;
      }

      if (await selectedProfileExists(marketplaceRole)) {
        setSelectedRole(marketplaceRole);
        localStorage.setItem(onboardingCompletedKey, "true");
        setOnboardingStep("complete");
        window.location.hash = "/";
        return;
      }

      setSelectedRole(undefined);
      setOnboardingStep("role");
    } catch {
      setAuthorizationFailed(true);
      setOnboardingStep("telegram");
    }
  }, [isTelegram]);

  useEffect(() => {
    if (initialBootstrapStarted.current) return;
    initialBootstrapStarted.current = true;

    if (localStorage.getItem(onboardingWelcomeKey) !== "true") {
      setOnboardingStep("welcome");
      setInitialDestinationResolved(true);
      return;
    }

    void resolveDestination().finally(() => setInitialDestinationResolved(true));
  }, [resolveDestination]);

  const authorize = async () => {
    await resolveDestination();
  };

  const beginAuthorization = () => {
    localStorage.setItem(onboardingWelcomeKey, "true");
    setOnboardingStep("telegram");
  };
  const handleRoleSelected = (role: MarketplaceRole) => {
    setSelectedRole(role);
    setOnboardingStep("profile");
    window.location.hash = profileRoute(role);
  };
  const handleProfileCompleted = () => setOnboardingStep("success");
  const finishOnboarding = () => {
    localStorage.setItem(onboardingCompletedKey, "true");
    setOnboardingStep("complete");
    window.location.hash = "/";
  };
  const resetToWelcome = useCallback(() => {
    setBackButtonHandler();
    setSelectedRole(undefined);
    setAuthorizationFailed(false);
    setVisitedRootRoutes(new Set(["/"]));
    setSessionEpoch((current) => current + 1);
    setOnboardingStep("welcome");
    if (window.location.hash !== "#/") window.location.hash = "/";
  }, [setBackButtonHandler]);

  const goBackFromNestedRoute = useCallback(() => {
    if (window.history.length > 1) window.history.back();
    else window.location.hash = "/";
  }, []);
  useTelegramBackHandler(goBackFromNestedRoute, onboardingStep === "complete" && !rootRoutes.includes(route.path));

  useEffect(() => {
    if (["/blogger", "/brand-face-detail", "/campaign", "/my-campaign", "/my-campaign-edit"].includes(route.path)) {
      window.scrollTo(0, 0);
    }
  }, [route.id, route.path]);

  const knownRoutes = ["/", "/profile", "/favorites", "/blogger-form", "/business", "/brand-face", "/brand-face-detail", "/search", "/blogger", "/campaigns", "/campaign", "/my-campaigns", "/my-campaign", "/my-campaign-edit", "/requests", "/admin"];
  const onboardingContent = onboardingStep === "welcome" ? <Welcome onContinue={beginAuthorization} />
    : onboardingStep === "telegram" ? <TelegramAuthorization failed={authorizationFailed} isTelegram={isTelegram} loading={false} onContinue={authorize} />
      : onboardingStep === "checking" ? <TelegramAuthorization failed={false} isTelegram={isTelegram} loading onContinue={authorize} />
        : onboardingStep === "role" ? <Onboarding onRoleSelected={handleRoleSelected} />
          : onboardingStep === "profile" && selectedRole === "Blogger" ? <BloggerProfileForm onBackToRole={() => { setSelectedRole(undefined); setOnboardingStep("role"); window.location.hash = "/"; }} onCompleted={handleProfileCompleted} />
            : onboardingStep === "profile" && selectedRole === "BrandFace" ? <BrandFaceProfileForm onBackToRole={() => { setSelectedRole(undefined); setOnboardingStep("role"); window.location.hash = "/"; }} onCompleted={handleProfileCompleted} />
              : onboardingStep === "profile" && selectedRole === "Business" ? <BusinessProfileForm onBackToRole={() => { setSelectedRole(undefined); setOnboardingStep("role"); window.location.hash = "/"; }} onCompleted={handleProfileCompleted} />
                : onboardingStep === "success" ? <OnboardingSuccess onContinue={finishOnboarding} />
                  : <div className="screen"><LoadingState /></div>;

  if (!initialDestinationResolved) return <LaunchScreen />;

  return <FavoritesProvider enabled={onboardingStep === "complete"} key={sessionEpoch}><main className={`app-shell ${onboardingStep !== "complete" ? "app-shell--first-run" : ""}`}><Suspense fallback={onboardingStep === "complete" ? <div className="screen"><LoadingState /></div> : <LaunchScreen />}>
    {onboardingStep !== "complete" ? onboardingContent : <>
      {(visitedRootRoutes.has("/") || route.path === "/") && <RootScreenVisibility active={route.path === "/"}><CachedHome role={selectedRole} /></RootScreenVisibility>}
      {(visitedRootRoutes.has("/profile") || route.path === "/profile") && <RootScreenVisibility active={route.path === "/profile"}><CachedProfile onMarketplaceRoleSelected={setSelectedRole} onSessionReset={resetToWelcome} /></RootScreenVisibility>}
      {route.path === "/favorites" && <Favorites />}
      {route.path === "/blogger-form" && <BloggerProfileForm />}
      {route.path === "/business" && <BusinessProfileForm />}
      {route.path === "/brand-face" && <BrandFaceProfileForm />}
      {route.path === "/brand-face-detail" && route.id && <BrandFaceDetails id={route.id} />}
      {(visitedRootRoutes.has("/search") || route.path === "/search") && <RootScreenVisibility active={route.path === "/search"}><CachedSearch /></RootScreenVisibility>}
      {route.path === "/blogger" && route.id && <BloggerDetails id={route.id} />}
      {(visitedRootRoutes.has("/campaigns") || route.path === "/campaigns") && <RootScreenVisibility active={route.path === "/campaigns"}><CachedCampaigns /></RootScreenVisibility>}
      {route.path === "/campaign" && route.id && <CampaignDetails id={route.id} />}
      {(visitedMyCampaigns || route.path === "/my-campaigns") && <RootScreenVisibility active={route.path === "/my-campaigns"}><CachedMyCampaigns /></RootScreenVisibility>}
      {route.path === "/my-campaign" && route.id && <MyCampaignDetails id={route.id} />}
      {route.path === "/my-campaign-edit" && route.id && <MyCampaignEdit id={route.id} />}
      {(visitedRootRoutes.has("/requests") || route.path === "/requests") && <RootScreenVisibility active={route.path === "/requests"}><CachedRequests /></RootScreenVisibility>}
      {route.path === "/admin" && <Admin />}
      {!knownRoutes.includes(route.path) && <Home />}
    </>}
  </Suspense></main></FavoritesProvider>;
}
