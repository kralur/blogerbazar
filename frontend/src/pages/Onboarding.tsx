import { useState } from "react";
import { selectMarketplaceRole, type MarketplaceRole } from "../api/marketplace";
import { Button, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";

const roleRoutes: Record<MarketplaceRole, string> = {
  Blogger: "/blogger-form",
  BrandFace: "/brand-face",
  Business: "/business"
};

export function Onboarding({ onRoleSelected }: { onRoleSelected?: (role: MarketplaceRole) => void }) {
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const [selectedRole, setSelectedRole] = useState<MarketplaceRole>("Blogger");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const roles: Array<{ role: MarketplaceRole; title: string; description: string }> = [
    { role: "BrandFace", title: t("onboarding.brandFace"), description: t("onboarding.brandFaceDescription") },
    { role: "Blogger", title: t("onboarding.blogger"), description: t("onboarding.bloggerDescription") },
    { role: "Business", title: t("onboarding.business"), description: t("onboarding.businessDescription") }
  ];

  const saveRole = async () => {
    try {
      setSaving(true);
      setError("");
      await selectMarketplaceRole(selectedRole);
      window.dispatchEvent(new Event("bloggerbazar:role-selected"));
      if (onRoleSelected) onRoleSelected(selectedRole);
      else window.location.hash = roleRoutes[selectedRole];
    } catch {
      setError(t("onboarding.selectionFailed"));
    } finally {
      setSaving(false);
    }
  };

  return <div className="screen flex flex-col px-5 pt-10">
    <p className="text-sm font-bold text-brand-blue">BloggerBazar</p>
    <h1 className="mt-3 text-3xl font-extrabold tracking-tight">{t("onboarding.title")}</h1>
    <p className="mt-3 text-sm leading-6 text-brand-muted">{t("onboarding.subtitle")}</p>
    <div className="mt-7 grid gap-3">
      {roles.map((item) => <button aria-pressed={selectedRole === item.role} className={`rounded-3xl border p-5 text-left transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-blue ${selectedRole === item.role ? "border-brand-blue bg-blue-50 shadow-card" : "border-brand-line bg-white"}`} key={item.role} onClick={() => { haptic.selection(); setSelectedRole(item.role); }} type="button">
        <p className="text-lg font-extrabold">{item.title}</p><p className="mt-1 text-sm leading-5 text-brand-muted">{item.description}</p>
      </button>)}
    </div>
    <Button className="mt-auto mb-8 w-full" disabled={saving} onClick={saveRole} type="button">{saving ? t("common.loading") : t("onboarding.selectRole")}</Button>
    <Toast message={error} tone="error" />
  </div>;
}
