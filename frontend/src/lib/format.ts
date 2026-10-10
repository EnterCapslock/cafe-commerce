const whole = new Intl.NumberFormat("en-PH", {
  style: "currency", currency: "PHP", minimumFractionDigits: 0, maximumFractionDigits: 0,
});
const cents = new Intl.NumberFormat("en-PH", { style: "currency", currency: "PHP" });

/** ₱130 for whole amounts, ₱130.50 otherwise. */
export function formatPeso(amount: number): string {
  return Number.isInteger(amount) ? whole.format(amount) : cents.format(amount);
}