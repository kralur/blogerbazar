import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

// Security B4: the policy in nginx.conf allows the inline theme script of index.html by its hash. Editing that script
// without updating the hash would be blocked once the policy is strict, so this test fails first.
describe("Content-Security-Policy", () => {
  it("allows exactly the inline script that index.html has", () => {
    const html = readFileSync(resolve(process.cwd(), "index.html"), "utf8");
    const nginx = readFileSync(resolve(process.cwd(), "nginx.conf"), "utf8");
    const inline = [...html.matchAll(/<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)<\/script>/g)].map((match) => match[1]);

    expect(inline).toHaveLength(1);
    const hash = `'sha256-${createHash("sha256").update(inline[0]).digest("base64")}'`;
    expect(nginx).toContain(hash);
  });

  it("only reports for now and never lets other sites frame the app", () => {
    const nginx = readFileSync(resolve(process.cwd(), "nginx.conf"), "utf8");

    expect(nginx).toContain("Content-Security-Policy-Report-Only");
    expect(nginx).toMatch(/frame-ancestors 'self' https:\/\/web\.telegram\.org https:\/\/\*\.telegram\.org;/);
    expect(nginx).toContain("object-src 'none'");
  });
});
