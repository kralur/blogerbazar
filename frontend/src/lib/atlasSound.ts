// Sound for the atlas strip (D63, package F): a plucked string on every tile the finger drags past, in a pentatonic
// row so any order of tiles sounds like a tune rather than noise. Off by default: a phone in a public place should
// not start playing on its own; the switch lives in Settings. Synthesised in Web Audio, nothing is downloaded.
const storageKey = "bloggerbazar.atlasSound";
export const atlasSoundChangedEvent = "bloggerbazar:atlas-sound-changed";

// D minor pentatonic over two octaves, the mode of many Uzbek folk tunes.
const scale = [293.66, 349.23, 392, 440, 523.25, 587.33, 698.46, 783.99];
// The tile index walks the scale in a small motif so dragging in one direction gives a phrase, not a plain run.
const motif = [0, 2, 4, 3, 5, 4, 2, 1];

export function isAtlasSoundOn() {
  try {
    return localStorage.getItem(storageKey) === "on";
  } catch {
    return false;
  }
}

export function setAtlasSound(on: boolean) {
  try {
    if (on) localStorage.setItem(storageKey, "on");
    else localStorage.removeItem(storageKey);
  } catch {}
  window.dispatchEvent(new Event(atlasSoundChangedEvent));
}

export function noteForTile(index: number) {
  const step = motif[((index % motif.length) + motif.length) % motif.length];
  return scale[step];
}

type AudioContextCtor = typeof AudioContext;
let context: AudioContext | null = null;

function audioContext(): AudioContext | null {
  if (context) return context;
  const Ctor: AudioContextCtor | undefined = window.AudioContext ?? (window as Window & { webkitAudioContext?: AudioContextCtor }).webkitAudioContext;
  if (!Ctor) return null;
  try {
    context = new Ctor();
  } catch {
    return null;
  }
  return context;
}

// Call from a touch or pointer handler: browsers only let a user gesture start audio.
export function warmAtlasSound() {
  if (!isAtlasSoundOn()) return;
  const ctx = audioContext();
  if (ctx && ctx.state === "suspended") void ctx.resume().catch(() => undefined);
}

// A short pluck: a bright triangle that dies in a quarter of a second, with a softer octave under it.
export function pluckForTile(index: number) {
  if (!isAtlasSoundOn()) return;
  const ctx = audioContext();
  if (!ctx || ctx.state !== "running") return;
  const now = ctx.currentTime;
  const frequency = noteForTile(index);
  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0.0001, now);
  gain.gain.exponentialRampToValueAtTime(0.18, now + 0.004);
  gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.28);
  const filter = ctx.createBiquadFilter();
  filter.type = "lowpass";
  filter.frequency.setValueAtTime(frequency * 6, now);
  filter.frequency.exponentialRampToValueAtTime(frequency * 1.5, now + 0.25);
  const string = ctx.createOscillator();
  string.type = "triangle";
  string.frequency.setValueAtTime(frequency, now);
  const body = ctx.createOscillator();
  body.type = "sine";
  body.frequency.setValueAtTime(frequency / 2, now);
  const bodyGain = ctx.createGain();
  bodyGain.gain.setValueAtTime(0.35, now);
  string.connect(filter);
  body.connect(bodyGain);
  bodyGain.connect(filter);
  filter.connect(gain);
  gain.connect(ctx.destination);
  string.start(now);
  body.start(now);
  string.stop(now + 0.3);
  body.stop(now + 0.3);
}
