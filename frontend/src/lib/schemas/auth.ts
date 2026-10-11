import { z } from "zod";

// Mirrors the API's rules (FluentValidation). The API remains the source of truth.
export const PASSWORD_RULES = [
  { id: "length", label: "At least 8 characters", message: "Password must be at least 8 characters.", test: (p: string) => p.length >= 8 },
  { id: "lower", label: "A lowercase letter", message: "Password needs a lowercase letter.", test: (p: string) => /[a-z]/.test(p) },
  { id: "upper", label: "An uppercase letter", message: "Password needs an uppercase letter.", test: (p: string) => /[A-Z]/.test(p) },
  { id: "digit", label: "A number", message: "Password needs a number.", test: (p: string) => /[0-9]/.test(p) },
  { id: "special", label: "A special character", message: "Password needs a special character.", test: (p: string) => /[^A-Za-z0-9]/.test(p) },
] as const;

const email = z
  .string()
  .trim()
  .min(1, "Please enter your email.")
  .max(254, "That email is too long.")
  .pipe(z.email("Enter a valid email address."));

export const loginSchema = z.object({
  email,
  password: z.string().min(1, "Please enter your password.").max(72, "That password is too long."),
});

export const registerSchema = z.object({
  name: z.string().trim().min(1, "Please enter your name.").max(100, "That name is too long."),
  email,
  password: z
    .string()
    .min(1, "Please create a password.")
    .max(72, "Password can be at most 72 characters.")
    .superRefine((value, ctx) => {
      for (const rule of PASSWORD_RULES) {
        if (!rule.test(value)) ctx.addIssue({ code: "custom", message: rule.message });
      }
    }),
});

export type LoginValues = z.infer<typeof loginSchema>;
export type RegisterValues = z.infer<typeof registerSchema>;