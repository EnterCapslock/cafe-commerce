import { z } from "zod";
import { PICKUP_MAX_DAYS, PICKUP_MIN_LEAD_MINUTES, phtLocalToIso } from "@/lib/time";

const PH_MOBILE = /^(\+63|0)9\d{9}$/; // same rule as the API; spaces and dashes are stripped first

export const checkoutSchema = z
  .object({
    customerName: z.string().trim().min(1, "Please enter your name.").max(100, "That name is too long."),
    customerEmail: z
      .string()
      .trim()
      .min(1, "Please enter your email.")
      .max(254, "That email is too long.")
      .pipe(z.email("Enter a valid email address.")),
    customerPhone: z
      .string()
      .trim()
      .min(1, "Please enter your mobile number.")
      .refine(
        (v) => PH_MOBILE.test(v.replace(/[\s-]/g, "")),
        "Enter a valid Philippine mobile number (e.g. 09171234567 or +63 917 123 4567).",
      ),
    pickupChoice: z.enum(["20", "30", "60", "120", "custom"]),
    pickupLocal: z.string(),
    paymentMethod: z.enum(["Online", "PayAtCounter"]),
  })
  .superRefine((data, ctx) => {
    if (data.pickupChoice !== "custom") return;

    const iso = phtLocalToIso(data.pickupLocal);
    if (!iso) {
      ctx.addIssue({ code: "custom", path: ["pickupLocal"], message: "Choose a pickup date and time." });
      return;
    }

    const time = new Date(iso).getTime();
    const now = Date.now();
    if (time < now + PICKUP_MIN_LEAD_MINUTES * 60_000) {
      ctx.addIssue({
        code: "custom",
        path: ["pickupLocal"],
        message: `Pickup must be at least ${PICKUP_MIN_LEAD_MINUTES} minutes from now.`,
      });
    } else if (time > now + PICKUP_MAX_DAYS * 86_400_000) {
      ctx.addIssue({
        code: "custom",
        path: ["pickupLocal"],
        message: `Pickup can be at most ${PICKUP_MAX_DAYS} days ahead.`,
      });
    }
  });

export type CheckoutValues = z.infer<typeof checkoutSchema>;