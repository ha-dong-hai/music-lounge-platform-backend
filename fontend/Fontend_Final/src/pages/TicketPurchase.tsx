import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, getStoredToken } from "../api/client";
import { getShowDetail, LoungeShowDetail, TicketPriceSummary } from "../api/shows";
import { cancelHold, holdTicket, purchaseTicket } from "../api/tickets";
import { formatVnd } from "../lib/currency";
import AudienceHeader from "../components/AudienceHeader";
import AudienceFooter from "../components/AudienceFooter";

// Third Audience/Guest screen, same AuraLounge system as DiscoverShows.tsx/ShowDetail.tsx. No
// Stitch screen for this at all -- built directly from the real contract (HoldTicketCommand,
// PurchaseTicketCommand, docs/journeys/21-anonymous-journey.md Journey 4's ticket-purchase hard-stop).
//
// Zone/seat model confirmed against the real backend, not assumed: HoldTicketCommand only takes
// (PriceId, Quantity) -- there is no seat-level identifier anywhere in this flow. Venues sell by
// zone capacity (SeatingMapDto.ZoneMapEntryDto.Capacity/AvailableCount), never assigned seats, so
// this screen is a price-tier picker + quantity stepper, not a seat map.
//
// RequireAuthenticated on the whole TicketsController -- guarded client-side too (redirect to
// /login) since ShowDetail.tsx's own guard only covers the click that gets here, not someone
// landing on this URL directly.
//
// 2026-08-18: swapped the old standalone plain header for the shared AudienceHeader/AudienceFooter
// (this page had no Stitch mock to port, but the header/footer inconsistency vs DiscoverShows/
// MyTickets/ShowDetail was a real, flagged loose end) and corrected border-radius to the real
// AuraLounge scale from stitch_ai_audience/aura_echo/DESIGN.md -- rounded-[4px] for buttons/cards
// (DESIGN.md "Standard Elements"), true rounded-full only for small icon-only circular buttons
// (the stepper +/-), not for cards.

const HOLD_WARNING_SECONDS = 60;

function formatCountdown(seconds: number): string {
  const m = Math.floor(seconds / 60)
    .toString()
    .padStart(2, "0");
  const s = Math.floor(seconds % 60)
    .toString()
    .padStart(2, "0");
  return `${m}:${s}`;
}

