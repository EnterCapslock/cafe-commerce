import { z } from "zod";

export const lookupSchema = z.object({
  code: z
    .string()
    .trim()
    .min(1, "Enter your order code.")
    .refine(
      (value) => /^[0-9a-f]{8}$/i.test(value.replace(/[\s#-]/g, "")),
      "The order code is 8 letters and numbers, like E18B681F.",
    ),
  email: z
    .string()
    .trim()
    .min(1, "Enter the email you used at checkout.")
    .max(254, "That email is too long.")
    .pipe(z.email("Enter a valid email address.")),
});

export type LookupValues = z.infer<typeof lookupSchema>;