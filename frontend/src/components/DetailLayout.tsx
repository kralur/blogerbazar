import type { ReactNode } from "react";
import { useIsDesktop } from "../lib/desktopLayout";

// A page's content below its head. On the phone everything stays in one column, in this order: top, the main
// content, side. On a computer (the website, D64 stage B2) the main content takes the left column, and actions + top +
// side make a panel on the right that stays in view while the page scrolls: the page's buttons are always at hand and
// the screen has no empty half. actions exist only on a computer; on the phone the page shows them its own way (for
// example in the bar at the bottom).
// footer (the quiet "Report" link) closes the page on the phone and the left column on a computer.
export function DetailLayout({ top, side, actions, footer, children, className }: { top?: ReactNode; side?: ReactNode; actions?: ReactNode; footer?: ReactNode; children: ReactNode; className?: string }) {
  const desktop = useIsDesktop();
  if (!desktop) return <>{top}{children}{side}{footer}</>;
  return <div className={`detail-layout${className ? ` ${className}` : ""}`}>
    <div className="detail-layout__main">{children}{footer}</div>
    <aside className="detail-layout__side">{actions && <div className="detail-layout__actions">{actions}</div>}{top}{side}</aside>
  </div>;
}
