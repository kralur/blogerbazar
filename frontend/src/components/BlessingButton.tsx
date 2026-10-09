import { useEffect, useRef, useState, type CSSProperties } from "react";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";

const fireworkEmoji = ["🎉", "✨", "🤝", "💚", "🚀", "🥳", "⭐"];
const burstTaps = 3;
const burstWindowMs = 1200;
const particlesPerBurst = 14;
const maxParticles = 42;
const particleLifeMs = 1100;

type Particle = { id: number; emoji: string; dx: number; dy: number; rotate: number; delay: number };

// "Barakasini bersin!" on a new deal is a small celebration: a tap gives a light buzz and a bounce,
// three quick taps launch a short emoji firework. Reduced motion keeps only the buzz.
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

  const launch = () => {
    const burst = Array.from({ length: particlesPerBurst }, (_, index) => {
      const angle = (Math.PI * 2 * index) / particlesPerBurst + Math.random() * 0.5;
      const distance = 70 + Math.random() * 80;
      return {
        id: nextIdRef.current++,
        emoji: fireworkEmoji[Math.floor(Math.random() * fireworkEmoji.length)],
        dx: Math.round(Math.cos(angle) * distance),
        dy: Math.round(Math.sin(angle) * distance - 40),
        rotate: Math.round(Math.random() * 120 - 60),
        delay: Math.round(Math.random() * 120)
      };
    });
    setParticles((current) => [...current, ...burst].slice(-maxParticles));
    const ids = new Set(burst.map((particle) => particle.id));
    timersRef.current.push(window.setTimeout(() => setParticles((current) => current.filter((particle) => !ids.has(particle.id))), particleLifeMs + 200));
  };

  const tap = () => {
    haptic.impact();
    if (reducedMotion) return;
    setBounce((value) => value + 1);
    const now = Date.now();
    tapsRef.current = [...tapsRef.current.filter((time) => now - time < burstWindowMs), now];
    if (tapsRef.current.length >= burstTaps) {
      tapsRef.current = [];
      haptic.success();
      launch();
    }
  };

  return <button aria-label={t("deals.blessing")} className="deal-blessing" data-bounce={bounce === 0 ? undefined : bounce % 2 ? "a" : "b"} onClick={tap} type="button">
    <span aria-hidden="true" className="deal-blessing__icon">🤝</span>{t("deals.blessing")}
    {particles.length > 0 && <span aria-hidden="true" className="deal-blessing__fireworks">
      {particles.map((particle) => <span className="deal-blessing__particle" key={particle.id} style={{ "--dx": `${particle.dx}px`, "--dy": `${particle.dy}px`, "--rotate": `${particle.rotate}deg`, animationDelay: `${particle.delay}ms` } as CSSProperties}>{particle.emoji}</span>)}
    </span>}
  </button>;
}
