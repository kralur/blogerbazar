import { lazy, Suspense, useEffect, useState } from "react";
import { getCurrentPlatformUser, getMyBloggerProfile, getMyBrandFaceProfile, getMyBusinessProfile, normalizeMarketplaceRole, type MarketplaceRole } from "./api/marketplace";
import { LoadingState } from "./components/ui";
import { useTelegram } from "./telegram/TelegramProvider";
import { FavoritesProvider } from "./features/favorites/FavoritesProvider";

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
const Home = lazy(async () => ({ default: (await import("./pages/Home")).Home }));
const MyRequests = lazy(async () => ({ default: (await import("./pages/MyRequests")).MyRequests }));
const Onboarding = lazy(async () => ({ default: (await import("./pages/Onboarding")).Onboarding }));
const OnboardingSuccess = lazy(async () => ({ default: (await import("./pages/OnboardingSuccess")).OnboardingSuccess }));
const ProfileDashboard = lazy(async () => ({ default: (await import("./pages/ProfileDashboard")).ProfileDashboard }));
const TelegramAuthorization = lazy(async () => ({ default: (await import("./pages/TelegramAuthorization")).TelegramAuthorization }));
const Welcome = lazy(async () => ({ default: (await import("./pages/Welcome")).Welcome }));
const Favorites = lazy(async () => ({ default: (await import("./pages/Favorites")).Favorites }));

function useRoute() {
  const [hash, setHash] = useState(() => window.location.hash.replace("#", "") || "/");
  useEffect(() => {
    const handleRouteChange = () => setHash(window.location.hash.replace("#", "") || "/");
    window.addEventListener("hashchange", handleRouteChange);
    return () => window.removeEventListener("hashchange", handleRouteChange);
  }, []);
  const [path, id] = hash.split("/").filter(Boolean);
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
  const [selectedRole, setSelectedRole] = useState<MarketplaceRole>();
  const [authorizationFailed, setAuthorizationFailed] = useState(false);

  const authorize = async () => {
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

      setSelectedRole(marketplaceRole);
      if (await selectedProfileExists(marketplaceRole)) {
        localStorage.setItem(onboardingCompletedKey, "true");
        setOnboardingStep("complete");
        window.location.hash = "/";
        return;
      }

      setOnboardingStep("profile");
      window.location.hash = profileRoute(marketplaceRole);
    } catch {
      setAuthorizationFailed(true);
      setOnboardingStep("telegram");
    }
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

  useEffect(() => {
    if (onboardingStep !== "complete" || route.path === "/") {
      setBackButtonHandler();
      return;
    }
    const goBack = () => {
      if (window.history.length > 1) window.history.back();
      else window.location.hash = "/";
    };
    setBackButtonHandler(goBack);
    return () => setBackButtonHandler();
  }, [onboardingStep, route.path, route.id, setBackButtonHandler]);

  const knownRoutes = ["/", "/profile", "/favorites", "/blogger-form", "/business", "/brand-face", "/brand-face-detail", "/search", "/blogger", "/campaigns", "/campaign", "/requests", "/admin"];
  const onboardingContent = onboardingStep === "welcome" ? <Welcome onContinue={beginAuthorization} />
    : onboardingStep === "telegram" ? <TelegramAuthorization failed={authorizationFailed} isTelegram={isTelegram} loading={false} onContinue={authorize} />
      : onboardingStep === "checking" ? <div className="screen"><LoadingState /></div>
        : onboardingStep === "role" ? <Onboarding onRoleSelected={handleRoleSelected} />
          : onboardingStep === "profile" && selectedRole === "Blogger" ? <BloggerProfileForm onCompleted={handleProfileCompleted} />
            : onboardingStep === "profile" && selectedRole === "BrandFace" ? <BrandFaceProfileForm onCompleted={handleProfileCompleted} />
              : onboardingStep === "profile" && selectedRole === "Business" ? <BusinessProfileForm onCompleted={handleProfileCompleted} />
                : onboardingStep === "success" ? <OnboardingSuccess onContinue={finishOnboarding} />
                  : <div className="screen"><LoadingState /></div>;

  return <FavoritesProvider enabled={onboardingStep === "complete"}><main className={`app-shell bg-soft-radial ${onboardingStep !== "complete" ? "app-shell--first-run" : ""}`}><Suspense fallback={<div className="screen"><LoadingState /></div>}>
    {onboardingStep !== "complete" ? onboardingContent : <>
      {route.path === "/" && <Home />}
      {route.path === "/profile" && <ProfileDashboard />}
      {route.path === "/favorites" && <Favorites />}
      {route.path === "/blogger-form" && <BloggerProfileForm />}
      {route.path === "/business" && <BusinessProfileForm />}
      {route.path === "/brand-face" && <BrandFaceProfileForm />}
      {route.path === "/brand-face-detail" && route.id && <BrandFaceDetails id={route.id} />}
      {route.path === "/search" && <BloggerSearch />}
      {route.path === "/blogger" && route.id && <BloggerDetails id={route.id} />}
      {route.path === "/campaigns" && <Campaigns />}
      {route.path === "/campaign" && route.id && <CampaignDetails id={route.id} />}
      {route.path === "/requests" && <MyRequests />}
      {route.path === "/admin" && <Admin />}
      {!knownRoutes.includes(route.path) && <Home />}
    </>}
  </Suspense></main></FavoritesProvider>;
}
