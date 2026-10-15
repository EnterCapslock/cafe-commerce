"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { CircleAlert } from "lucide-react";
import { useRef, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import Link from "next/link";
import { PickupPicker } from "@/components/checkout/pickup-picker";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api, ApiError } from "@/lib/api/client";
import { describeApiError } from "@/lib/api/errors";
import { COUNTER_PAYMENT_LIMIT } from "@/lib/config";
import { formatPeso } from "@/lib/format";
import { checkoutSchema, type CheckoutValues } from "@/lib/schemas/checkout";
import { formatPht, PICKUP_MAX_DAYS, PICKUP_MIN_LEAD_MINUTES, phtLocalToIso, resolvePickupIso } from "@/lib/time";
import { cn } from "@/lib/utils";
import { selectSubtotal, useCartStore } from "@/stores/cart-store";
import type { OrderDto, UserDto } from "@/types/api";

const STEPS = ["Your details", "Pickup & payment", "Review"] as const;
const LAST_STEP = STEPS.length - 1;

const STEP_FIELDS: (keyof CheckoutValues)[][] = [
  ["customerName", "customerEmail", "customerPhone"],
  ["pickupChoice", "pickupLocal", "paymentMethod"],
];

const PICKUP_OPTIONS: { value: CheckoutValues["pickupChoice"]; title: string; description: string }[] = [
  { value: "20", title: "As soon as possible", description: "About 20 minutes" },
  { value: "30", title: "In 30 minutes", description: "" },
  { value: "60", title: "In 1 hour", description: "" },
  { value: "120", title: "In 2 hours", description: "" },
  { value: "custom", title: "Pick a date and time", description: "Up to 7 days ahead" },
];

function Choice({
  selected, disabled, onSelect, title, description,
}: {
  selected: boolean;
  disabled?: boolean;
  onSelect: () => void;
  title: string;
  description?: string;
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      disabled={disabled}
      onClick={onSelect}
      className={cn(
        "flex w-full flex-col items-start rounded-lg border px-4 py-3 text-left text-sm transition-colors",
        "disabled:cursor-not-allowed disabled:opacity-50",
        selected ? "border-primary bg-primary/5 ring-1 ring-primary" : "hover:bg-muted",
      )}
    >
      <span className="font-medium">{title}</span>
      {description && <span className="text-muted-foreground">{description}</span>}
    </button>
  );
}

function FieldError({ id, message }: { id: string; message?: string }) {
  return message ? (
    <p id={id} role="alert" className="text-sm text-destructive">
      {message}
    </p>
  ) : null;
}

