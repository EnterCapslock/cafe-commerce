"use client";

import { useRef, useState, type ChangeEvent } from "react";
import { toast } from "sonner";
import { ProductImage } from "@/components/shared/product-image";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api/client";
import { describeApiError } from "@/lib/api/errors";
import { prepareImage } from "@/lib/image-prep";

export function ProductImageUploader({ productId, initialUrl }: { productId: number; initialUrl: string | null }) {
  const [url, setUrl] = useState(initialUrl);
  const [busy, setBusy] = useState<"upload" | "remove" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  async function onFile(event: ChangeEvent<HTMLInputElement>) {
    const chosen = event.target.files?.[0];
    event.target.value = ""; // lets the same file be chosen again later
    if (!chosen) return;

    setError(null);
    setBusy("upload");
    try {
      const prepared = await prepareImage(chosen);
      if (!prepared.ok) {
        setError(prepared.message);
        return;
      }
      const updated = await api.uploadProductImage(productId, prepared.file);
      setUrl(updated.imageUrl);
      toast.success("Photo saved");
    } catch (e) {
      setError(describeApiError(e)[0]);
    } finally {
      setBusy(null);
    }
  }

  async function removePhoto() {
    setError(null);
    setBusy("remove");
    try {
      await api.removeProductImage(productId);
      setUrl(null);
      toast.success("Photo removed");
    } catch (e) {
      setError(describeApiError(e)[0]);
    } finally {
      setBusy(null);
    }
  }

  return (
    <section aria-labelledby="photo-heading" className="space-y-3 rounded-xl border p-4 lg:sticky lg:top-24">
      <h2 id="photo-heading" className="font-medium">Photo</h2>

      <div className="relative aspect-4/3 overflow-hidden rounded-lg bg-muted">
        {url ? (
          <ProductImage url={url} sizes="288px" />
        ) : (
          <div className="flex h-full items-center justify-center text-sm text-muted-foreground">No photo yet</div>
        )}
      </div>

      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        aria-label="Choose a photo"
        tabIndex={-1}
        className="sr-only"
        onChange={onFile}
      />

      <div className="flex flex-wrap gap-2">
        <Button type="button" size="sm" disabled={busy !== null} onClick={() => inputRef.current?.click()}>
          {busy === "upload" ? "Uploading…" : url ? "Replace photo" : "Upload photo"}
        </Button>
        {url && (
          <Button type="button" size="sm" variant="outline" disabled={busy !== null} onClick={() => void removePhoto()}>
            {busy === "remove" ? "Removing…" : "Remove"}
          </Button>
        )}
      </div>

      {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
      <p className="text-xs text-muted-foreground">JPEG, PNG, or WebP. Large photos are resized automatically.</p>
    </section>
  );
}