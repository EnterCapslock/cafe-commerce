/** Only allow redirects to paths on this site (blocks "?next=https://evil.example"). */
export function safeNext(value: string | string[] | undefined): string {
  const v = Array.isArray(value) ? value[0] : value;
  return v && v.startsWith("/") && !v.startsWith("//") && !v.startsWith("/\\") ? v : "/";
}