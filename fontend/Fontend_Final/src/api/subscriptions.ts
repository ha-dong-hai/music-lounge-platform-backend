import { apiGet, apiPost } from "./client";

// Matches SubscriptionPackageDto exactly (src/MusicLounge.Application/Subscriptions/DTOs).
export interface SubscriptionPackage {
  id: number;
  name: string;
  description: string | null;
  price: number;
  billingCycle: string;
  maxTicketsPerEvent: number;
  hasAiPoster: boolean;
  maxAiPostersPerMonth: number;
  maxTourScenes: number;
  isActive: boolean;
}

// Matches MySubscriptionDto -- entitlement fields are a *snapshot* from when the owner
// subscribed, deliberately not re-read from the live package (see SubscribeToPackageCommandHandler).
export interface MySubscription {
  id: number;
  packageId: number;
  packageName: string;
  startedAt: string;
  expiresAt: string;
  status: string;
  maxTicketsPerEventSnapshot: number;
  hasAiPosterSnapshot: boolean;
  maxAiPostersPerMonthSnapshot: number;
  maxTourScenesSnapshot: number;
}

export interface SubscriptionPaymentInitiation {
  paymentId: number;
  orderId: string;
  amount: number;
  paymentUrl: string;
}

export function getSubscriptionPackages(): Promise<SubscriptionPackage[]> {
  return apiGet<SubscriptionPackage[]>("/subscriptions/packages?activeOnly=true");
}

// Backend returns 200 with a null data payload when the owner has no subscription -- not a 404.
export function getMySubscription(): Promise<MySubscription | null> {
  return apiGet<MySubscription | null>("/subscriptions/my");
}

export function subscribeToPackage(packageId: number): Promise<SubscriptionPaymentInitiation> {
  return apiPost<SubscriptionPaymentInitiation>("/subscriptions/subscribe", { packageId });
}

export function renewSubscription(): Promise<SubscriptionPaymentInitiation> {
  return apiPost<SubscriptionPaymentInitiation>("/subscriptions/renew", {});
}

export function cancelSubscription(): Promise<void> {
  return apiPost<void>("/subscriptions/cancel", {});
}
