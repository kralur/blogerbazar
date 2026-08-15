import { useState } from "react";
import { selectMarketplaceRole, type MarketplaceRole } from "../api/marketplace";
import { Button, Icon, Toast } from "../components/ui";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
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
  const [selectedRole, setSelectedRole] = useState<MarketplaceRole | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const roles: Array<{ role: MarketplaceRole; title: string; description: string; icon: string }> = [
    { role: "Blogger", title: t("onboarding.blogger"), description: t("onboarding.bloggerDescription"), icon: "chart" },
    { role: "BrandFace", title: t("onboarding.brandFace"), description: t("onboarding.brandFaceDescription"), icon: "users" },
    { role: "Business", title: t("onboarding.business"), description: t("onboarding.businessDescription"), icon: "building" }
  ];

  const saveRole = async () => {
    if (!selectedRole || saving) return;

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

  return <main className="ftue-screen ftue-role-selection">
    <div className="ftue-screen__layout">
      <div className="ftue-role-selection__header" data-content-header>
        <div className="ftue-role-selection__language"><LanguageSwitcher /></div>
        <h1 className="ftue-role-selection__title">{t("onboarding.title")}</h1>
        <p className="ftue-role-selection__subtitle">{t("onboarding.subtitle")}</p>
      </div>

      <div aria-label={t("onboarding.title")} className="ftue-role-selection__list" role="radiogroup">
        {roles.map((item) => {
          const selected = selectedRole === item.role;
          return <button
            aria-checked={selected}
            aria-label={`${item.title}. ${item.description}`}
            className={`ftue-role-card${selected ? " ftue-role-card--selected" : ""}`}
            key={item.role}
            onClick={() => { haptic.selection(); setSelectedRole(item.role); }}
            role="radio"
            type="button"
          >
            <span aria-hidden="true" className="ftue-role-card__icon"><Icon name={item.icon} /></span>
            <span className="ftue-role-card__copy"><span className="ftue-role-card__title">{item.title}</span><span className="ftue-role-card__description">{item.description}</span></span>
            <span aria-hidden="true" className="ftue-role-card__check"><Icon name="check" /></span>
          </button>;
        })}
      </div>

      <Button className="ftue-primary-button ftue-role-selection__cta w-full" disabled={!selectedRole || saving} onClick={saveRole} type="button" variant="secondary">{saving ? t("common.loading") : t("onboarding.selectRole")}</Button>
    </div>
    <Toast message={error} tone="error" />
  </main>;
}
