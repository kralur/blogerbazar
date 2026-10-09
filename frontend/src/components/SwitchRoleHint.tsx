import { useEffect, useState } from "react";
import { getCurrentPlatformUser, getMyBloggerProfile, getMyBusinessProfile, normalizeMarketplaceRole, selectMarketplaceRole, type MarketplaceRole } from "../api/marketplace";
import { useI18n } from "../i18n";
import { Button } from "./ui";

// Offers and deals open only under the role they belong to (selected-role rule). A bot button can point to an
// item of another of the person's profiles; instead of a dead end, offer one tap to switch to that role.
export function SwitchRoleHint() {
  const { t } = useI18n();
  const [roles, setRoles] = useState<MarketplaceRole[]>([]);
  const [switching, setSwitching] = useState(false);

  useEffect(() => {
    let active = true;
    Promise.allSettled([getCurrentPlatformUser(), getMyBloggerProfile(), getMyBusinessProfile()]).then(([user, blogger, business]) => {
      if (!active || user.status !== "fulfilled") return;
      const current = normalizeMarketplaceRole(user.value.selectedMarketplaceRole);
      const owned: MarketplaceRole[] = [];
      if (blogger.status === "fulfilled" && blogger.value) owned.push("Blogger");
      if (business.status === "fulfilled" && business.value) owned.push("Business");
      setRoles(owned.filter((role) => role !== current));
    });
    return () => { active = false; };
  }, []);

  const switchTo = async (role: MarketplaceRole) => {
    if (switching) return;
    setSwitching(true);
    try {
      await selectMarketplaceRole(role);
      // A fresh start reloads every screen and cache for the new role and reopens this same link.
      window.location.reload();
    } catch {
      setSwitching(false);
    }
  };

  if (roles.length === 0) return null;
  return <div className="switch-role-hint">
    <p>{t("roleSwitch.hint")}</p>
    {roles.map((role) => <Button aria-busy={switching} className="w-full" disabled={switching} key={role} onClick={() => void switchTo(role)} type="button" variant="secondary">{t("roleSwitch.action", { role: t(role === "Blogger" ? "profile.blogger" : "profile.business") })}</Button>)}
  </div>;
}
