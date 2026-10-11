"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { CircleAlert } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { PasswordInput } from "@/components/auth/password-input";
import { useSession } from "@/components/auth/session-provider";
import { FieldError } from "@/components/field-error";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { describeApiError } from "@/lib/api-errors";
import { authApi } from "@/lib/auth-api";
import { loginSchema, type LoginValues } from "@/lib/schemas/auth";

export function LoginForm({ next }: { next: string }) {
  const router = useRouter();
  const { session } = useSession();

  useEffect(() => {
    if (session.status === "authenticated") router.replace(next);
  }, [session.status, next, router]);

  // While signed in, the fields are not rendered at all. That destroys any typed values
  // (including the password) and any "Signing in…" state, so nothing stale can come back
  // when this page is shown again after signing out.
  if (session.status === "authenticated") {
    return (
      <p role="status" className="text-sm text-muted-foreground">
        You&apos;re signed in. Redirecting…
      </p>
    );
  }

  return <LoginFields next={next} />;
}

function LoginFields({ next }: { next: string }) {
  const { signIn } = useSession();
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    mode: "onTouched",
    defaultValues: { email: "", password: "" },
  });

  async function onSubmit(values: LoginValues) {
    setServerErrors([]);
    setSubmitting(true);
    try {
      const { user } = await authApi.login({ email: values.email.trim(), password: values.password });
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
          <AlertTitle>We couldn&apos;t sign you in</AlertTitle>
          <AlertDescription>
            <ul className="list-disc pl-4">
              {serverErrors.map((m) => <li key={m}>{m}</li>)}
            </ul>
          </AlertDescription>
        </Alert>
      )}

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
          id="password" autoComplete="current-password" required
          aria-invalid={!!errors.password}
          aria-describedby={errors.password ? "password-error" : undefined}
          {...register("password")}
        />
        <FieldError id="password-error" message={errors.password?.message} />
      </div>

      <Button type="submit" className="w-full" disabled={submitting}>
        {submitting ? "Signing in…" : "Sign in"}
      </Button>

      <p className="text-center text-sm text-muted-foreground">
        New here?{" "}
        <Link href={`/register?next=${encodeURIComponent(next)}`} className="font-medium text-foreground underline underline-offset-4">
          Create an account
        </Link>
      </p>
    </form>
  );
}