import { lazy, Suspense, useEffect, useState } from "react";
import { getCurrentPlatformUser } from "./api/marketplace";
import { LoadingState } from "./components/ui";
import { useTelegram } from "./telegram/TelegramProvider";

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
const ProfileDashboard = lazy(async () => ({ default: (await import("./pages/ProfileDashboard")).ProfileDashboard }));

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

export function App() {
  const route = useRoute();
  const { isTelegram, setBackButtonHandler } = useTelegram();
  const [onboardingRequired, setOnboardingRequired] = useState(false);
  const [onboardingLoading, setOnboardingLoading] = useState(isTelegram);

  useEffect(() => {
    if (!isTelegram) {
      setOnboardingLoading(false);
      return;
    }
    let cancelled = false;
    const loadUser = () => {
      setOnboardingLoading(true);
      getCurrentPlatformUser()
        .then((user) => { if (!cancelled) setOnboardingRequired(!user.selectedMarketplaceRole); })
        .catch(() => { if (!cancelled) setOnboardingRequired(false); })
        .finally(() => { if (!cancelled) setOnboardingLoading(false); });
    };
    loadUser();
    window.addEventListener("bloggerbazar:role-selected", loadUser);
    return () => { cancelled = true; window.removeEventListener("bloggerbazar:role-selected", loadUser); };
  }, [isTelegram]);

  useEffect(() => {
    if (route.path === "/") {
      setBackButtonHandler();
      return;
    }
    const goBack = () => {
      if (window.history.length > 1) window.history.back();
      else window.location.hash = "/";
    };
    setBackButtonHandler(goBack);
    return () => setBackButtonHandler();
  }, [route.path, route.id, setBackButtonHandler]);

  const knownRoutes = ["/", "/profile", "/blogger-form", "/business", "/brand-face", "/brand-face-detail", "/search", "/blogger", "/campaigns", "/campaign", "/requests", "/admin"];
  return <main className="app-shell bg-soft-radial"><Suspense fallback={<div className="screen"><LoadingState /></div>}>
    {onboardingLoading && <div className="screen"><LoadingState /></div>}
    {!onboardingLoading && onboardingRequired && <Onboarding />}
    {!onboardingLoading && !onboardingRequired && <>
      {route.path === "/" && <Home />}
      {route.path === "/profile" && <ProfileDashboard />}
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
  </Suspense></main>;
}
