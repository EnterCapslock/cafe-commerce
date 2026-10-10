// The Philippines has no daylight saving, so a fixed +08:00 offset is always correct.
// All pickup times are entered and shown in Philippine time, whatever the visitor's own zone.

export const PICKUP_MIN_LEAD_MINUTES = 15; // keep in sync with Orders:MinLeadMinutes
export const PICKUP_MAX_DAYS = 7;          // keep in sync with Orders:MaxAdvanceDays

/** Interprets a datetime-local value ("2026-10-10T14:30") as Philippine time. */
export function phtLocalToIso(local: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(local)) return null;
  const date = new Date(`${local}:00+08:00`);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

/** Quick choices ("20", "30", ...) are minutes from now, computed when called. */
export function resolvePickupIso(choice: string, local: string): string | null {
  if (choice === "custom") return phtLocalToIso(local);
  const minutes = Number(choice);
  return Number.isFinite(minutes) ? new Date(Date.now() + minutes * 60_000).toISOString() : null;
}

// The API returns UTC timestamps; make sure a missing "Z" can't be read as browser-local time.
function toDate(iso: string): Date {
  return new Date(/(Z|[+-]\d{2}:\d{2})$/.test(iso) ? iso : `${iso}Z`);
}

const phtFormatter = new Intl.DateTimeFormat("en-PH", {
  timeZone: "Asia/Manila",
  dateStyle: "medium",
  timeStyle: "short",
});

export function formatPht(iso: string): string {
  return `${phtFormatter.format(toDate(iso))} PHT`;
}

const PHT_OFFSET_MS = 8 * 3_600_000;
const pad = (n: number) => String(n).padStart(2, "0");
const dayLabel = new Intl.DateTimeFormat("en-PH", {
  weekday: "short", month: "short", day: "numeric", timeZone: "UTC",
});

/** Calendar days (in Philippine time) that can be booked, starting today. */
export function pickupDays(anchorMs: number): { value: string; label: string }[] {
  const pht = new Date(anchorMs + PHT_OFFSET_MS);
  const y = pht.getUTCFullYear(), m = pht.getUTCMonth(), d = pht.getUTCDate();

  return Array.from({ length: PICKUP_MAX_DAYS + 1 }, (_, i) => {
    const date = new Date(Date.UTC(y, m, d + i));
    const value = `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;
    return { value, label: i === 0 ? "Today" : i === 1 ? "Tomorrow" : dayLabel.format(date) };
  });
}

/** 15-minute slots for one day (the cafe is 24/7), limited to the allowed booking window. */
export function pickupSlots(day: string, anchorMs: number): { value: string; label: string }[] {
  const earliest = anchorMs + PICKUP_MIN_LEAD_MINUTES * 60_000;
  const latest = anchorMs + PICKUP_MAX_DAYS * 86_400_000;
  const slots: { value: string; label: string }[] = [];

  for (let minutes = 0; minutes < 24 * 60; minutes += 15) {
    const hh = Math.floor(minutes / 60);
    const mm = minutes % 60;
    const at = Date.parse(`${day}T${pad(hh)}:${pad(mm)}:00+08:00`);
    if (at < earliest || at > latest) continue;

    const hour12 = hh % 12 === 0 ? 12 : hh % 12;
    slots.push({ value: `${pad(hh)}:${pad(mm)}`, label: `${hour12}:${pad(mm)} ${hh < 12 ? "AM" : "PM"}` });
  }
  return slots;
}