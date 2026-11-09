import { useEffect, useId, useRef, type CSSProperties } from "react";
import { pluckForTile, warmAtlasSound } from "../lib/atlasSound";

// The atlas (abrband) strip under the Home hero (D62): three flames with three-layer cores in the bazaar colours on
// lapis, drawn at 176x48 and scaled to the strip height, with a light feathering filter so the edges read as weave
// rather than vector. The strip scrolls sideways under the finger (owner, 2026-11-07) and drifts slowly by itself;
// vertical swipes still scroll the page because the browser picks the pan axis from the first move. The track is
// three screens wide and starts in the middle; when the finger nears an end it jumps back by whole tiles, which is
// invisible because the pattern repeats every tile. Decorative only.
// `fixed` draws one screen width with no scroll and no drift (the previews in Settings).
export function AtlasStrip({ fixed = false, height = 26 }: { fixed?: boolean; height?: number }) {
  const scale = height / 48;
  const tile = 176 * scale;
  const ref = useRef<HTMLDivElement>(null);
  // Two strips on one screen (the season previews) need their own filter and pattern ids.
  const id = useId().replace(/:/g, "");
  const featherId = `atlas-feather-${id}`;
  const motifId = `atlas-motif-${id}`;
  useEffect(() => {
    const el = ref.current;
    if (el && !fixed) el.scrollLeft = el.scrollWidth / 3;
  }, [fixed]);
  // Each tile the finger drags past answers with a selection tick and, when the sound is on, a plucked note
  // (owner, 2026-11-09). A recentre jump moves by several tiles at once and is not a tick.
  const lastTile = useRef<number | null>(null);
  const onScroll = () => {
    const el = ref.current;
    if (!el || tile <= 0) return;
    const index = Math.floor(el.scrollLeft / tile);
    if (lastTile.current !== null && Math.abs(index - lastTile.current) === 1) {
      (window as Window & { Telegram?: { WebApp?: { HapticFeedback?: { selectionChanged?: () => void } } } }).Telegram?.WebApp?.HapticFeedback?.selectionChanged?.();
      pluckForTile(index);
    }
    lastTile.current = index;
    const third = el.scrollWidth / 3;
    const jump = Math.floor(third / tile) * tile;
    if (jump <= 0) return;
    if (el.scrollLeft < tile) { el.scrollLeft += jump; lastTile.current = Math.floor(el.scrollLeft / tile); }
    else if (el.scrollLeft > el.scrollWidth - el.clientWidth - tile) { el.scrollLeft -= jump; lastTile.current = Math.floor(el.scrollLeft / tile); }
  };
  const flame = (x: number, outer: string, middle: string, core: string) => <>
    <path d={`M${x} 0 C${x + 12} 10 ${x + 16} 20 ${x + 16} 24 C${x + 16} 28 ${x + 12} 38 ${x} 48 C${x - 12} 38 ${x - 16} 28 ${x - 16} 24 C${x - 16} 20 ${x - 12} 10 ${x} 0 Z`} fill={outer} />
    <path d={`M${x} 8 C${x + 7} 15 ${x + 9} 20 ${x + 9} 24 C${x + 9} 28 ${x + 7} 33 ${x} 40 C${x - 7} 33 ${x - 9} 28 ${x - 9} 24 C${x - 9} 20 ${x - 7} 15 ${x} 8 Z`} fill={middle} />
    <path d={`M${x} 17 C${x + 3} 20 ${x + 4} 22 ${x + 4} 24 C${x + 4} 26 ${x + 3} 28 ${x} 31 C${x - 3} 28 ${x - 4} 26 ${x - 4} 24 C${x - 4} 22 ${x - 3} 20 ${x} 17 Z`} fill={core} />
  </>;
  return (
    <div aria-hidden="true" className={fixed ? "atlas-strip atlas-strip--fixed" : "atlas-strip"} onPointerDown={fixed ? undefined : warmAtlasSound} onScroll={fixed ? undefined : onScroll} ref={ref} style={{ height, "--atlas-tile": `${tile}px` } as CSSProperties}>
      <div className="atlas-strip__track" style={{ height }}>
        <svg height={height} preserveAspectRatio="none" style={{ width: fixed ? "100%" : `calc(100% + ${tile}px)` }}>
          <defs>
            <filter id={featherId} x="-5%" y="-10%" width="110%" height="120%">
              <feTurbulence type="fractalNoise" baseFrequency=".015 .7" numOctaves={3} seed={7} result="noise" />
              <feDisplacementMap in="SourceGraphic" in2="noise" scale={4} xChannelSelector="R" yChannelSelector="G" />
            </filter>
            <pattern id={motifId} width={tile} height={height} patternUnits="userSpaceOnUse">
              <g transform={`scale(${scale})`}>
                <rect width="176" height="48" fill="var(--bb-lapis)" />
                {flame(28, "var(--bb-atlas-flame-1)", "var(--bb-atlas-core-1)", "var(--bb-atlas-dot-1)")}
                {flame(88, "var(--bb-atlas-flame-2)", "var(--bb-atlas-core-2)", "var(--bb-atlas-dot-2)")}
                {flame(148, "var(--bb-atlas-flame-3)", "var(--bb-atlas-core-3)", "var(--bb-atlas-dot-3)")}
                <rect x="2" width="3" height="48" fill="var(--bb-atlas-thread-1)" />
                <rect x="57" width="3" height="48" fill="var(--bb-atlas-thread-2)" />
                <rect x="117" width="3" height="48" fill="var(--bb-atlas-thread-3)" />
              </g>
            </pattern>
          </defs>
          <rect width="100%" height={height} fill={`url(#${motifId})`} filter={`url(#${featherId})`} />
        </svg>
      </div>
    </div>
  );
}
