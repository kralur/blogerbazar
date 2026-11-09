import { FallingArt, StaticArt, StaticPieceAt, type FallingPiece, type StaticPiece } from "./FallingArt";

// Winter (D63, package H): snowflakes, white with an ice-blue glow, over the ice palette of the winter atlas. They
// fall slower than the leaves and barely sway: snow drifts, it does not tumble.
const arm = "M0 0 L0 -30 M0 -12 L-7 -19 M0 -12 L7 -19 M0 -22 L-5 -27 M0 -22 L5 -27";
const arms = [0, 60, 120, 180, 240, 300];
const glow = "rgba(158, 216, 230, .55)";

function Snowflake() {
  return <>
    {arms.map((angle) => <path d={arm} fill="none" key={`g${angle}`} stroke={glow} strokeLinecap="round" strokeWidth="6" transform={`rotate(${angle})`} />)}
    {arms.map((angle) => <path d={arm} fill="none" key={angle} stroke="#FFFFFF" strokeLinecap="round" strokeWidth="2.6" transform={`rotate(${angle})`} />)}
    <circle fill="#FFFFFF" r="3" />
  </>;
}

const staticFlakes: StaticPiece[] = [
  { x: 146, y: 34, scale: .95, rotate: 10, opacity: .95 },
  { x: 98, y: 22, scale: .5, rotate: 25, opacity: .75 },
  { x: 156, y: 102, scale: .6, rotate: 0, opacity: .7 }
];

const fallingFlakes: FallingPiece[] = [
  { x: 50, scale: .8, opacity: .95, delay: 0, duration: 28, sway: 5.2, swayX: 10, swayDeg: 30 },
  { x: 300, scale: .5, opacity: .8, delay: 6, duration: 34, sway: 4.4, swayX: 8, swayDeg: 20 },
  { x: 170, scale: .4, opacity: .7, delay: 11, duration: 30, sway: 6, swayX: 12, swayDeg: 25 },
  { x: 345, scale: .7, opacity: .85, delay: 17, duration: 36, sway: 4.8, swayX: 9, swayDeg: 35 },
  { x: 100, scale: .35, opacity: .65, delay: 21, duration: 32, sway: 5.6, swayX: 7, swayDeg: 15 },
  { x: 235, scale: .55, opacity: .75, delay: 3, duration: 38, sway: 5, swayX: 11, swayDeg: 25 },
  { x: 20, scale: .3, opacity: .6, delay: 26, duration: 29, sway: 4.2, swayX: 6, swayDeg: 20 },
  { x: 270, scale: .45, opacity: .7, delay: 14, duration: 33, sway: 5.8, swayX: 10, swayDeg: 30 }
];

export function WinterStatic() {
  return <StaticArt season="winter">{staticFlakes.map((flake, index) => <StaticPieceAt key={index} piece={flake}><Snowflake /></StaticPieceAt>)}</StaticArt>;
}

export function WinterFalling() {
  return <FallingArt draw={(piece) => <g transform={`scale(${piece.scale})`}><Snowflake /></g>} pieces={fallingFlakes} season="winter" />;
}
