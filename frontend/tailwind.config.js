/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        brand: {
          blue: "#2563EB",
          cyan: "#06B6D4",
          premium: "#7C3AED",
          ink: "#111827",
          muted: "#6B7280",
          bg: "#F8FAFC",
          line: "#E5E7EB",
          success: "#22C55E",
          warning: "#F59E0B",
          danger: "#EF4444"
        }
      },
      fontFamily: {
        sans: ["Inter", "SF Pro Display", "ui-sans-serif", "system-ui", "sans-serif"]
      },
      boxShadow: {
        soft: "0 20px 60px rgba(15, 23, 42, 0.08)",
        card: "0 14px 35px rgba(17, 24, 39, 0.07)",
        glow: "0 18px 50px rgba(37, 99, 235, 0.24)"
      },
      borderRadius: {
        "4xl": "2rem",
        "5xl": "2.5rem"
      },
      backgroundImage: {
        "brand-gradient": "linear-gradient(135deg, #2563EB 0%, #06B6D4 100%)",
        "premium-gradient": "linear-gradient(135deg, #7C3AED 0%, #2563EB 55%, #06B6D4 100%)",
        "soft-radial": "radial-gradient(circle at 20% 10%, rgba(37,99,235,.16), transparent 34%), radial-gradient(circle at 85% 0%, rgba(6,182,212,.14), transparent 28%)"
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
