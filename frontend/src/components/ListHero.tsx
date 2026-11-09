import type { ReactNode } from "react";
import { AtlasStrip } from "./AtlasStrip";

// The lapis head of a list screen (D63, package B): the page header, tabs, search and filters sit on lapis under
// the Telegram chrome, and the atlas strip closes it, as on Home. Results scroll below on the page background.
export function ListHero({ children, className }: { children: ReactNode; className?: string }) {
  return <>
    <div className={`list-hero ${className ?? ""}`.trim()}>{children}</div>
    <AtlasStrip />
  </>;
}
