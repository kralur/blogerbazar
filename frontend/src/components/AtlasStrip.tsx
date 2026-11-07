// The atlas (abrband) strip of the "Bazar" direction (D61, D62): three flame motifs with a three-layer core and thin
// threads between them, drawn at 176x48 and shown small. The same drawing lives in landing/src/App.tsx (Ikat); the
// landing is a separate package with its own colour tokens, so the SVG is repeated here on purpose.
export function AtlasStrip({ height = 26 }: { height?: number }) {
  const scale = height / 48;
  const flame = (x: number, outer: string, middle: string, core: string) => <>
    <path d={`M${x} 0 C${x + 12} 10 ${x + 16} 20 ${x + 16} 24 C${x + 16} 28 ${x + 12} 38 ${x} 48 C${x - 12} 38 ${x - 16} 28 ${x - 16} 24 C${x - 16} 20 ${x - 12} 10 ${x} 0 Z`} fill={outer} />
    <path d={`M${x} 8 C${x + 7} 15 ${x + 9} 20 ${x + 9} 24 C${x + 9} 28 ${x + 7} 33 ${x} 40 C${x - 7} 33 ${x - 9} 28 ${x - 9} 24 C${x - 9} 20 ${x - 7} 15 ${x} 8 Z`} fill={middle} />
    <path d={`M${x} 17 C${x + 3} 20 ${x + 4} 22 ${x + 4} 24 C${x + 4} 26 ${x + 3} 28 ${x} 31 C${x - 3} 28 ${x - 4} 26 ${x - 4} 24 C${x - 4} 22 ${x - 3} 20 ${x} 17 Z`} fill={core} />
  </>;
  return (
    <div aria-hidden="true" className="atlas-strip" style={{ height }}>
      <svg height={height} preserveAspectRatio="none">
        <defs>
          <filter id="atlas-feather" x="-5%" y="-10%" width="110%" height="120%">
            <feTurbulence type="fractalNoise" baseFrequency=".015 .7" numOctaves={3} seed={7} result="noise" />
            <feDisplacementMap in="SourceGraphic" in2="noise" scale={4} xChannelSelector="R" yChannelSelector="G" />
          </filter>
          <pattern id="atlas-motif" width={176 * scale} height={height} patternUnits="userSpaceOnUse">
            <g transform={`scale(${scale})`}>
              <rect width="176" height="48" fill="var(--bb-lapis)" />
              {flame(28, "var(--bb-saffron)", "var(--bb-ink)", "#FFFFFF")}
              {flame(88, "var(--bb-accent)", "var(--bb-tile)", "var(--bb-ink)")}
              {flame(148, "var(--bb-coral)", "#FFFFFF", "var(--bb-lapis)")}
              <rect x="2" width="3" height="48" fill="var(--bb-tile)" />
              <rect x="57" width="3" height="48" fill="var(--bb-accent)" />
              <rect x="117" width="3" height="48" fill="var(--bb-saffron)" />
            </g>
          </pattern>
        </defs>
        <rect width="100%" height={height} fill="url(#atlas-motif)" filter="url(#atlas-feather)" />
      </svg>
    </div>
  );
}
