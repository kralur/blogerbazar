import type { Language } from "../i18n";
import { formatShortDate } from "./currency";

// D69: a planned publication is entered and shown in Tashkent time (UTC+5, no daylight saving), wherever the browser is.
const tashkentOffsetMs = 5 * 60 * 60 * 1000;
// The same window the server accepts: from a minute to 60 days ahead.
const minimumLeadMs = 60 * 1000;
const maximumLeadMs = 60 * 24 * 60 * 60 * 1000;
export const defaultScheduleTime = "11:00";

export type ScheduleProblem = "invalid" | "past" | "tooFar" | "expired";

const pad = (value: number) => String(value).padStart(2, "0");
const isoDay = (date: Date) => `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;

// The Tashkent calendar day (yyyy-mm-dd) and clock time (hh:mm) of a moment.
export function tashkentParts(value: string | Date): { day: string; time: string } {
  const shifted = new Date(new Date(value).getTime() + tashkentOffsetMs);
  return { day: isoDay(shifted), time: `${pad(shifted.getUTCHours())}:${pad(shifted.getUTCMinutes())}` };
}

// The moment of a Tashkent day and time; null when either is not filled in.
export function tashkentToUtc(day: string, time: string): Date | null {
  const dayMatch = /^(\d{4})-(\d{2})-(\d{2})$/.exec(day);
  const timeMatch = /^(\d{2}):(\d{2})$/.exec(time);
  if (!dayMatch || !timeMatch) return null;
  return new Date(Date.UTC(Number(dayMatch[1]), Number(dayMatch[2]) - 1, Number(dayMatch[3]), Number(timeMatch[1]), Number(timeMatch[2])) - tashkentOffsetMs);
}

// Tomorrow at 11:00 in Tashkent: the usual "publish tomorrow morning" plan.
export function defaultSchedule(now = new Date()): { day: string; time: string } {
  const [year, month, day] = tashkentParts(now).day.split("-").map(Number);
  return { day: isoDay(new Date(Date.UTC(year, month - 1, day + 1))), time: defaultScheduleTime };
}

// The Tashkent days the date field offers: today through 60 days ahead.
export function scheduleDayRange(now = new Date()): { min: string; max: string } {
  return { min: tashkentParts(now).day, max: tashkentParts(new Date(now.getTime() + maximumLeadMs)).day };
}

// Same checks as the server; the deadline is a calendar day that stays open through that day (UTC, as on the server).
export function scheduleProblem(at: Date | null, deadline?: string | null, now = new Date()): ScheduleProblem | null {
  if (!at || Number.isNaN(at.getTime())) return "invalid";
  if (at.getTime() < now.getTime() + minimumLeadMs) return "past";
  if (at.getTime() > now.getTime() + maximumLeadMs) return "tooFar";
  if (deadline && new Date(deadline).toISOString().slice(0, 10) < at.toISOString().slice(0, 10)) return "expired";
  return null;
}

export const scheduleProblemKey: Record<ScheduleProblem, string> = {
  invalid: "schedule.errorInvalid",
  past: "schedule.errorPast",
  tooFar: "schedule.errorTooFar",
  expired: "schedule.errorExpired"
};

// "22 Nov, 11:00" in the reader's language: the planned moment as the Tashkent clock shows it.
export function formatScheduled(value: string, language?: Language): string {
  const { day, time } = tashkentParts(value);
  const [year, month, date] = day.split("-").map(Number);
  const [hours, minutes] = time.split(":").map(Number);
  return formatShortDate(new Date(year, month - 1, date, hours, minutes), language, { time: true });
}
