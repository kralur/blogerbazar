import { useEffect, useState } from "react";
import { sessionExpiredEvent } from "../api/client";
import { useI18n } from "../i18n";

// Shown once any request comes back 401: the fix is to reopen the app, not "access denied".
export function SessionExpiredNotice() {
  const { t } = useI18n();
  const [expired, setExpired] = useState(false);
  useEffect(() => {
    const show = () => setExpired(true);
    window.addEventListener(sessionExpiredEvent, show);
    return () => window.removeEventListener(sessionExpiredEvent, show);
  }, []);
  if (!expired) return null;
  return <div className="session-expired" role="alert">{t("error.session_expired")}</div>;
}
