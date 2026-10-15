import { api } from "../api/client";

// Security B4: the server sends a report-only Content-Security-Policy; whatever it would block is sent here once per
// kind, so the admin can see it before the policy is made strict. Up to 20 reports a session, never blocking anything.
const maxReports = 20;
const sent = new Set<string>();

export function installCspReporter() {
  if (typeof document === "undefined") return;
  document.addEventListener("securitypolicyviolation", (event) => {
    const report = { directive: event.effectiveDirective || event.violatedDirective, blocked: event.blockedURI, source: event.sourceFile };
    const key = `${report.directive}|${report.blocked}|${report.source}`;
    if (sent.has(key) || sent.size >= maxReports) return;
    sent.add(key);
    void api("/api/csp-reports", { method: "POST", body: JSON.stringify(report) }).catch(() => undefined);
  });
}
