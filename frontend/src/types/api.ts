export type ProductCategory = "Coffee" | "Food" | "Merchandise";
export type OrderStatus = "Pending" | "Preparing" | "ReadyForPickup" | "Completed" | "Cancelled";
export type PaymentMethod = "Online" | "PayAtCounter";
export type PaymentStatus = "Unpaid" | "Paid" | "Expired";

export interface VariantDto {
  id: number;
  name: string;
  price: number;
  isAvailable: boolean;
}

export interface ProductSummary {
  id: number;
  name: string;
  description: string;
  imageUrl: string | null;
  category: ProductCategory;
  fromPrice: number;
  isAvailable: boolean;
  variantCount: number;
}

export interface ProductDetail extends Omit<ProductSummary, "variantCount"> {
  variants: VariantDto[];
}

export interface OrderItemDto {
  variantId: number;
  productName: string;
  variantName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface OrderDto {
  id: string;
  status: OrderStatus;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  pickupTime: string;
  totalAmount: number;
  createdAt: string;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  checkoutUrl: string | null;
  paidAt: string | null;
  items: OrderItemDto[];
}

export interface CreateOrderRequest {
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  pickupTime: string; // ISO 8601
  paymentMethod: PaymentMethod;
  items: { variantId: number; quantity: number }[];
}

/** RFC 7807 shape returned by every API error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}