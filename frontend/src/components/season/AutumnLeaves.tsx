import { FallingArt, StaticArt, StaticPieceAt, type FallingPiece, type StaticPiece } from "./FallingArt";

// Autumn (D63): chinar leaves, the autumn tree of Tashkent and Samarkand, in the saffron, terracotta and ochre of
// the autumn atlas palette.
const leafPath = "M0 -32 C4 -22 8 -18 14 -20 C12 -12 16 -8 24 -8 C20 -2 22 4 30 6 C22 8 18 12 20 20 C14 16 8 16 4 22 L2 34 L-2 34 L-4 22 C-8 16 -14 16 -20 20 C-18 12 -22 8 -30 6 C-22 4 -20 -2 -24 -8 C-16 -8 -12 -12 -14 -20 C-8 -18 -4 -22 0 -32 Z";
const veins = "M0 -28 L0 30 M0 -2 L20 -8 M0 -2 L-20 -8 M0 8 L16 16 M0 8 L-16 16";
const terracotta = "#D96A2B";
const saffron = "#F2B01E";
const ochre = "#C9A227";

function Leaf({ color }: { color: string }) {
  return <>
    <path d={leafPath} fill={color} />
    <path d={veins} fill="none" stroke="rgba(23, 48, 122, .45)" strokeWidth="2.2" />
  </>;
}

const staticLeaves: (StaticPiece & { color: string })[] = [
  { x: 136, y: 36, scale: .9, rotate: -25, color: terracotta, opacity: .95 },
  { x: 100, y: 20, scale: .5, rotate: 40, color: saffron, opacity: .8 },
  { x: 154, y: 100, scale: .6, rotate: 110, color: ochre, opacity: .75 }
];

const fallingLeaves: (FallingPiece & { color: string })[] = [
  { x: 40, scale: 1, color: terracotta, opacity: .9, delay: 0, duration: 18, sway: 3.8 },
  { x: 300, scale: .7, color: saffron, opacity: .85, delay: 4, duration: 22, sway: 3.1 },
  { x: 160, scale: .6, color: ochre, opacity: .75, delay: 8, duration: 20, sway: 4.2 },
  { x: 340, scale: .9, color: terracotta, opacity: .8, delay: 12, duration: 24, sway: 3.4 },
  { x: 90, scale: .5, color: saffron, opacity: .7, delay: 15, duration: 19, sway: 2.9 },
  { x: 230, scale: .55, color: ochre, opacity: .6, delay: 2.5, duration: 26, sway: 3.7 }
];

export function AutumnStatic() {
  return <StaticArt season="autumn">{staticLeaves.map((leaf, index) => <StaticPieceAt key={index} piece={leaf}><Leaf color={leaf.color} /></StaticPieceAt>)}</StaticArt>;
}

export function AutumnFalling() {
  return <FallingArt draw={(piece, index) => { const leaf = fallingLeaves[index]; return <g transform={`scale(${piece.scale}) rotate(${(piece.x * 7) % 60})`}><Leaf color={leaf.color} /></g>; }} pieces={fallingLeaves} season="autumn" />;
}
