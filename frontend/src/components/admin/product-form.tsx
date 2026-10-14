"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowDown, ArrowUp, CircleAlert, Plus, Trash2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { toast } from "sonner";
import { FieldError } from "@/components/shared/field-error";
import { NativeSelect } from "@/components/shared/native-select";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api/client";
import { describeApiError } from "@/lib/api/errors";
import { productSchema, type ProductFormValues } from "@/lib/schemas/product";
import type { AdminProduct, ProductInput } from "@/types/api";

function toFormValues(product?: AdminProduct): ProductFormValues {
  if (!product) {
    return {
      name: "",
      description: "",
      category: "Coffee",
      isActive: true,
      variants: [{ name: "Regular", price: "", isAvailable: true }],
    };
  }
  return {
    name: product.name,
    description: product.description,
    category: product.category,
    isActive: product.isActive,
    variants: [...product.variants]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((v) => ({ variantId: v.id, name: v.name, price: String(v.price), isAvailable: v.isAvailable })),
  };
}

export function ProductForm({ product }: { product?: AdminProduct }) {
  const router = useRouter();
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const alertRef = useRef<HTMLDivElement>(null);

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isDirty },
  } = useForm<ProductFormValues>({
    resolver: zodResolver(productSchema),
    mode: "onTouched",
    defaultValues: toFormValues(product),
  });
  const { fields, append, remove, move } = useFieldArray({ control, name: "variants" });

  // Warn before a refresh or tab close would throw away unsaved changes.
  useEffect(() => {
    if (!isDirty) return;
    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [isDirty]);

  useEffect(() => {
    if (serverErrors.length > 0) alertRef.current?.scrollIntoView({ block: "center" });
  }, [serverErrors]);

  async function onSubmit(values: ProductFormValues) {
    setServerErrors([]);
    setSubmitting(true);

    const body: ProductInput = {
      name: values.name.trim(),
      description: values.description.trim(),
      category: values.category,
      isActive: values.isActive,
      variants: values.variants.map((v) => ({
        id: v.variantId,
        name: v.name.trim(),
        price: Number(v.price),
        isAvailable: v.isAvailable,
      })),
    };

    try {
      if (product) {
        const updated = await api.updateProduct(product.id, body);
        reset(toFormValues(updated)); // adopt the saved values (new variants get their ids)
        toast.success("Changes saved");
      } else {
        const created = await api.createProduct(body);
        toast.success("Item created. You can add a photo now.");
        router.replace(`/admin/menu/${created.id}`);
        return; // stay in the "saving" state while the page changes
      }
    } catch (error) {
      setServerErrors(describeApiError(error));
    }
    setSubmitting(false);
  }

  const variantsError = errors.variants?.root?.message ?? errors.variants?.message;

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-8">
      {serverErrors.length > 0 && (
        <div ref={alertRef}>
          <Alert variant="destructive">
            <CircleAlert aria-hidden />
            <AlertTitle>We couldn&apos;t save this item</AlertTitle>
            <AlertDescription>
              <ul className="list-disc pl-4">
                {serverErrors.map((m) => <li key={m}>{m}</li>)}
              </ul>
            </AlertDescription>
          </Alert>
        </div>
      )}

      <section className="space-y-4" aria-label="Item details">
        <div className="space-y-2">
          <Label htmlFor="name">Name</Label>
          <Input
            id="name" required aria-invalid={!!errors.name}
            aria-describedby={errors.name ? "name-error" : undefined}
            {...register("name")}
          />
          <FieldError id="name-error" message={errors.name?.message} />
        </div>

        <div className="space-y-2">
          <Label htmlFor="description">Description</Label>
          <Textarea
            id="description" rows={3} required aria-invalid={!!errors.description}
            aria-describedby={errors.description ? "description-error" : undefined}
            {...register("description")}
          />
          <FieldError id="description-error" message={errors.description?.message} />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="category">Category</Label>
            <NativeSelect id="category" {...register("category")}>
              <option value="Coffee">Coffee</option>
              <option value="Food">Food</option>
              <option value="Merchandise">Merchandise</option>
            </NativeSelect>
          </div>

          <div className="flex items-end pb-2">
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" className="size-4 accent-primary" {...register("isActive")} />
              Visible on the menu
            </label>
          </div>
        </div>
      </section>

      <section className="space-y-3" aria-labelledby="variants-heading">
        <div className="space-y-1">
          <h2 id="variants-heading" className="text-lg font-medium">Sizes and options</h2>
          <p className="text-sm text-muted-foreground">
            Customers choose one of these. Untick &quot;Available&quot; for something that&apos;s sold out today.
            Options that appear in past orders can&apos;t be removed, only marked unavailable.
          </p>
        </div>

        <div aria-hidden className="hidden grid-cols-[1fr_9rem_6rem_7rem] gap-3 px-3 text-xs text-muted-foreground sm:grid">
          <span>Name</span>
          <span>Price (₱)</span>
          <span>Available</span>
          <span />
        </div>

        <ul className="space-y-3">
          {fields.map((field, index) => (
            <li
              key={field.id}
              data-testid="variant-row"
              className="grid items-start gap-3 rounded-lg border p-3 sm:grid-cols-[1fr_9rem_6rem_7rem]"
            >
              <div className="space-y-1">
                <Input
                  placeholder="Name, e.g. Large"
                  aria-label={`Variant ${index + 1} name`}
                  aria-invalid={!!errors.variants?.[index]?.name}
                  aria-describedby={errors.variants?.[index]?.name ? `variant-${index}-name-error` : undefined}
                  {...register(`variants.${index}.name`)}
                />
                <FieldError id={`variant-${index}-name-error`} message={errors.variants?.[index]?.name?.message} />
              </div>

              <div className="space-y-1">
                <Input
                  inputMode="decimal"
                  placeholder="Price"
                  aria-label={`Variant ${index + 1} price`}
                  aria-invalid={!!errors.variants?.[index]?.price}
                  aria-describedby={errors.variants?.[index]?.price ? `variant-${index}-price-error` : undefined}
                  {...register(`variants.${index}.price`)}
                />
                <FieldError id={`variant-${index}-price-error`} message={errors.variants?.[index]?.price?.message} />
              </div>

              <label className="flex h-9 items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  className="size-4 accent-primary"
                  aria-label={`Variant ${index + 1} available`}
                  {...register(`variants.${index}.isAvailable`)}
                />
                <span className="sm:sr-only">Available</span>
              </label>

              <div className="flex gap-1">
                <Button
                  type="button" variant="ghost" size="icon" disabled={index === 0}
                  aria-label={`Move variant ${index + 1} up`} onClick={() => move(index, index - 1)}
                >
                  <ArrowUp aria-hidden />
                </Button>
                <Button
                  type="button" variant="ghost" size="icon" disabled={index === fields.length - 1}
                  aria-label={`Move variant ${index + 1} down`} onClick={() => move(index, index + 1)}
                >
                  <ArrowDown aria-hidden />
                </Button>
                <Button
                  type="button" variant="ghost" size="icon" disabled={fields.length === 1}
                  aria-label={`Remove variant ${index + 1}`} onClick={() => remove(index)}
                >
                  <Trash2 aria-hidden />
                </Button>
              </div>
            </li>
          ))}
        </ul>

        <FieldError id="variants-error" message={variantsError} />

        <Button
          type="button" variant="outline" size="sm" disabled={fields.length >= 20}
          onClick={() => append({ name: "", price: "", isAvailable: true })}
        >
          <Plus aria-hidden /> Add a variant
        </Button>
      </section>

      <div className="flex items-center gap-3">
        <Button type="submit" disabled={submitting}>
          {submitting ? "Saving…" : product ? "Save changes" : "Create item"}
        </Button>
        <Button asChild variant="ghost">
          <Link href="/admin/menu">{product ? "Back to menu" : "Cancel"}</Link>
        </Button>
      </div>
    </form>
  );
}