"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Check, Circle, CircleAlert } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { PasswordInput } from "@/components/auth/password-input";
import { useSession } from "@/components/auth/session-provider";
import { FieldError } from "@/components/shared/field-error";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { describeApiError } from "@/lib/api/errors";
import { authApi } from "@/lib/api/auth";
import { PASSWORD_RULES, registerSchema, type RegisterValues } from "@/lib/schemas/auth";
import { cn } from "@/lib/utils";

export function RegisterForm({ next }: { next: string }) {
  const router = useRouter();
  const { session } = useSession();

  useEffect(() => {
    if (session.status === "authenticated") router.replace(next);
  }, [session.status, next, router]);

  if (session.status === "authenticated") {
    return (
      <p role="status" className="text-sm text-muted-foreground">
        You&apos;re signed in. Redirecting…
      </p>
    );
  }

  return <RegisterFields next={next} />;
}

function RegisterFields({ next }: { next: string }) {
  const { signIn } = useSession();
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    mode: "onTouched",
    defaultValues: { name: "", email: "", password: "" },
  });
  const password = useWatch({ control, name: "password" }) ?? "";

  async function onSubmit(values: RegisterValues) {
    setServerErrors([]);
    setSubmitting(true);
    try {
      const { user } = await authApi.register({
        name: values.name.trim(),
        email: values.email.trim(),
        password: values.password,
      });
      signIn(user); // the session change swaps this form for the redirect message
    } catch (error) {
      setServerErrors(describeApiError(error));
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
      {serverErrors.length > 0 && (
        <Alert variant="destructive">
          <CircleAlert aria-hidden />
          <AlertTitle>We couldn&apos;t create your account</AlertTitle>
          <AlertDescription>
            <ul className="list-disc pl-4">
              {serverErrors.map((m) => <li key={m}>{m}</li>)}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      <div className="space-y-2">
        <Label htmlFor="name">Full name</Label>
        <Input
          id="name" autoComplete="name" required placeholder="Juan Dela Cruz"
          aria-invalid={!!errors.name}
          aria-describedby={errors.name ? "name-error" : undefined}
          {...register("name")}
        />
        <FieldError id="name-error" message={errors.name?.message} />
      </div>

      <div className="space-y-2">
        <Label htmlFor="email">Email</Label>
        <Input
          id="email" type="email" autoComplete="email" required placeholder="name@example.com"
          aria-invalid={!!errors.email}
          aria-describedby={errors.email ? "email-error" : undefined}
          {...register("email")}
        />
        <FieldError id="email-error" message={errors.email?.message} />
      </div>

      <div className="space-y-2">
        <Label htmlFor="password">Password</Label>
        <PasswordInput
          id="password" autoComplete="new-password" required
          aria-invalid={!!errors.password}
          aria-describedby="password-rules"
          {...register("password")}
        />
        <ul id="password-rules" aria-label="Password requirements" className="space-y-1 pt-1 text-sm">
          {PASSWORD_RULES.map((rule) => {
            const met = rule.test(password);
            return (
              <li key={rule.id} className={cn("flex items-center gap-2", met ? "text-foreground" : "text-muted-foreground")}>
                {met ? <Check className="size-4" aria-hidden /> : <Circle className="size-4" aria-hidden />}
                <span>{rule.label}</span>
                <span className="sr-only">{met ? "(met)" : "(not met)"}</span>
              </li>
            );
          })}
        </ul>
      </div>

      <Button type="submit" className="w-full" disabled={submitting}>
        {submitting ? "Creating account…" : "Create account"}
      </Button>

      <p className="text-center text-sm text-muted-foreground">
        Already have an account?{" "}
        <Link href={`/login?next=${encodeURIComponent(next)}`} className="font-medium text-foreground underline underline-offset-4">
          Sign in
        </Link>
      </p>
    </form>
  );
}