import { useCallback, useEffect, useRef, useState } from "react";
import { getBloggerReviews, getBrandFaceReviews, getBusinessReviews, type BloggerDetails, type BloggerReview, type BrandFaceDetails, type PublicBusinessProfile } from "../api/marketplace";
import { ReviewList } from "../components/details/DetailBlocks";
import { PageHeader } from "../components/PageHeader";
import { BottomNav, Button, ErrorState, LoadingState } from "../components/ui";
import { getCachedPublicDetail } from "../data/publicDetailCache";
import { useI18n } from "../i18n";
import { formatRating } from "../lib/currency";
import { profileRoute } from "../lib/profileRoutes";

const pageSize = 20;

// Every published review of a blogger, a brand face or a business, newest first, loaded page by page.
export function AllReviews({ target, id }: { target: "blogger" | "brandFace" | "business"; id: string }) {
  const { t } = useI18n();
  const cached = target === "blogger" ? getCachedPublicDetail<BloggerDetails>("blogger", id) : target === "business" ? getCachedPublicDetail<PublicBusinessProfile>("business", id) : null;
  const cachedName = cached?.name ?? (target === "brandFace" ? getCachedPublicDetail<BrandFaceDetails>("brand-face", id)?.name : undefined);
  const [reviews, setReviews] = useState<BloggerReview[]>([]);
  const [summary, setSummary] = useState<{ rating?: number | null; count: number } | null>(() => cached ? { rating: cached.rating, count: cached.reviewsCount } : null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [failed, setFailed] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const requestIdRef = useRef(0);

  const loadPage = useCallback(async (skip: number) => {
    if (target === "blogger") return { items: await getBloggerReviews(id, { skip, take: pageSize }) };
    const page = target === "brandFace" ? await getBrandFaceReviews(id, undefined, { skip, take: pageSize }) : await getBusinessReviews(id, undefined, { skip, take: pageSize });
    return { items: page.items, rating: page.rating, count: page.reviewsCount };
  }, [id, target]);

  const load = useCallback((skip: number) => {
    const requestId = ++requestIdRef.current;
    if (skip === 0) setLoading(true); else setLoadingMore(true);
    setFailed(false);
    loadPage(skip).then((page) => {
      if (requestId !== requestIdRef.current) return;
      setReviews((current) => skip === 0 ? page.items : [...current, ...page.items.filter((item) => !current.some((existing) => existing.id === item.id))]);
      setHasMore(page.items.length === pageSize);
      if (page.count !== undefined) setSummary({ rating: page.rating, count: page.count });
    }).catch(() => {
      if (requestId === requestIdRef.current) setFailed(true);
    }).finally(() => {
      if (requestId !== requestIdRef.current) return;
      setLoading(false);
      setLoadingMore(false);
    });
  }, [loadPage]);

  useEffect(() => {
    load(0);
    return () => { requestIdRef.current += 1; };
  }, [load]);

  const title = target === "business" ? t("campaign.businessReviews") : t("details.reviews");
  const back = { href: profileRoute(target, id), label: t("common.back") };
  if (loading) return <div className="screen screen--with-nav"><PageHeader back={back} title={title} /><LoadingState /><BottomNav /></div>;
  if (failed && reviews.length === 0) return <div className="screen screen--with-nav"><PageHeader back={back} title={title} /><ErrorState onRetry={() => load(0)} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;
  return <div className="screen screen--with-nav">
    <PageHeader back={back} eyebrow={cachedName} title={title} />
    {summary && summary.count > 0 && <p className="all-reviews__summary">{summary.rating != null && <><span aria-hidden="true" className="text-brand-warning">★</span> {formatRating(summary.rating)} · </>}{t("common.reviews", { count: summary.count })}</p>}
    <div className="mt-4"><ReviewList emptyText={target === "business" ? t("campaign.noBusinessReviews") : t("details.noReviews")} reviewerRoute={(profileId, role) => target === "business" ? profileRoute(role ?? "blogger", profileId) : `#/company/${profileId}`} reviews={reviews} /></div>
    {failed && <p className="mt-3 text-sm text-brand-muted" role="status">{t("common.connectionRetry")}</p>}
    {hasMore && <Button aria-busy={loadingMore} className="mt-4 w-full" disabled={loadingMore} onClick={() => load(reviews.length)} type="button" variant="secondary">{t("applications.loadMore")}</Button>}
    <BottomNav />
  </div>;
}
