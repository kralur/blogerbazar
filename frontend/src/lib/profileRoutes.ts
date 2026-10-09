// The public page of a marketplace profile by its kind; the backend names the kind with a fixed camelCase value (D46).
export type ProfileKind = "blogger" | "brandFace" | "business";

export function profileRoute(kind: ProfileKind | string | null | undefined, id: string): string {
  if (kind === "brandFace") return `#/brand-face-detail/${id}`;
  if (kind === "business") return `#/company/${id}`;
  return `#/blogger/${id}`;
}
