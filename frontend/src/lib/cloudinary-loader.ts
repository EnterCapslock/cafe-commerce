// Asks Cloudinary for a resized, modern-format copy of an image by editing its URL:
// ".../image/upload/v123/abc.jpg" becomes ".../image/upload/f_auto,q_auto,w_640,c_limit/v123/abc.jpg"
export default function cloudinaryLoader({ src, width, quality }: { src: string; width: number; quality?: number }): string {
  const marker = "/image/upload/";
  const at = src.indexOf(marker);
  if (!src.startsWith("https://res.cloudinary.com/") || at === -1) return src;

  const transforms = `f_auto,q_${quality ?? "auto"},w_${width},c_limit`;
  return `${src.slice(0, at + marker.length)}${transforms}/${src.slice(at + marker.length)}`;
}