import { FallingArt, StaticArt, StaticPieceAt, type FallingPiece, type StaticPiece } from "./FallingArt";

// Summer (D63, package I): fireflies of a warm evening. Warm points of light with a soft halo; on the first screens
// they drift up, not down, each pulsing on its own timer.
function Firefly() {
  return <g className="season-firefly">
    <circle fill="rgba(255, 224, 102, .3)" r="12" />
    <circle fill="rgba(255, 224, 102, .55)" r="7" />
    <circle fill="#FFF3B0" r="3.6" />
  </g>;
}

const staticFireflies: StaticPiece[] = [
  { x: 142, y: 40, scale: 1.2, rotate: 0, opacity: 1 },
  { x: 104, y: 24, scale: .7, rotate: 0, opacity: .85 },
  { x: 156, y: 100, scale: .9, rotate: 0, opacity: .9 }
];

const risingFireflies: FallingPiece[] = [
  { x: 50, scale: 1, opacity: 1, delay: 0, duration: 24, sway: 4.2, swayX: 14, swayDeg: 0 },
  { x: 300, scale: .7, opacity: .9, delay: 5, duration: 28, sway: 3.6, swayX: 10, swayDeg: 0 },
  { x: 170, scale: .6, opacity: .8, delay: 9, duration: 26, sway: 5, swayX: 16, swayDeg: 0 },
  { x: 345, scale: .9, opacity: .95, delay: 14, duration: 30, sway: 4.4, swayX: 12, swayDeg: 0 },
  { x: 100, scale: .5, opacity: .75, delay: 18, duration: 22, sway: 3.9, swayX: 9, swayDeg: 0 },
  { x: 235, scale: .8, opacity: .9, delay: 2.5, duration: 32, sway: 4.8, swayX: 15, swayDeg: 0 },
  { x: 20, scale: .45, opacity: .7, delay: 21, duration: 25, sway: 3.4, swayX: 8, swayDeg: 0 },
  { x: 270, scale: .65, opacity: .85, delay: 11, duration: 27, sway: 4.6, swayX: 13, swayDeg: 0 },
  { x: 130, scale: .55, opacity: .8, delay: 7, duration: 29, sway: 4, swayX: 11, swayDeg: 0 },
  { x: 370, scale: .4, opacity: .7, delay: 16, duration: 23, sway: 3.7, swayX: 7, swayDeg: 0 }
];

export function SummerStatic() {
  return <StaticArt season="summer">{staticFireflies.map((firefly, index) => <StaticPieceAt key={index} piece={firefly}><Firefly /></StaticPieceAt>)}</StaticArt>;
}

export function SummerFalling() {
  return <FallingArt draw={(piece) => <g transform={`scale(${piece.scale})`}><Firefly /></g>} pieces={risingFireflies} rise season="summer" />;
}
