import { type CSSProperties, type PointerEvent as ReactPointerEvent, type ReactNode } from "react";
import type { Season } from "../../lib/season";

// The two layers every drawn season shares (D63, package H): a few static pieces pinned to the top right corner of
// a lapis block (the Home hero) and a set of pieces drifting down the whole screen (the first screens), each at its
// own pace, swaying on its own timer. A falling piece can be caught with a finger and carried; released, it keeps
// falling from where it was left. `rise` sends the pieces up instead (the fireflies of summer). The season modules
// only say what a piece looks like and where it starts.
export type StaticPiece = { x: number; y: number; scale: number; rotate: number; opacity: number };
// x, size, opacity, start offset and fall duration in seconds, sway period in seconds, sway reach in px and degrees
export type FallingPiece = { x: number; scale: number; opacity: number; delay: number; duration: number; sway: number; swayX?: number; swayDeg?: number };

export function StaticArt({ season, children }: { season: Season; children: ReactNode }) {
  return <svg aria-hidden="true" className={`season-art season-art--static season-art--${season}`} viewBox="0 0 180 140">{children}</svg>;
}

export function StaticPieceAt({ piece, children }: { piece: StaticPiece; children: ReactNode }) {
  return <g opacity={piece.opacity} transform={`translate(${piece.x} ${piece.y})`}><g transform={`scale(${piece.scale}) rotate(${piece.rotate})`}>{children}</g></g>;
}

export function FallingArt({ season, pieces, draw, rise = false }: { season: Season; pieces: readonly FallingPiece[]; draw: (piece: FallingPiece, index: number) => ReactNode; rise?: boolean }) {
  return <svg aria-hidden="true" className={`season-art season-art--falling season-art--${season}${rise ? " season-art--rising" : ""}`} preserveAspectRatio="none" viewBox="0 0 390 844">
    {pieces.map((piece, index) => <g key={index} onPointerCancel={releasePiece} onPointerDown={grabPiece} onPointerMove={dragPiece} onPointerUp={releasePiece} style={pieceStyle(piece)}>
      <g>{draw(piece, index)}</g>
    </g>)}
  </svg>;
}

function pieceStyle(piece: FallingPiece) {
  const style: Record<string, string | number> = {
    "--art-x": `${piece.x}px`,
    "--art-opacity": piece.opacity,
    "--art-delay": `-${piece.delay}s`,
    "--art-duration": `${piece.duration}s`,
    "--art-sway": `${piece.sway}s`
  };
  if (piece.swayX !== undefined) style["--art-sway-x"] = `${piece.swayX}px`;
  if (piece.swayDeg !== undefined) style["--art-sway-deg"] = `${piece.swayDeg}deg`;
  return style as CSSProperties;
}

// The fall runs from -60 to 880 in the 390x844 drawing (a rise the other way); a released piece continues from
// where the finger left it.
const fallStart = -60;
const fallEnd = 880;
const fallLength = fallEnd - fallStart;
const grabbed = new WeakMap<Element, { dx: number; dy: number }>();

// Finger (or mouse) position in the drawing's units, whatever size the svg is stretched to.
function drawingPoint(svg: SVGSVGElement, clientX: number, clientY: number) {
  const matrix = svg.getScreenCTM?.();
  if (!matrix) return { x: clientX, y: clientY };
  const point = new DOMPoint(clientX, clientY).matrixTransform(matrix.inverse());
  return { x: point.x, y: point.y };
}

function currentTranslate(element: Element) {
  // Browsers answer with a matrix; a plain translate() is what an inline style reads back as in tests.
  const transform = getComputedStyle(element).transform || (element as HTMLElement | SVGElement).style?.transform || "";
  const matrix = /matrix\(([^)]+)\)/.exec(transform);
  if (matrix) { const parts = matrix[1].split(",").map(Number); return { x: parts[4], y: parts[5] }; }
  const translate = /translate\(\s*(-?[\d.]+)px,\s*(-?[\d.]+)px\)/.exec(transform);
  if (translate) return { x: Number(translate[1]), y: Number(translate[2]) };
  return { x: 0, y: fallStart };
}

function grabPiece(event: ReactPointerEvent<SVGGElement>) {
  const piece = event.currentTarget;
  const svg = piece.ownerSVGElement;
  if (!svg) return;
  const at = currentTranslate(piece);
  const finger = drawingPoint(svg, event.clientX, event.clientY);
  grabbed.set(piece, { dx: finger.x - at.x, dy: finger.y - at.y });
  piece.style.animation = "none";
  piece.style.transform = `translate(${at.x}px, ${at.y}px)`;
  piece.style.opacity = "1";
  piece.setPointerCapture?.(event.pointerId);
}

function dragPiece(event: ReactPointerEvent<SVGGElement>) {
  const piece = event.currentTarget;
  const grip = grabbed.get(piece);
  const svg = piece.ownerSVGElement;
  if (!grip || !svg) return;
  const finger = drawingPoint(svg, event.clientX, event.clientY);
  piece.style.transform = `translate(${finger.x - grip.dx}px, ${finger.y - grip.dy}px)`;
}

function releasePiece(event: ReactPointerEvent<SVGGElement>) {
  const piece = event.currentTarget;
  if (!grabbed.has(piece)) return;
  grabbed.delete(piece);
  const at = currentTranslate(piece);
  const duration = parseFloat(piece.style.getPropertyValue("--art-duration")) || 20;
  const rising = piece.ownerSVGElement?.classList.contains("season-art--rising") ?? false;
  const travelled = rising ? fallEnd - at.y : at.y - fallStart;
  const progress = Math.min(Math.max(travelled / fallLength, 0), 1);
  piece.style.setProperty("--art-x", `${Math.min(Math.max(at.x, 0), 390)}px`);
  piece.style.transform = "";
  piece.style.opacity = "";
  piece.style.animation = "";
  piece.style.animationDelay = `-${(progress * duration).toFixed(2)}s`;
}
