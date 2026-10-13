import type { Metadata } from "next";
import { MenuManager } from "@/components/admin/menu-manager";

export const metadata: Metadata = { title: "Menu" };

export default function AdminMenuPage() {
  return <MenuManager />;
}