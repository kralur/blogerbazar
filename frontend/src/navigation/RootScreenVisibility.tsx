import { createContext, useContext, type ReactNode } from "react";

const RootScreenVisibilityContext = createContext(true);

export function RootScreenVisibility({ active, children }: { active: boolean; children: ReactNode }) {
  return <RootScreenVisibilityContext.Provider value={active}><div aria-hidden={!active} hidden={!active}>{children}</div></RootScreenVisibilityContext.Provider>;
}

export function useRootScreenVisibility() {
  return useContext(RootScreenVisibilityContext);
}
