import { useState } from "react";
import { getApiErrorMessage } from "../api/client";
import { deleteCurrentAccount } from "../api/marketplace";
import { useI18n } from "../i18n";
import { themePreferenceChangedEvent } from "../lib/themePreference";
import { useTelegram } from "../telegram/TelegramProvider";
import { Button, Modal, Toast } from "./ui";

// Log out and delete account, with their confirmations. Both clear this device's BloggerBazar state.
export function AccountActions({ onSessionReset }: { onSessionReset?: () => void }) {
  const { haptic } = useTelegram();
  const { setLanguage, t } = useI18n();
  const [logoutOpen, setLogoutOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [toast, setToast] = useState("");

  const resetLocalSession = () => {
    [localStorage, sessionStorage].forEach((storage) => {
      for (let index = storage.length - 1; index >= 0; index -= 1) {
        const key = storage.key(index);
        if (key?.startsWith("bloggerbazar.")) storage.removeItem(key);
      }
    });
    setLanguage("ru");
    window.dispatchEvent(new Event(themePreferenceChangedEvent));
    setLogoutOpen(false);
    setDeleteOpen(false);
    onSessionReset?.();
  };

  const requestAccountDeletion = async () => {
    if (deleting) return;
    setDeleting(true);
    setToast("");
    try {
      await deleteCurrentAccount();
      haptic.success();
      resetLocalSession();
    } catch (error) {
      haptic.error();
      setToast(getApiErrorMessage(error, t("profile.deleteFailed")));
    } finally {
      setDeleting(false);
    }
  };

  return <>
    <div className="grid gap-2"><Button className="w-full" onClick={() => { haptic.warning(); setLogoutOpen(true); }} type="button" variant="secondary">{t("profile.logout")}</Button><button className="profile-delete-link" onClick={() => { haptic.warning(); setDeleteOpen(true); }} type="button">{t("profile.deleteAccount")}</button></div>
    <Modal onClose={() => setLogoutOpen(false)} open={logoutOpen} title={t("profile.logoutTitle")}><p className="text-sm leading-6 text-brand-muted">{t("profile.logoutDescription")}</p><div className="mt-5 grid grid-cols-2 gap-3"><Button onClick={() => setLogoutOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button onClick={resetLocalSession} type="button" variant="danger">{t("profile.logout")}</Button></div></Modal>
    <Modal onClose={() => { if (!deleting) setDeleteOpen(false); }} open={deleteOpen} title={t("profile.deleteAccountTitle")}><p className="text-sm leading-6 text-brand-muted">{t("profile.deleteAccountDescription")}</p><div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={deleting} onClick={() => setDeleteOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button aria-busy={deleting} disabled={deleting} onClick={() => void requestAccountDeletion()} type="button" variant="danger">{deleting ? t("profile.deleting") : t("profile.deleteAccount")}</Button></div></Modal>
    <Toast message={toast} tone="error" />
  </>;
}
