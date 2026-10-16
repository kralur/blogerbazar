// Post-deploy check of the web admin (D57): node scripts/check-admin-web-deploy.mjs https://<admin site>
// Read only apart from 7 failing sign-in attempts that use up the sign-in limit of the address it runs from for a
// minute. Run it from a network with one outgoing address (a proxy that changes address per connection makes every
// attempt look like a new person, and the limit check then fails falsely). Checks the security headers and CSP on the pages, robots.txt, that the API answers through the site, that
// the sign-in limit (5 a minute) works, and that a forged X-Forwarded-For does not get around it.
const base = (process.argv[2] ?? "").replace(/\/+$/, "");
if (!/^https:\/\//.test(base)) {
  console.error("Usage: node scripts/check-admin-web-deploy.mjs https://<admin site>");
  process.exit(2);
}

let failed = 0;
const check = (ok, name, detail = "") => {
  if (!ok) failed++;
  console.log(`${ok ? "ok  " : "FAIL"} ${name}${detail ? ` (${detail})` : ""}`);
};

for (const path of ["/", "/login", "/users"]) {
  const response = await fetch(base + path, { redirect: "manual" });
  const csp = response.headers.get("content-security-policy") ?? "";
  check(response.status === 200, `${path} answers`, String(response.status));
  check(/default-src 'none'|default-src 'self'/.test(csp) && /frame-ancestors 'none'/.test(csp), `${path} has a strict CSP`);
  check(!/unsafe-inline|unsafe-eval/.test(csp.replace(/style-src[^;]*/, "")), `${path} CSP allows no inline or eval scripts`);
  check(path === "/login" ? csp.includes("telegram.org") : !csp.includes("telegram.org"), `${path} allows Telegram only on the sign-in page`);
  check(/max-age=\d{6,}/.test(response.headers.get("strict-transport-security") ?? ""), `${path} sends HSTS`);
  check(response.headers.get("x-content-type-options") === "nosniff", `${path} sends nosniff`);
  check(response.headers.get("referrer-policy") === "no-referrer", `${path} sends no-referrer`);
  check(/noindex/.test(response.headers.get("x-robots-tag") ?? ""), `${path} is not indexed`);
}

const robots = await fetch(`${base}/robots.txt`);
check(robots.ok && /Disallow:\s*\//.test(await robots.text()), "robots.txt closes the site");

const config = await fetch(`${base}/api/admin-web/config`);
check(config.status === 200, "the API answers through the site", String(config.status));
const session = await fetch(`${base}/api/admin-web/session`);
check(session.status === 401, "no session without the cookie", String(session.status));
const foreign = await fetch(`${base}/api/admin-web/session`, { method: "POST", headers: { "Content-Type": "application/json", Origin: "https://evil.example" }, body: "{}" });
check(foreign.status === 403, "sign-in from another site is refused", String(foreign.status));

// Six attempts in a row: the sixth must hit the limit even though every request names a different client address.
const statuses = [];
for (let attempt = 1; attempt <= 6; attempt++) {
  const response = await fetch(`${base}/api/admin-web/session`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Origin: base, "X-Forwarded-For": `203.0.113.${attempt}` },
    body: JSON.stringify({ id: "1", auth_date: "1", hash: "0".repeat(64) })
  });
  statuses.push(response.status);
}
check(statuses.at(-1) === 429, "sign-in limit holds against a forged X-Forwarded-For", statuses.join(","));

console.log(failed ? `${failed} check(s) failed` : "all checks passed");
process.exit(failed ? 1 : 0);
