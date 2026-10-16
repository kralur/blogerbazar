import { recordExport, type ExportKind } from "../api/admin";
import { useI18n } from "../i18n";
import { toCsv, saveFile, type CsvCell } from "../lib/csv";
import { tashkentDate } from "../lib/format";
import { useActions } from "./Actions";
import { Button } from "./ui";

// Lists with people's names, usernames, phones or Telegram ids; the same set as on the server.
const personal: ExportKind[] = ["users", "deals", "complaints", "journal"];

export type ExportTable = { head: string[]; rows: CsvCell[][]; personalColumns?: number[] };

// "Download CSV" for the table on screen. The journal entry comes first; for people's data the admin confirms a
// warning, the file name says "personal-data" and those columns are marked in the header.
export function ExportButton({ kind, table }: { kind: ExportKind; table: () => ExportTable }) {
  const { t } = useI18n();
  const actions = useActions(() => undefined);
  const isPersonal = personal.includes(kind);

  const download = async () => {
    const { head, rows, personalColumns = [] } = table();
    await recordExport(kind, rows.length);
    const marked = head.map((name, index) => personalColumns.includes(index) ? `${name} ${t("export.personalMark")}` : name);
    saveFile(`bloggerbazar-${kind}${isPersonal ? "-personal-data" : ""}-${tashkentDate()}.csv`, toCsv(marked, rows));
  };

  const start = () => actions.ask({
    title: isPersonal ? t("export.personalTitle") : t("export.title"),
    text: isPersonal ? t("export.personalText", { count: table().rows.length }) : t("export.text", { count: table().rows.length }),
    confirmLabel: t("export.download"),
    run: download,
    done: t("export.done")
  });

  return (
    <>
      <Button onClick={start}>{t("export.button")}</Button>
      {actions.dialog}
      {actions.status && <div className="basis-full">{actions.status}</div>}
    </>
  );
}
