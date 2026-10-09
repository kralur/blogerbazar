import { useEffect, useRef, useState, type CSSProperties, type KeyboardEvent } from "react";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";

const fireworkEmoji = ["🎉", "✨", "🤝", "💚", "🚀", "🥳", "⭐"];
const comboTaps = 5;
const comboWindowMs = 1500;
const floatersPerTap = 2;
const particlesPerBurst = 14;
const maxParticles = 60;
const particleLifeMs = 1300;

type Particle = { id: number; emoji: string; kind: "float" | "burst"; x: number; dx: number; dy: number; rotate: number; delay: number };

const randomEmoji = () => fireworkEmoji[Math.floor(Math.random() * fireworkEmoji.length)];

// "Barakasini bersin!" on a new deal is a small celebration, like likes in a live stream: every tap sends
// emoji floating up, tapping fast makes a stream, five quick taps add a firework. Reduced motion keeps only the buzz.
export function BlessingButton() {
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const [particles, setParticles] = useState<Particle[]>([]);
  const [bounce, setBounce] = useState(0);
  const tapsRef = useRef<number[]>([]);
  const nextIdRef = useRef(0);
  const timersRef = useRef<number[]>([]);

  useEffect(() => () => timersRef.current.forEach((timer) => window.clearTimeout(timer)), []);

  const reducedMotion = typeof window !== "undefined" && window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;

  const add = (added: Particle[]) => {
    setParticles((current) => [...current, ...added].slice(-maxParticles));
    const ids = new Set(added.map((particle) => particle.id));
    timersRef.current.push(window.setTimeout(() => setParticles((current) => current.filter((particle) => !ids.has(particle.id))), particleLifeMs + 250));
  };

  const floaters = (): Particle[] => Array.from({ length: floatersPerTap }, () => ({
    id: nextIdRef.current++,
    emoji: randomEmoji(),
    kind: "float",
    x: Math.round(Math.random() * 70 - 35),
    dx: Math.round(Math.random() * 60 - 30),
    dy: -Math.round(110 + Math.random() * 90),
    rotate: Math.round(Math.random() * 40 - 20),
    delay: Math.round(Math.random() * 80)
  }));

  const burst = (): Particle[] => Array.from({ length: particlesPerBurst }, (_, index) => {
    const angle = (Math.PI * 2 * index) / particlesPerBurst + Math.random() * 0.5;
    const distance = 70 + Math.random() * 80;
    return {
      id: nextIdRef.current++,
      emoji: randomEmoji(),
      kind: "burst",
      x: 0,
      dx: Math.round(Math.cos(angle) * distance),
      dy: Math.round(Math.sin(angle) * distance - 40),
      rotate: Math.round(Math.random() * 120 - 60),
      delay: Math.round(Math.random() * 120)
    };
  });

  const tap = () => {
    haptic.impact();
    if (reducedMotion) return;
    setBounce((value) => value + 1);
    const now = Date.now();
    tapsRef.current = [...tapsRef.current.filter((time) => now - time < comboWindowMs), now];
    const combo = tapsRef.current.length >= comboTaps;
    if (combo) {
      tapsRef.current = [];
      haptic.success();
    }
    add(combo ? [...floaters(), ...burst()] : floaters());
  };

  // Reacting on touch, not on click: a click waits for the double-tap check and fast taps get lost.
  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === "Enter" || event.key === " ") { event.preventDefault(); tap(); }
  };

  return <button aria-label={t("deals.blessing")} className="deal-blessing" data-bounce={bounce === 0 ? undefined : bounce % 2 ? "a" : "b"} onKeyDown={onKeyDown} onPointerDown={tap} type="button">
    <span aria-hidden="true" className="deal-blessing__icon">🤝</span>{t("deals.blessing")}
    {particles.length > 0 && <span aria-hidden="true" className="deal-blessing__fireworks">
      {particles.map((particle) => <span className={`deal-blessing__particle deal-blessing__particle--${particle.kind}`} key={particle.id} style={{ "--x": `${particle.x}px`, "--dx": `${particle.dx}px`, "--dy": `${particle.dy}px`, "--rotate": `${particle.rotate}deg`, animationDelay: `${particle.delay}ms` } as CSSProperties}>{particle.emoji}</span>)}
    </span>}
  </button>;
}
