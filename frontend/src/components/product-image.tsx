"use client";

import Image from "next/image";
import cloudinaryLoader from "@/lib/cloudinary-loader";

/**
 * A product photo that fills its parent, which must be `relative` and have a size.
 * The alt text is empty on purpose: the product's name always appears right next to it.
 */
export function ProductImage({ url, sizes, priority }: { url: string; sizes: string; priority?: boolean }) {
  return (
    <Image loader={cloudinaryLoader} src={url} alt="" fill sizes={sizes} priority={priority} className="object-cover" />
  );
}