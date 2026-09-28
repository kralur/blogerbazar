/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        brand: {
          blue: "var(--bb-action)",
          cyan: "var(--bb-accent)",
          premium: "var(--bb-premium)",
          ink: "var(--bb-text)",
          muted: "var(--bb-text-secondary)",
          bg: "var(--bb-background)",
          line: "var(--bb-border)",
          success: "var(--bb-success)",
          warning: "var(--bb-warning)",
          danger: "var(--bb-error)"
        }
      },
      fontFamily: {
        sans: ["Inter", "SF Pro Display", "ui-sans-serif", "system-ui", "sans-serif"]
      },
      boxShadow: {
        soft: "var(--bb-shadow-overlay)",
        card: "var(--bb-shadow-card)",
        glow: "var(--bb-shadow-action)"
      },
      borderRadius: {
        "4xl": "2rem",
        "5xl": "2.5rem"
      },
      backgroundImage: {
        "brand-gradient": "var(--bb-gradient-action)",
        "premium-gradient": "var(--bb-gradient-premium)",
        "soft-radial": "none"
      },
      keyframes: {
        "screen-in": {
          "0%": { opacity: "0", transform: "translateY(6px) scale(.995)" },
          "100%": { opacity: "1", transform: "translateY(0) scale(1)" }
        }
      },
      animation: {
        "screen-in": "screen-in 240ms cubic-bezier(.2,.8,.2,1) both"
      }
    }
  },
  plugins: []
};
