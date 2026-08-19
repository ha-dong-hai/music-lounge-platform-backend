import { apiDelete, apiGet, apiPost, getStoredToken } from "./client";
import { AccessType } from "./shows";

// Matches HoldTicketCommand(PriceId, Quantity) exactly (TicketsController.Hold) -- no zone/seat
// selection here, just which TicketPriceSummary row + how many. Zone is implicit via the price's
// parent tier (TicketTierSummary.zoneId) -- there is no seat-level identifier anywhere in this
// flow, venues sell by zone capacity, not assigned seats (see SeatingMapDto/ZoneMapEntryDto: a
// zone has a Capacity/AvailableCount, never individual seat numbers).
export interface HoldTicketResult {
  holdId: number;
  expiresAt: string;
}

export function holdTicket(priceId: number, quantity: number): Promise<HoldTicketResult> {
  return apiPost<HoldTicketResult>("/tickets/holds", { priceId, quantity });
}

export function cancelHold(holdId: number): Promise<void> {
  return apiDelete<void>(`/tickets/holds/${holdId}`);
}

// Matches PaymentInitiationDto exactly. PaymentUrl is the VNPay redirect -- browser navigates
// there directly (window.location.href), it isn't fetched via apiGet. ClientIpAddress is captured
// server-side from the connection (TicketsController.Purchase), never sent by the client.
export interface PaymentInitiation {
  paymentId: number;
  orderId: string;
  amount: number;
  paymentUrl: string;
  ticketIds: string[];
}

export function purchaseTicket(holdId: number): Promise<PaymentInitiation> {
  return apiPost<PaymentInitiation>("/tickets/purchase", { holdId });
}

// ---------------------------------------------------------------------------
// My Tickets (GET /tickets/my, GET /tickets/{id}, GET /tickets/{id}/qr, POST /tickets/{id}/cancel)
// -- all RequireAuthenticated, ticket id is a Guid (string on the wire).
// ---------------------------------------------------------------------------

export type TicketStatus = "Pending" | "Confirmed" | "Used" | "Cancelled" | "Refunded";

export interface TicketListItem {
  id: string;
  showId: number;
  showName: string;
  loungeName: string;
  loungeCity: string;
  showScheduledStart: string;
  tierName: string;
  priceName: string;
  pricePaid: number;
  accessType: AccessType;
  status: TicketStatus;
  qrCode: string | null;
  purchasedAt: string;
  hasPendingTransfer: boolean;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export function getMyTickets(page = 1, pageSize = 20): Promise<PaginatedResult<TicketListItem>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiGet<PaginatedResult<TicketListItem>>(`/tickets/my?${params.toString()}`);
}

export interface PhysicalDetail {
  seatInfo: string | null;
  checkedInAt: string | null;
}

export interface LivestreamDetail {
  accessToken: string | null;
}

export interface TicketDetail {
  id: string;
  showName: string;
  loungeName: string;
  loungeAddress: string;
  showScheduledStart: string;
  showScheduledEnd: string | null;
  tierName: string;
  priceName: string;
  pricePaid: number;
  accessType: AccessType;
  status: TicketStatus;
  qrCode: string | null;
  purchasedAt: string;
  physicalDetail: PhysicalDetail | null;
  livestreamDetail: LivestreamDetail | null;
}

export function getTicketDetail(id: string): Promise<TicketDetail> {
  return apiGet<TicketDetail>(`/tickets/${id}`);
}

// Not apiGet -- this endpoint returns raw `image/svg+xml`, not the {success,data} JSON envelope
// every other endpoint uses, so it can't go through apiGet's response unwrapping. A plain <img
// src="/api/v1/tickets/{id}/qr"> can't work either since RequireAuthenticated needs the bearer
// token as a header, which <img> has no way to attach -- fetch it manually and inline the SVG
// markup instead (safe here: this is our own backend's QR-generator output, Net.Codecrete.
// QrCodeGenerator, never user-controlled content).
export async function getTicketQrSvg(id: string): Promise<string> {
  const token = getStoredToken();
  const res = await fetch(`/api/v1/tickets/${id}/qr`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });
  if (!res.ok) throw new Error(`QR fetch failed (${res.status})`);
  return res.text();
}

// Returns the new RefundRequest's id (CancelTicketCommand) -- the ticket's own Status flips to
// Cancelled server-side, a separate refund workflow (Group I) picks up from there. Only makes
// sense to offer while Status is Confirmed; the backend itself will 400 on anything else.
export function cancelTicket(id: string): Promise<number> {
  return apiPost<number>(`/tickets/${id}/cancel`, {});
}

// POST /tickets/{id}/transfer -- 204, recipient must already have a MusicLounge account (matched
// by email); they accept via POST /tickets/{id}/transfer/accept (not wired here yet, no "incoming
// transfers" screen built). 409 if the ticket already has a pending transfer or isn't Confirmed.
export function initiateTicketTransfer(id: string, recipientEmail: string): Promise<void> {
  return apiPost<void>(`/tickets/${id}/transfer`, { recipientEmail });
}

// ---------------------------------------------------------------------------
// Staff box office (POST /tickets/walk-in, RequireVenueOperator = Staff/Owner/Admin, scoped to
// the caller's own venue via VenueOperatorAccess.CanOperate) -- a genuinely different purchase
// path from HoldTicket/PurchaseTicket above, not a UI variant of it: Payment.Method is Cash and
// Status is Confirmed immediately (no VNPay redirect, no hold/expiry step), matching
// SellWalkInTicketCommandHandler exactly. Only sells TicketPrice rows whose PurchaseChannel is
// Offline/Both on a Physical-access tier -- Online-only prices are rejected server-side with a
// real error message, shown as-is rather than duplicated client-side.
// ---------------------------------------------------------------------------

// Matches PaymentMethod exactly (Domain/Enums/PaymentMethod.cs) minus Gateway -- the box office
// validator rejects Gateway server-side (SellWalkInTicketCommandValidator), it's a VNPay-redirect
// concept that makes no sense at a counter. BankTransfer isn't a VNPay call: Staff just confirms
// the customer's transfer already landed (e.g. scanning the venue's own bank QR at the counter)
// and the sale settles immediately, same as Cash.
export type WalkInPaymentMethod = "Cash" | "BankTransfer";

export interface WalkInSaleResult {
  paymentId: number;
  amount: number;
  ticketIds: string[];
  method: WalkInPaymentMethod;
}

export function sellWalkInTicket(
  priceId: number, quantity: number, method: WalkInPaymentMethod
): Promise<WalkInSaleResult> {
  return apiPost<WalkInSaleResult>("/tickets/walk-in", { priceId, quantity, method });
}
