"use client";

import { useState } from "react";
import { Label } from "@/components/ui/label";
import { pickupDays, pickupSlots } from "@/lib/time";
import { cn } from "@/lib/utils";

const selectClass = cn(
  "h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm shadow-xs outline-none",
  "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
  "aria-invalid:border-destructive",
);

interface Props {
  /** "Now" (ms) captured when the picker was opened, so the available slots stay stable. */
  anchor: number;
  /** "YYYY-MM-DDTHH:mm" in Philippine time, or "" when nothing is chosen. */
  value: string;
  onChange: (value: string) => void;
  invalid?: boolean;
  describedBy?: string;
}

export function PickupPicker({ anchor, value, onChange, invalid, describedBy }: Props) {
  const days = pickupDays(anchor);
  const [day, setDay] = useState(value ? value.slice(0, 10) : days[0].value);
  const slots = pickupSlots(day, anchor);

  const chosen = value.slice(11, 16);
  const time = slots.some((s) => s.value === chosen) ? chosen : "";

  function changeDay(next: string) {
    setDay(next);
    const stillValid = pickupSlots(next, anchor).some((s) => s.value === time);
    onChange(stillValid ? `${next}T${time}` : "");
  }

  return (
    <div role="group" aria-label="Pickup date and time" className="grid gap-4 sm:grid-cols-2">
      <div className="space-y-2">
        <Label htmlFor="pickupDay">Day</Label>
        <select id="pickupDay" className={selectClass} value={day} onChange={(e) => changeDay(e.target.value)}>
          {days.map((d) => <option key={d.value} value={d.value}>{d.label}</option>)}
        </select>
      </div>

      <div className="space-y-2">
        <Label htmlFor="pickupTime">Time (Philippine time)</Label>
        <select
          id="pickupTime"
          required
          className={selectClass}
          value={time}
          aria-invalid={invalid}
          aria-describedby={describedBy}
          onChange={(e) => onChange(e.target.value ? `${day}T${e.target.value}` : "")}
        >
          <option value="">{slots.length > 0 ? "Select a time" : "No times left today"}</option>
          {slots.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>
      </div>
    </div>
  );
}