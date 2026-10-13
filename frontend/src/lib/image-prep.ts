// Shrinks big photos in the browser before upload: faster, cheaper, and under server limits.
const ACCEPTED = ["image/jpeg", "image/png", "image/webp"];
const MAX_SIDE = 1600;
const MAX_INPUT_BYTES = 15 * 1024 * 1024; // refuse enormous originals before decoding them
const SERVER_LIMIT_BYTES = 4 * 1024 * 1024;
const KEEP_AS_IS_BYTES = 1_500_000;

export type PreparedImage = { ok: true; file: File } | { ok: false; message: string };

function passThrough(file: File): PreparedImage {
  return file.size <= SERVER_LIMIT_BYTES
    ? { ok: true, file }
    : { ok: false, message: "That image is too large to upload. Please choose a smaller one." };
}

export async function prepareImage(file: File): Promise<PreparedImage> {
  if (!ACCEPTED.includes(file.type)) return { ok: false, message: "Please choose a JPEG, PNG, or WebP image." };
  if (file.size > MAX_INPUT_BYTES) return { ok: false, message: "That image is too large. Please choose one under 15 MB." };

  try {
    const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
    const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));

    if (scale === 1 && file.size <= KEEP_AS_IS_BYTES) {
      bitmap.close();
      return { ok: true, file };
    }

    const canvas = document.createElement("canvas");
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    const context = canvas.getContext("2d");
    if (!context) {
      bitmap.close();
      return passThrough(file);
    }
    context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close();

    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/webp", 0.85));
    if (!blob) return passThrough(file);

    const extension = blob.type === "image/webp" ? "webp" : "png"; // some browsers can't encode WebP
    const base = file.name.replace(/\.[^.]+$/, "") || "photo";
    return { ok: true, file: new File([blob], `${base}.${extension}`, { type: blob.type }) };
  } catch {
    return passThrough(file); // couldn't decode it here: let the server decide
  }
}