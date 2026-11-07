// A full-screen atlas (abrband) behind the first-run screens (owner, 2026-11-07: variant 1, the crisp one): two
// staggered rows of the AtlasStrip flames under a lapis veil that gets denser towards the bottom, so the white text
// and the cards stay readable. Decorative only.
export function AtlasBackground() {
  const flame = (x: number, outer: string, middle: string, core: string) => <>
    <path d={`M${x} 0 C${x + 12} 10 ${x + 16} 20 ${x + 16} 24 C${x + 16} 28 ${x + 12} 38 ${x} 48 C${x - 12} 38 ${x - 16} 28 ${x - 16} 24 C${x - 16} 20 ${x - 12} 10 ${x} 0 Z`} fill={outer} />
    <path d={`M${x} 8 C${x + 7} 15 ${x + 9} 20 ${x + 9} 24 C${x + 9} 28 ${x + 7} 33 ${x} 40 C${x - 7} 33 ${x - 9} 28 ${x - 9} 24 C${x - 9} 20 ${x - 7} 15 ${x} 8 Z`} fill={middle} />
    <path d={`M${x} 17 C${x + 3} 20 ${x + 4} 22 ${x + 4} 24 C${x + 4} 26 ${x + 3} 28 ${x} 31 C${x - 3} 28 ${x - 4} 26 ${x - 4} 24 C${x - 4} 22 ${x - 3} 20 ${x} 17 Z`} fill={core} />
  </>;
  const row = (dx: number) => <>
    {flame(28 + dx, "var(--bb-saffron)", "var(--bb-ink)", "#FFFFFF")}
    {flame(88 + dx, "var(--bb-accent)", "var(--bb-tile)", "var(--bb-ink)")}
    {flame(148 + dx, "var(--bb-coral)", "#FFFFFF", "var(--bb-lapis)")}
    {flame(dx - 28, "var(--bb-coral)", "#FFFFFF", "var(--bb-lapis)")}
    <rect x={2 + dx} width="3" height="48" fill="var(--bb-tile)" />
    <rect x={57 + dx} width="3" height="48" fill="var(--bb-accent)" />
    <rect x={117 + dx} width="3" height="48" fill="var(--bb-saffron)" />
  </>;
  return (
    <div aria-hidden="true" className="atlas-background">
      <svg preserveAspectRatio="none">
        <defs>
          <filter id="atlas-bg-feather" x="-5%" y="-10%" width="110%" height="120%">
            <feTurbulence type="fractalNoise" baseFrequency=".015 .7" numOctaves={3} seed={7} result="noise" />
            <feDisplacementMap in="SourceGraphic" in2="noise" scale={5} xChannelSelector="R" yChannelSelector="G" />
          </filter>
          <pattern id="atlas-bg-motif" width="176" height="96" patternUnits="userSpaceOnUse">
            <rect width="176" height="96" fill="var(--bb-lapis)" />
            <g>{row(0)}</g>
            <g transform="translate(0 48)">{row(30)}</g>
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill="url(#atlas-bg-motif)" filter="url(#atlas-bg-feather)" />
      </svg>
      <div className="atlas-background__veil" />
    </div>
  );
}
