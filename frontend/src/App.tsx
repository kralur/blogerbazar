import { useEffect, useState } from "react";
import { Admin } from "./pages/Admin";
import { BloggerDetails } from "./pages/BloggerDetails";
import { BusinessProfileForm } from "./pages/BusinessProfileForm";
import { BloggerProfileForm } from "./pages/BloggerProfileForm";
import { BloggerSearch } from "./pages/BloggerSearch";
import { CampaignDetails } from "./pages/CampaignDetails";
import { Campaigns } from "./pages/Campaigns";
import { Home } from "./pages/Home";
import { MyRequests } from "./pages/MyRequests";

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

  return (
    <main className="app-shell bg-soft-radial">
      {route.path === "/" && <Home />}
      {route.path === "/profile" && <BloggerProfileForm />}
      {route.path === "/business" && <BusinessProfileForm />}
      {route.path === "/search" && <BloggerSearch />}
      {route.path === "/blogger" && route.id && <BloggerDetails id={route.id} />}
      {route.path === "/campaigns" && <Campaigns />}
      {route.path === "/campaign" && route.id && <CampaignDetails id={route.id} />}
      {route.path === "/requests" && <MyRequests />}
      {route.path === "/admin" && <Admin />}
      {!["/", "/profile", "/business", "/search", "/blogger", "/campaigns", "/campaign", "/requests", "/admin"].includes(route.path) && <Home />}
    </main>
  );
}
