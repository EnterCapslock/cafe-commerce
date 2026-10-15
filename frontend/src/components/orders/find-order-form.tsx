"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { CircleAlert } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { FieldError } from "@/components/shared/field-error";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api, ApiError } from "@/lib/api/client";
import { describeApiError } from "@/lib/api/errors";
import { rememberOrder } from "@/lib/last-order";
import { lookupSchema, type LookupValues } from "@/lib/schemas/lookup";

export function FindOrderForm() {
  const router = useRouter();
  const [messages, setMessages] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LookupValues>({
    resolver: zodResolver(lookupSchema),
    mode: "onTouched",
    defaultValues: { code: "", email: "" },
  });

  async function onSubmit(values: LookupValues) {
    setMessages([]);
    setSubmitting(true);
    try {
      const { orderId } = await api.lookupOrder({ code: values.code, email: values.email });
      rememberOrder(orderId);            // so "Track order" appears in the header on this device
      router.push(`/orders/${orderId}`); // stays in the "submitting" state while the page changes
    } catch (error) {
      setMessages(
        error instanceof ApiError && error.status === 429
          ? ["Too many attempts. Please wait a while and try again."]
          : describeApiError(error),
      );
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
      {messages.length > 0 && (
        <Alert variant="destructive">
          <CircleAlert aria-hidden />
          <AlertTitle>No matching order</AlertTitle>
          <AlertDescription>
            <ul className="list-disc pl-4">
              {messages.map((m) => <li key={m}>{m}</li>)}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      <div className="space-y-2">
        <Label htmlFor="code">Order code</Label>
        <Input
          id="code" required placeholder="E18B681F" autoComplete="off" autoCapitalize="characters" spellCheck={false}
          aria-invalid={!!errors.code}
          aria-describedby={errors.code ? "code-error" : "code-hint"}
          {...register("code")}
        />
        <p id="code-hint" className="text-sm text-muted-foreground">
          It&apos;s at the top of your order page, next to &quot;Order&quot;.
        </p>
        <FieldError id="code-error" message={errors.code?.message} />
      </div>

      <div className="space-y-2">
        <Label htmlFor="lookup-email">Email used at checkout</Label>
        <Input
          id="lookup-email" type="email" required autoComplete="email" placeholder="name@example.com"
          aria-invalid={!!errors.email}
          aria-describedby={errors.email ? "lookup-email-error" : undefined}
          {...register("email")}
        />
        <FieldError id="lookup-email-error" message={errors.email?.message} />
      </div>

      <Button type="submit" className="w-full" disabled={submitting}>
        {submitting ? "Looking…" : "Find my order"}
      </Button>

      <p className="text-center text-sm text-muted-foreground">
        Ordered with an account?{" "}
        <Link href="/account/orders" className="font-medium text-foreground underline underline-offset-4">
          See all your orders
        </Link>
      </p>
    </form>
  );
}