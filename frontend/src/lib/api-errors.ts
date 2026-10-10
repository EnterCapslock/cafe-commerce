import { ApiError } from "@/lib/api";

export function describeApiError(error: unknown): string[] {
  if (error instanceof ApiError) {
    if (error.status === 0) return ["We couldn't reach the server. Check your connection and try again."];
    if (error.status === 429) return ["Too many attempts. Please wait a minute and try again."];

    const fieldMessages = error.problem?.errors ? Object.values(error.problem.errors).flat() : [];
    if (fieldMessages.length > 0) return fieldMessages;

    const messages = [error.problem?.title, error.problem?.detail].filter(
      (m): m is string => Boolean(m),
    );
    return messages.length > 0 ? messages : [error.message];
  }
  return ["Something went wrong. Please try again."];
}