export function CheckoutForm({
  onPlaced,
  user,
}: {
  onPlaced: (order: OrderDto) => void;
  user: UserDto | null;
}) {
  const items = useCartStore((s) => s.items);
  const subtotal = useCartStore(selectSubtotal);
  const counterAllowed = subtotal <= COUNTER_PAYMENT_LIMIT;

  const [step, setStep] = useState(0);
  const [submitting, setSubmitting] = useState(false);
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const submittingRef = useRef(false);
  const [anchor, setAnchor] = useState<number | null>(null); 

  const form = useForm<CheckoutValues>({
    resolver: zodResolver(checkoutSchema),
    mode: "onTouched",
    defaultValues: {
      customerName: user?.name ?? "",
      customerEmail: user?.email ?? "",
      customerPhone: "",
      pickupChoice: "20",
      pickupLocal: "",
      paymentMethod: "Online",
    },
  });
  const { register, trigger, setValue, getValues, handleSubmit, control, formState: { errors } } = form;

  const pickupChoice = useWatch({ control, name: "pickupChoice" });
  const paymentMethod = useWatch({ control, name: "paymentMethod" });
  const pickupLocal = useWatch({ control, name: "pickupLocal" });

  async function next() {
    if (await trigger(STEP_FIELDS[step], { shouldFocus: true })) setStep((s) => s + 1);
  }

  async function onSubmit(values: CheckoutValues) {
    if (submittingRef.current) return;
    setServerErrors([]);

    const pickupTime = resolvePickupIso(values.pickupChoice, values.pickupLocal);
    if (!pickupTime) {
      setStep(1);
      setServerErrors(["Please choose a valid pickup time."]);
      return;
    }

    submittingRef.current = true;
    setSubmitting(true);
    try {
      const order = await api.createOrder({
        customerName: values.customerName.trim(),
        customerEmail: values.customerEmail.trim(),
        customerPhone: values.customerPhone.trim(),
        pickupTime,
        paymentMethod: values.paymentMethod,
        items: items.map((i) => ({ variantId: i.variantId, quantity: i.quantity })),
      });
      onPlaced(order);
    } catch (error) {
      const messages = describeApiError(error);
      if (error instanceof ApiError && error.status === 502 && counterAllowed) {
        messages.push("You can also go back and choose to pay at the counter.");
      }
      setServerErrors(messages);
      submittingRef.current = false;
      setSubmitting(false);
    }
  }

  // A form submit (the Continue button, or Enter in a field) only ever means "continue".
  // Placing the order needs an explicit click on the final button.
  function handleFormSubmit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (step < LAST_STEP) void next();
  }

  const review = getValues();
  const reviewPickup =
    review.pickupChoice === "custom"
      ? (() => {
          const iso = phtLocalToIso(review.pickupLocal);
          return iso ? formatPht(iso) : "—";
        })()
      : `In about ${review.pickupChoice} minutes`;

  return (
    <form onSubmit={handleFormSubmit} noValidate className="space-y-6">
      <ol className="flex flex-wrap items-center gap-2 text-sm" aria-label="Checkout progress">
        {STEPS.map((label, i) => (
          <li
            key={label}
            aria-current={i === step ? "step" : undefined}
            className={cn(
              "flex items-center gap-2 rounded-full px-3 py-1",
              i === step ? "bg-primary text-primary-foreground" : i < step ? "bg-muted" : "text-muted-foreground",
            )}
          >
            <span className="font-medium">{i + 1}</span>
            <span className="hidden sm:inline">{label}</span>
          </li>
        ))}
      </ol>

      {step === 0 && (
        <fieldset className="space-y-4">
          <legend className="mb-1 text-lg font-medium">Your details</legend>
          
          <p className="text-sm text-muted-foreground">
            {user ? (
              <>Ordering as {user.email}. This order will be saved to your account.</>
            ) : (
              <>
                Checking out as a guest.{" "}
                <Link href="/login?next=/checkout" className="font-medium text-foreground underline underline-offset-4">
                  Sign in
                </Link>{" "}
                to save this order to your account.
              </>
            )}
          </p>

          <p className="text-sm text-muted-foreground">All fields are required.</p>

          <div className="space-y-2">
            <Label htmlFor="customerName">Name</Label>
            <Input
              id="customerName" autoComplete="name" required
              aria-invalid={!!errors.customerName}
              aria-describedby={errors.customerName ? "customerName-error" : undefined}
              {...register("customerName")}
            />
            <FieldError id="customerName-error" message={errors.customerName?.message} />
          </div>

          <div className="space-y-2">
            <Label htmlFor="customerEmail">Email</Label>
            <Input
              id="customerEmail" type="email" autoComplete="email" required
              aria-invalid={!!errors.customerEmail}
              aria-describedby={errors.customerEmail ? "customerEmail-error" : undefined}
              {...register("customerEmail")}
            />
            <p className="text-sm text-muted-foreground">We&apos;ll email your order confirmation and tell you when it&apos;s ready.</p>
            <FieldError id="customerEmail-error" message={errors.customerEmail?.message} />
          </div>

          <div className="space-y-2">
            <Label htmlFor="customerPhone">Mobile number</Label>
            <Input
              id="customerPhone" type="tel" inputMode="tel" autoComplete="tel" required
              placeholder="09171234567"
              aria-invalid={!!errors.customerPhone}
              aria-describedby={errors.customerPhone ? "customerPhone-error" : undefined}
              {...register("customerPhone")}
            />
            <FieldError id="customerPhone-error" message={errors.customerPhone?.message} />
          </div>
        </fieldset>
      )}

      {step === 1 && (
        <div className="space-y-8">
          <div className="space-y-3">
            <h2 className="text-lg font-medium" id="pickup-heading">When will you pick it up?</h2>
            <div role="radiogroup" aria-labelledby="pickup-heading" className="grid gap-2 sm:grid-cols-2">
              {PICKUP_OPTIONS.map((o) => (
                <Choice
                  key={o.value}
                  selected={pickupChoice === o.value}
                  title={o.title}
                  description={o.description}
                  onSelect={() => {
                    setValue("pickupChoice", o.value, { shouldValidate: true });
                    if (o.value === "custom") setAnchor(Date.now());
                  }}
                />
              ))}
            </div>
          {pickupChoice === "custom" && anchor !== null && (
            <div className="space-y-2">
                <PickupPicker
                  anchor={anchor}
                  value={pickupLocal}
                  onChange={(v) => setValue("pickupLocal", v, { shouldValidate: true, shouldTouch: true })}
                  invalid={!!errors.pickupLocal}
                  describedBy={errors.pickupLocal ? "pickupLocal-error" : "pickupLocal-hint"}
                />
                <p id="pickupLocal-hint" className="text-sm text-muted-foreground">
                  At least {PICKUP_MIN_LEAD_MINUTES} minutes from now, up to {PICKUP_MAX_DAYS} days ahead.
                </p>
                <FieldError id="pickupLocal-error" message={errors.pickupLocal?.message} />
            </div>
          )}  
          </div>

          <div className="space-y-3">
            <h2 className="text-lg font-medium" id="payment-heading">How would you like to pay?</h2>
            <div role="radiogroup" aria-labelledby="payment-heading" className="grid gap-2">
              <Choice
                selected={paymentMethod === "Online"}
                title="Pay online"
                description="GCash, Maya, GrabPay, QR Ph, or card, through PayMongo. Demo mode: no real money is charged."
                onSelect={() => setValue("paymentMethod", "Online", { shouldValidate: true })}
              />
              <Choice
                selected={paymentMethod === "PayAtCounter"}
                disabled={!counterAllowed}
                title="Pay at the counter"
                description={
                  counterAllowed
                    ? "Pay when you pick up your order."
                    : `Only for orders up to ${formatPeso(COUNTER_PAYMENT_LIMIT)}. Please pay online.`
                }
                onSelect={() => setValue("paymentMethod", "PayAtCounter", { shouldValidate: true })}
              />
            </div>
          </div>
        </div>
      )}

      {step === 2 && (
        <div className="space-y-4">
          <h2 className="text-lg font-medium">Review your order</h2>
          <dl className="divide-y rounded-xl border text-sm">
            {[
              ["Name", review.customerName],
              ["Email", review.customerEmail],
              ["Mobile", review.customerPhone],
              ["Pickup", reviewPickup],
              ["Payment", review.paymentMethod === "Online" ? "Pay online" : "Pay at the counter"],
              ["Total", formatPeso(subtotal)],
            ].map(([label, value]) => (
              <div key={label} className="flex justify-between gap-4 p-3">
                <dt className="text-muted-foreground">{label}</dt>
                <dd className="text-right font-medium">{value}</dd>
              </div>
            ))}
          </dl>

          {serverErrors.length > 0 && (
            <Alert variant="destructive">
              <CircleAlert aria-hidden />
              <AlertTitle>We couldn&apos;t place your order</AlertTitle>
              <AlertDescription>
                <ul className="list-disc pl-4">
                  {serverErrors.map((m) => <li key={m}>{m}</li>)}
                </ul>
              </AlertDescription>
            </Alert>
          )}
        </div>
      )}

      <div className="flex items-center justify-between gap-3">
        {step > 0 ? (
          <Button
            type="button"
            variant="outline"
            disabled={submitting}
            onClick={() => { setServerErrors([]); setStep((s) => s - 1); }}
          >
            Back
          </Button>
        ) : <span />}

        {/* Separate keys: React must never reuse one DOM button for both, or a click on
            "Continue" can turn into a submit of the final step. */}
        {step < LAST_STEP ? (
          <Button key="continue" type="submit">Continue</Button>
        ) : (
          <Button
            key="place-order"
            type="button"
            disabled={submitting}
            onClick={() => void handleSubmit(onSubmit)()}
          >
            {submitting
              ? "Placing order…"
              : review.paymentMethod === "Online"
                ? `Pay ${formatPeso(subtotal)} online`
                : `Place order · ${formatPeso(subtotal)}`}
          </Button>
        )}
      </div>
    </form>
  );
}