import type { OfferFormat, OfferState } from "../api/marketplace";

export const offerFormats: OfferFormat[] = ["stories", "reels", "post", "integration"];

export function offerFormatLabelKey(format: OfferFormat | null | undefined) {
  if (format === "stories") return "card.stories";
  if (format === "reels") return "card.reels";
  if (format === "post") return "card.post";
  if (format === "integration") return "card.integration";
  return "offers.formatAny";
}

export function offerStateLabelKey(state: OfferState) {
  if (state === "accepted") return "offers.state.accepted";
  if (state === "declined") return "offers.state.declined";
  if (state === "expired") return "offers.state.expired";
  return "offers.state.pending";
}

export function offerStateTone(state: OfferState) {
  if (state === "accepted") return "green" as const;
  if (state === "declined") return "red" as const;
  if (state === "expired") return "gray" as const;
  return "blue" as const;
}

export function offerRoute(id: string) {
  return `/offer/${id}`;
}
