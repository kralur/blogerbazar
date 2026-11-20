import { useState } from "react";
import { verifyMyPlatform, type MyPlatform, type PlatformVerificationResult } from "../api/marketplace";
import { getApiErrorMessage } from "../api/client";
import { useI18n } from "../i18n";
import { formatCompactNumber, formatShortDate } from "../lib/currency";
import { Button, Card, Icon } from "./ui";

// D68: a blogger proves a Telegram or YouTube channel; its followers then come from the platform and companies see
// "Confirmed". Each answer says exactly what to do next, step by step.
const verifiable = (platform: MyPlatform) => ["telegram", "youtube"].includes(platform.type.toLowerCase());

export function PlatformVerification({ platforms, onVerified }: { platforms: MyPlatform[]; onVerified: () => void }) {
  const { t } = useI18n();
  const channels = platforms.filter(verifiable);
  if (channels.length === 0) return null;
  return <section className="detail-section platform-verification" aria-labelledby="platform-verification-title">
    <div className="detail-section__heading"><h2 id="platform-verification-title">{t("verify.title")}</h2></div>
    <p className="platform-verification__hint">{t("verify.hint")}</p>
    <div className="grid gap-3">{channels.map((platform) => <PlatformRow key={platform.id} onVerified={onVerified} platform={platform} />)}</div>
  </section>;
}

function PlatformRow({ platform, onVerified }: { platform: MyPlatform; onVerified: () => void }) {
  const { language, t } = useI18n();
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<PlatformVerificationResult | null>(null);
  const [error, setError] = useState("");
  const [copied, setCopied] = useState(false);
  const type = platform.type.toLowerCase() as "telegram" | "youtube";
  const shown = result?.platform ?? platform;
  const verified = Boolean(shown.verificationSource);

  const check = async () => {
    setBusy(true);
    setError("");
    try {
      const answer = await verifyMyPlatform(platform.id);
      setResult(answer);
      if (answer.outcome === "verified") onVerified();
    } catch (failure) {
      setError(getApiErrorMessage(failure, t("verify.failed")));
    } finally {
      setBusy(false);
    }
  };
  const copy = async (code: string) => {
    try { await navigator.clipboard.writeText(code); setCopied(true); } catch { setCopied(false); }
  };
  const outcome = result?.outcome;

  return <Card className="platform-verification__row">
    <div className="platform-verification__head">
      <span className="platform-verification__name"><Icon className="h-4 w-4" name={type} />{t(`verify.platform.${type}`)}</span>
      <span className="platform-verification__url">{platform.url.replace(/^https?:\/\/(www\.)?/, "")}</span>
    </div>
    {verified
      ? <p className="platform-verification__done" role="status"><Icon className="h-4 w-4" name="check" />{t("verify.verifiedLine", { source: t(`verify.platform.${type}`), followers: formatCompactNumber(shown.followers ?? 0), date: shown.verifiedAtUtc ? formatShortDate(shown.verifiedAtUtc, language) : "" })}</p>
      : <p className="platform-verification__todo">{t(`verify.todo.${type}`)}</p>}
    {outcome && outcome !== "verified" && <div className="platform-verification__steps" role="status">
      {outcome === "botNotAdmin" && <><strong>{t("verify.botNotAdmin.title")}</strong><ol>
        <li>{t("verify.botNotAdmin.step1")}</li>
        <li>{t("verify.botNotAdmin.step2", { bot: result?.botUsername ? `@${result.botUsername}` : t("verify.ourBot") })}</li>
        <li>{t("verify.botNotAdmin.step3")}</li>
      </ol></>}
      {outcome === "codeMissing" && result?.code && <><strong>{t("verify.code.title")}</strong><ol>
        <li>{t("verify.code.step1")}</li>
        <li>{t("verify.code.step2")} <span className="platform-verification__code">{result.code}</span> <button className="platform-verification__copy" onClick={() => void copy(result.code!)} type="button">{t(copied ? "verify.code.copied" : "verify.code.copy")}</button></li>
        <li>{t("verify.code.step3")}</li>
      </ol><p>{t("verify.code.after")}</p></>}
      {outcome === "notOwner" && <p>{t("verify.notOwner")}</p>}
      {outcome === "notFound" && <p>{t(`verify.notFound.${type}`)}</p>}
      {outcome === "notConfigured" && <p>{t("verify.notConfigured")}</p>}
      {outcome === "unavailable" && <p>{t("verify.unavailable")}</p>}
    </div>}
    {error && <p className="platform-verification__error" role="alert">{error}</p>}
    <Button aria-busy={busy} className="platform-verification__button" disabled={busy} onClick={() => void check()} type="button" variant={verified ? "secondary" : undefined}>
      {busy ? t("verify.checking") : verified ? t("verify.refresh") : outcome ? t("verify.checkAgain") : t("verify.confirm")}
    </Button>
  </Card>;
}
