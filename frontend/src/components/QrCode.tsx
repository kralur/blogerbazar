import qrcode from "qrcode-generator";
import { useMemo } from "react";

// D64 stage B: the browser sign-in link as a QR code for the phone's camera. Drawn as one SVG path of dark modules
// with the standard quiet zone of four modules; dark on white whatever the theme, as scanners expect.
export function QrCode({ value, label, className }: { value: string; label: string; className?: string }) {
  const { size, path } = useMemo(() => {
    const code = qrcode(0, "M");
    code.addData(value);
    code.make();
    const count = code.getModuleCount();
    const quiet = 4;
    let d = "";
    for (let row = 0; row < count; row += 1) {
      for (let column = 0; column < count; column += 1) {
        if (code.isDark(row, column)) d += `M${column + quiet} ${row + quiet}h1v1h-1z`;
      }
    }
    return { size: count + quiet * 2, path: d };
  }, [value]);

  return <svg aria-label={label} className={className} role="img" shapeRendering="crispEdges" viewBox={`0 0 ${size} ${size}`} xmlns="http://www.w3.org/2000/svg">
    <rect fill="#FFFFFF" height={size} width={size} />
    <path d={path} fill="#0C1838" />
  </svg>;
}
