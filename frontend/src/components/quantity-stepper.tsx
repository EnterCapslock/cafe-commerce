import { Minus, Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { MAX_LINE_QUANTITY } from "@/stores/cart-store";

interface Props {
  value: number;
  onChange: (value: number) => void;
  label: string; // for screen readers, e.g. the product name
  min?: number;
  max?: number;
}

export function QuantityStepper({ value, onChange, label, min = 1, max = MAX_LINE_QUANTITY }: Props) {
  return (
    <div className="inline-flex items-center gap-2">
      <Button type="button" variant="outline" size="icon" aria-label={`Decrease quantity of ${label}`}
        disabled={value <= min} onClick={() => onChange(value - 1)}>
        <Minus aria-hidden />
      </Button>
      <span className="w-8 text-center tabular-nums" aria-live="polite">{value}</span>
      <Button type="button" variant="outline" size="icon" aria-label={`Increase quantity of ${label}`}
        disabled={value >= max} onClick={() => onChange(value + 1)}>
        <Plus aria-hidden />
      </Button>
    </div>
  );
}