export default function TicketPurchase() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [show, setShow] = useState<LoungeShowDetail | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [selectedPriceId, setSelectedPriceId] = useState<number | null>(null);
  const [quantity, setQuantity] = useState(1);
  const [holdError, setHoldError] = useState<string | null>(null);
  const [holding, setHolding] = useState(false);

  const [hold, setHold] = useState<{ holdId: number; expiresAt: number } | null>(null);
  const [now, setNow] = useState(() => Date.now());
  const [purchasing, setPurchasing] = useState(false);
  const [purchaseError, setPurchaseError] = useState<string | null>(null);
  const cancellingRef = useRef(false);

  useEffect(() => {
    if (!getStoredToken()) {
      navigate("/login");
    }
  }, [navigate]);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    getShowDetail(Number(id))
      .then((result) => {
        if (!cancelled) setShow(result);
      })
      .catch(() => {
        if (!cancelled) setLoadError("Không tìm thấy show này.");
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  // Tick every second while a hold is active so the countdown stays accurate to the server's own
  // ExpiresAt, not a client-side timer that could drift.
  useEffect(() => {
    if (!hold) return;
    const interval = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(interval);
  }, [hold]);

  const remainingSeconds = hold ? Math.max(0, Math.round((hold.expiresAt - now) / 1000)) : null;
  const expired = remainingSeconds === 0;

  const allPrices: { tierName: string; price: TicketPriceSummary }[] =
    show?.ticketTiers.flatMap((tier) => tier.prices.map((price) => ({ tierName: tier.name, price }))) ?? [];
  const selected = allPrices.find((p) => p.price.id === selectedPriceId) ?? null;
  const maxQuantity = selected?.price.availableSlots ?? 1;

  async function handleHold() {
    if (!selectedPriceId) return;
    setHoldError(null);
    setHolding(true);
    try {
      const result = await holdTicket(selectedPriceId, quantity);
      setHold({ holdId: result.holdId, expiresAt: new Date(result.expiresAt).getTime() });
    } catch (err) {
      setHoldError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setHolding(false);
    }
  }

  async function handleCancelHold() {
    if (!hold || cancellingRef.current) return;
    cancellingRef.current = true;
    try {
      await cancelHold(hold.holdId);
    } catch {
      // Best-effort -- if this fails the hold still self-expires via the server's own
      // release-expired-holds job (runs every minute), nothing to show the user for this.
    } finally {
      cancellingRef.current = false;
      setHold(null);
    }
  }

  async function handlePurchase() {
    if (!hold) return;
    setPurchaseError(null);
    setPurchasing(true);
    try {
      const result = await purchaseTicket(hold.holdId);
      // Survives the full-page redirect to VNPay and back -- PaymentResult.tsx reads this to show
      // ticket-specific copy instead of its subscription-flow default (see that file's own note).
      sessionStorage.setItem("musiclounge_payment_context", "ticket");
      window.location.href = result.paymentUrl;
    } catch (err) {
      setPurchaseError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      setPurchasing(false);
    }
  }

  if (loadError) {
    return (
      <div className="bg-[#fbf9f4] min-h-screen flex items-center justify-center text-[#1b1c19] px-6" style={{ fontFamily: "'Manrope', sans-serif" }}>
        <div className="text-center">
          <p className="text-lg mb-2" style={{ fontFamily: "'Libre Caslon Text', serif" }}>{loadError}</p>
          <Link to="/shows" className="text-sm text-[#4d4540] hover:text-[#181512] underline">Quay lại danh sách show</Link>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-[#fbf9f4] min-h-screen text-[#1b1c19] flex flex-col" style={{ fontFamily: "'Manrope', sans-serif" }}>
      <AudienceHeader active="Sự kiện" />

      <div className="flex-1 max-w-2xl mx-auto px-6 py-10 w-full">
        {id && (
          <Link to={`/shows/${id}`} className="inline-block text-sm text-[#4d4540] hover:text-[#181512] transition-colors mb-6">
            ← Quay lại show
          </Link>
        )}
        {!show ? (
          <p className="text-center text-[#4d4540] py-16">Đang tải...</p>
        ) : (
          <>
            <p className="text-xs tracking-[0.15em] text-[#7e756f] mb-2">MUA VÉ</p>
            <h1 className="text-3xl mb-1" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
              {show.name}
            </h1>
            <p className="text-sm text-[#4d4540] mb-8">{show.lounge.name} — {show.lounge.fullAddress}</p>

            {!hold ? (
              <div>
                <h2 className="text-lg mb-4" style={{ fontFamily: "'Libre Caslon Text', serif" }}>Chọn hạng vé</h2>
                <div className="space-y-3 mb-6">
                  {allPrices.map(({ tierName, price }) => {
                    const soldOut = price.availableSlots === 0;
                    const isSelected = selectedPriceId === price.id;
                    return (
                      <button
                        key={price.id}
                        disabled={soldOut}
                        onClick={() => {
                          setSelectedPriceId(price.id);
                          setQuantity(1);
                          setHoldError(null);
                        }}
                        className={`w-full text-left flex items-center justify-between px-4 py-3.5 rounded-[4px] border transition-colors ${
                          soldOut
                            ? "border-[#1b1c19]/10 bg-[#f0eee9] opacity-50 cursor-not-allowed"
                            : isSelected
                            ? "border-[#181512] bg-[#f5f3ee]"
                            : "border-[#1b1c19]/10 hover:border-[#181512]/40"
                        }`}
                      >
                        <div>
                          <p className="text-sm font-medium">{tierName} — {price.name}</p>
                          <p className="text-xs text-[#7e756f]">
                            {soldOut ? "Hết vé" : price.availableSlots !== null ? `Còn ${price.availableSlots} vé` : "Còn vé"}
                          </p>
                        </div>
                        <p className="text-sm font-medium">{formatVnd(price.price)}</p>
                      </button>
                    );
                  })}
                  {allPrices.length === 0 && (
                    <p className="text-sm text-[#7e756f]">Chưa mở bán vé cho show này.</p>
                  )}
                </div>

                {selected && (
                  <div className="flex items-center justify-between mb-6 px-4 py-3.5 rounded-[4px] bg-white border border-[#1b1c19]/10">
                    <span className="text-sm">Số lượng</span>
                    <div className="flex items-center gap-4">
                      <button
                        onClick={() => setQuantity((q) => Math.max(1, q - 1))}
                        className="w-8 h-8 rounded-full border border-[#cfc4bd] flex items-center justify-center hover:bg-[#f0eee9]"
                      >
                        −
                      </button>
                      <span className="w-6 text-center">{quantity}</span>
                      <button
                        onClick={() => setQuantity((q) => Math.min(maxQuantity, q + 1))}
                        disabled={quantity >= maxQuantity}
                        className="w-8 h-8 rounded-full border border-[#cfc4bd] flex items-center justify-center hover:bg-[#f0eee9] disabled:opacity-40"
                      >
                        +
                      </button>
                    </div>
                  </div>
                )}

                {holdError && <p className="text-sm text-[#ba1a1a] mb-4">{holdError}</p>}

                <button
                  onClick={handleHold}
                  disabled={!selectedPriceId || holding}
                  className="w-full bg-[#181512] text-white text-sm font-medium py-3.5 rounded-[4px] hover:bg-[#181512]/90 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  {holding ? "Đang giữ chỗ..." : selected ? `Giữ chỗ — ${formatVnd(selected.price.price * quantity)}` : "Chọn hạng vé để tiếp tục"}
                </button>
              </div>
            ) : expired ? (
              <div className="text-center py-10">
                <p className="text-lg mb-2 text-[#ba1a1a]" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                  Đã hết thời gian giữ chỗ
                </p>
                <p className="text-sm text-[#4d4540] mb-6">Vé đã được nhả lại cho người khác. Vui lòng chọn lại.</p>
                <button
                  onClick={() => setHold(null)}
                  className="px-6 py-3 rounded-[4px] bg-[#181512] text-white text-sm hover:bg-[#181512]/90"
                >
                  Chọn lại vé
                </button>
              </div>
            ) : (
              <div>
                <div className="rounded-[4px] border border-[#e9c176] bg-[#fef8e9] px-4 py-3 mb-6 flex items-center justify-between">
                  <p className="text-sm">Giữ chỗ còn hiệu lực</p>
                  <p
                    className={`text-lg font-medium ${remainingSeconds !== null && remainingSeconds < HOLD_WARNING_SECONDS ? "text-[#ba1a1a]" : "text-[#181512]"}`}
                  >
                    {formatCountdown(remainingSeconds ?? 0)}
                  </p>
                </div>

                {selected && (
                  <div className="rounded-[4px] border border-[#1b1c19]/10 bg-white px-4 py-4 mb-6">
                    <div className="flex items-center justify-between text-sm mb-1">
                      <span>{selected.tierName} — {selected.price.name} × {quantity}</span>
                      <span>{formatVnd(selected.price.price * quantity)}</span>
                    </div>
                    <div className="flex items-center justify-between text-base font-medium mt-2 pt-2 border-t border-[#1b1c19]/10">
                      <span>Tổng cộng</span>
                      <span>{formatVnd(selected.price.price * quantity)}</span>
                    </div>
                  </div>
                )}

                {purchaseError && <p className="text-sm text-[#ba1a1a] mb-4">{purchaseError}</p>}

                <div className="flex gap-3">
                  <button
                    onClick={handleCancelHold}
                    disabled={purchasing}
                    className="px-5 py-3.5 rounded-[4px] border border-[#cfc4bd] text-sm hover:bg-[#f0eee9] transition-colors disabled:opacity-40"
                  >
                    Huỷ giữ chỗ
                  </button>
                  <button
                    onClick={handlePurchase}
                    disabled={purchasing}
                    className="flex-1 bg-[#181512] text-white text-sm font-medium py-3.5 rounded-[4px] hover:bg-[#181512]/90 transition-colors disabled:opacity-60"
                  >
                    {purchasing ? "Đang chuyển tới VNPay..." : "Thanh toán qua VNPay"}
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </div>

      <AudienceFooter />
    </div>
  );
}
