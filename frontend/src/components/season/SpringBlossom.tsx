import { FallingArt, StaticArt, StaticPieceAt, type FallingPiece, type StaticPiece } from "./FallingArt";

// Spring (D63, package I): apricot blossom, the first tree to flower in the Uzbek spring. Whole blossoms sit in the
// corner of the Home hero; single petals tumble down the first screens, lighter and quicker than the leaves.
const petal = "M0 0 C7 -4 9 -13 0 -20 C-9 -13 -7 -4 0 0 Z";
const petalFill = "#FFD9E2";
const petalEdge = "rgba(232, 160, 180, .8)";

function Blossom() {
  return <>
    {[0, 72, 144, 216, 288].map((angle) => <path d={petal} fill={petalFill} key={angle} stroke={petalEdge} strokeWidth="1.2" transform={`rotate(${angle})`} />)}
    <circle fill="#F2B01E" r="3.2" />
  </>;
}

function Petal() {
  return <path d={petal} fill={petalFill} stroke={petalEdge} strokeWidth="1.2" transform="translate(0 10)" />;
}

const staticBlossoms: StaticPiece[] = [
  { x: 142, y: 40, scale: 1.1, rotate: 10, opacity: .95 },
  { x: 102, y: 22, scale: .65, rotate: 30, opacity: .8 },
  { x: 156, y: 100, scale: .8, rotate: 0, opacity: .8 }
];

const fallingPetals: FallingPiece[] = [
  { x: 40, scale: 1.1, opacity: .95, delay: 0, duration: 17, sway: 2.6, swayX: 12, swayDeg: 50 },
  { x: 300, scale: .8, opacity: .9, delay: 4, duration: 21, sway: 2.2, swayX: 10, swayDeg: 40 },
  { x: 160, scale: .7, opacity: .8, delay: 8, duration: 19, sway: 3, swayX: 14, swayDeg: 60 },
  { x: 340, scale: 1, opacity: .85, delay: 12, duration: 23, sway: 2.4, swayX: 11, swayDeg: 45 },
  { x: 90, scale: .6, opacity: .75, delay: 15, duration: 18, sway: 2.8, swayX: 9, swayDeg: 55 },
  { x: 230, scale: .65, opacity: .7, delay: 2.5, duration: 25, sway: 2.5, swayX: 13, swayDeg: 40 },
  { x: 20, scale: .5, opacity: .7, delay: 20, duration: 20, sway: 2.3, swayX: 8, swayDeg: 50 },
  { x: 270, scale: .55, opacity: .75, delay: 10, duration: 22, sway: 2.9, swayX: 12, swayDeg: 45 },
  { x: 130, scale: .45, opacity: .65, delay: 6, duration: 16, sway: 2.1, swayX: 10, swayDeg: 60 }
];

export function SpringStatic() {
  return <StaticArt season="spring">{staticBlossoms.map((blossom, index) => <StaticPieceAt key={index} piece={blossom}><Blossom /></StaticPieceAt>)}</StaticArt>;
}

export function SpringFalling() {
  return <FallingArt draw={(piece) => <g transform={`scale(${piece.scale}) rotate(${(piece.x * 5) % 90})`}><Petal /></g>} pieces={fallingPetals} season="spring" />;
}
