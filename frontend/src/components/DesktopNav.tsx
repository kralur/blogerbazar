import { useCallback, useEffect, useState } from "react";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { getMyBusinessProfile } from "../api/marketplace";
import { ActionBadge, useActionCounts } from "../features/actionCounts/ActionCountsProvider";
import { useI18n } from "../i18n";
import { Icon, isNavItemActive } from "./ui";

// D64 stage B2: the computer's side menu, in place of the phone's bottom bar. Always there, also on detail pages,
// with the company the person works for.
export function DesktopNav() {
  const { t } = useI18n();
  const actionCounts = useActionCounts();
  const [hash, setHash] = useState(() => window.location.hash || "#/");
  const [company, setCompany] = useState<string | null>(null);

  useEffect(() => {
    const handler = () => setHash(window.location.hash || "#/");
    window.addEventListener("hashchange", handler);
    return () => window.removeEventListener("hashchange", handler);
  }, []);
  // The company's name; read again when a profile changes (a renamed company shows its new name).
  const [profileVersion, setProfileVersion] = useState(0);
  useProfileDataRefresh(useCallback(() => setProfileVersion((value) => value + 1), []));
  useEffect(() => {
    let active = true;
    getMyBusinessProfile().then((profile) => { if (active) setCompany(profile.name); }).catch(() => undefined);
    return () => { active = false; };
    // Until it is known, try again on the next page change (a request that failed at sign-in left the line blank).
  }, [profileVersion, company === null ? hash : ""]);

  const main = [
    { href: "#/", label: t("nav.home"), icon: "home" },
    { href: "#/search", label: t("nav.search"), icon: "search" },
    { href: "#/campaigns", label: t("nav.campaigns"), icon: "send" },
    { href: "#/requests", label: t("nav.requests"), icon: "briefcase" },
    { href: "#/profile", label: t("nav.profile"), icon: "user" }
  ];
  const isActive = (href: string) => href === "#/profile" ? isNavItemActive(href, hash) && !hash.startsWith("#/settings") && !hash.startsWith("#/favorites") && !hash.startsWith("#/team") : isNavItemActive(href, hash);

  return <nav aria-label={t("nav.aria")} className="desktop-nav">
    <a className="desktop-nav__brand" href="#/">{t("common.appName")}</a>
    <p aria-hidden={company ? undefined : true} className="desktop-nav__company">{company ?? "\u00a0"}</p>
    <ul className="desktop-nav__items">
      {main.map((item) => <li key={item.href}>
        <a aria-current={isActive(item.href) ? "page" : undefined} className="desktop-nav__item" href={item.href}>
          <Icon className="h-5 w-5" name={item.icon} />
          <span>{item.label}</span>
          {item.href === "#/requests" && <ActionBadge count={actionCounts.total} label={t("requests.actionBadge", { count: actionCounts.total })} />}
        </a>
      </li>)}
    </ul>
    <ul className="desktop-nav__items desktop-nav__items--secondary">
      <li><a aria-current={hash.startsWith("#/team") ? "page" : undefined} className="desktop-nav__item" href="#/team"><Icon className="h-5 w-5" name="users" /><span>{t("team.title")}</span></a></li>
      <li><a aria-current={hash.startsWith("#/favorites") ? "page" : undefined} className="desktop-nav__item" href="#/favorites"><Icon className="h-5 w-5" name="heart" /><span>{t("favorites.profileTitle")}</span></a></li>
      <li><a aria-current={hash.startsWith("#/settings") ? "page" : undefined} className="desktop-nav__item" href="#/settings"><Icon className="h-5 w-5" name="settings" /><span>{t("profile.settings")}</span></a></li>
    </ul>
  </nav>;
}
