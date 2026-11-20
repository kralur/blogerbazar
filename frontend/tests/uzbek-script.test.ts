import { describe, expect, it } from "vitest";
import { toNewUzbekLatin } from "@bloggerbazar/shared/uzbekScript";
import { loadLanguage, setUzbekScript, translate } from "../src/i18n";

describe("new Uzbek alphabet (2026)", () => {

  it("writes Oʻ, Gʻ, Sh, Ch as Ö, Ğ, Ş, Ç", () => {
    expect(toNewUzbekLatin("O‘zbekcha qo‘shish, bog‘lanish, Shahar, ochish, Chiqish")).toBe("Özbekça qöşiş, boğlaniş, Şahar, oçiş, Çiqiş");
  });

  it("keeps placeholders, links, handles, brands and the tutuq belgisi", () => {
    expect(toNewUzbekLatin("{followers} obunachi · qamrov {reach} · @shox_ch Chrome https://t.me/channel ma’lumot")).toBe("{followers} obunaçi · qamrov {reach} · @shox_ch Chrome https://t.me/channel ma’lumot");
  });

  it("is the default for the Uzbek interface, the old alphabet on request", async () => {
    await loadLanguage("uz");
    setUzbekScript("new");
    expect(translate("language.uzbek", undefined, "uz")).toBe("Özbekça");
    setUzbekScript("old");
    expect(translate("language.uzbek", undefined, "uz")).toBe("O‘zbekcha");
    // Values (a user's name) are never converted.
    setUzbekScript("new");
    expect(translate("team.requestsBadge", { count: 2 }, "uz")).toContain("2");
  });
});
