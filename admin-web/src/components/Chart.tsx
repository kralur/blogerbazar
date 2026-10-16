import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";

// One series per chart (the card title names it), so no legend; several measures are separate small charts, never
// a second axis. Colors are the Design System v2 tokens, so both themes work without a second palette.
// label is the short axis text; detail, when given, is the fuller name in the tooltip.
export type ChartPoint = { label: string; value: number; detail?: string };

type ChartProps = {
  kind: "line" | "bar";
  points: ChartPoint[];
  // Read out instead of the drawing (the chart itself is not focusable); the same numbers are always a table.
  summary: string;
  formatValue: (value: number) => string;
  formatAxis?: (value: number) => string;
  height?: number;
  axisWidth?: number;
};

const axisTick = { fill: "var(--bb-text-secondary)", fontSize: 12 };

type TooltipProps = { active?: boolean; payload?: readonly { value?: unknown; payload?: unknown }[]; label?: unknown; formatValue: (value: number) => string };

function ChartTooltip({ active, payload, label, formatValue }: TooltipProps) {
  if (!active || !payload?.length) return null;
  const detail = (payload[0].payload as ChartPoint | undefined)?.detail;
  return (
    <div className="rounded-[var(--bb-radius-control)] border border-brand-line bg-brand-surface px-3 py-2 text-xs text-brand-ink shadow-card">
      <p className="text-brand-muted">{detail ?? String(label ?? "")}</p>
      <p className="mt-0.5 text-sm font-bold tabular-nums">{formatValue(Number(payload[0].value ?? 0))}</p>
    </div>
  );
}

export function Chart({ kind, points, summary, formatValue, formatAxis = formatValue, height = 200, axisWidth = 48 }: ChartProps) {
  const axes = [
    <CartesianGrid key="grid" vertical={false} stroke="var(--bb-border)" />,
    <XAxis key="x" dataKey="label" tick={axisTick} tickLine={false} axisLine={{ stroke: "var(--bb-border)" }} interval="preserveStartEnd" minTickGap={16} />,
    <YAxis key="y" tick={axisTick} tickLine={false} axisLine={false} allowDecimals={false} width={axisWidth} tickFormatter={(value: number) => formatAxis(value)} />,
    <Tooltip key="tip" cursor={kind === "line" ? { stroke: "var(--bb-border)" } : { fill: "var(--bb-surface-secondary)" }} content={(props) => <ChartTooltip {...props} formatValue={formatValue} />} />
  ];
  return (
    <div role="img" aria-label={summary} className="w-full" style={{ height }}>
      <ResponsiveContainer width="100%" height="100%" initialDimension={{ width: 320, height }}>
        {kind === "line" ? (
          <LineChart accessibilityLayer={false} data={points} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
            {axes}
            <Line type="monotone" dataKey="value" stroke="var(--bb-action)" strokeWidth={2} dot={false} activeDot={{ r: 4, stroke: "var(--bb-surface)", strokeWidth: 2 }} isAnimationActive={false} />
          </LineChart>
        ) : (
          <BarChart accessibilityLayer={false} data={points} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
            {axes}
            <Bar dataKey="value" fill="var(--bb-action)" radius={[4, 4, 0, 0]} maxBarSize={32} isAnimationActive={false} />
          </BarChart>
        )}
      </ResponsiveContainer>
    </div>
  );
}
