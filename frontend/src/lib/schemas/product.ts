import { z } from "zod";

const PRICE = /^\d{1,6}(\.\d{1,2})?$/; // up to 2 decimals, matching the API

const variantSchema = z.object({
  variantId: z.number().optional(), // the server id of an existing variant
  name: z.string().trim().min(1, "Enter a name.").max(50, "50 characters at most."),
  price: z
    .string()
    .trim()
    .min(1, "Enter a price.")
    .refine((v) => PRICE.test(v), "Use up to 2 decimals, like 120 or 120.50.")
    .refine((v) => Number(v) > 0, "The price must be above ₱0.")
    .refine((v) => Number(v) <= 100_000, "The price can be at most ₱100,000."),
  isAvailable: z.boolean(),
});

export const productSchema = z
  .object({
    name: z.string().trim().min(1, "Enter a name.").max(120, "120 characters at most."),
    description: z.string().trim().min(1, "Enter a description.").max(1000, "1,000 characters at most."),
    category: z.enum(["Coffee", "Food", "Merchandise"]),
    isActive: z.boolean(),
    variants: z.array(variantSchema).min(1, "Add at least one variant.").max(20, "At most 20 variants."),
  })
  .superRefine((data, ctx) => {
    const seen = new Set<string>();
    data.variants.forEach((variant, index) => {
      const key = variant.name.trim().toLowerCase();
      if (!key) return;
      if (seen.has(key)) {
        ctx.addIssue({ code: "custom", path: ["variants", index, "name"], message: "Variant names must be different." });
      }
      seen.add(key);
    });
  });

export type ProductFormValues = z.infer<typeof productSchema>;