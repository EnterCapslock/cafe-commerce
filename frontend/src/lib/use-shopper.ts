import { useSession } from "@/components/auth/session-provider";

export function useShopper() {
  const { session } = useSession();
  const user = session.status === "authenticated" ? session.user : null;

  return {
    user,
    isAdmin: user?.role === "Admin",
    /** The signed-in customer's id. Null for guests, admins, and while the session is loading. */
    customerId: user?.role === "Customer" ? user.id : null,
  };
}