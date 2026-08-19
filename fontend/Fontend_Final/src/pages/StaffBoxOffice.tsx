import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Armchair, Bank, Minus, Money, Plus, Receipt, Storefront, Ticket as TicketIcon } from "@phosphor-icons/react";
import { ApiError, getStoredToken, getStoredUser } from "../api/client";
import {
  LoungeShowDetail,
  LoungeShowListItem,
  SeatingMap,
  getLoungeShowsByLounge,
  getShowDetail,
  getShowSeatingMap,
} from "../api/shows";
import { WalkInPaymentMethod, sellWalkInTicket } from "../api/tickets";
import { formatVnd } from "../lib/currency";
import StaffHeader from "../components/StaffHeader";

// Ported from
// stitch/stitch_ai_powered_hybrid_music_lounge_platform (4)/.../staff_walk_in_sales_pos
// (raw code.html the user pasted directly). Structural layout kept (left: show picker + sale
// panel, right: sticky order sidebar + totals + action buttons) but the core interaction model is
// NOT ported literally -- the mock assumes individually-selectable seats (Table 1 Seat C, Row A
// Seat 6, a full seat-map grid with per-seat available/selected/sold/VIP state). This backend has
// no seat-level identifier anywhere: verified against SellWalkInTicketCommand(PriceId, Quantity)
// and SeatingMapDto/ZoneMapEntryDto (Capacity/AvailableCount only). Rebuilt as a price-tier +
// quantity picker instead -- the real unit this system sells.
//
// Real backend, not a stub: POST /tickets/walk-in creates real Confirmed Ticket rows immediately
// with a Cash Payment (no VNPay redirect) -- verified via SellWalkInTicketCommandHandler, which
// re-runs the exact same quota/capacity chain as the online HoldTicket flow inside the same
// IShowBookingLock, so it can't oversell relative to online holds.
//
// Two mock features dropped, not faked, because nothing backs them yet (researched before
// building, not guessed):
//   - "Attach Guest Profile": Ticket.BuyerId is hard-set to null on every walk-in sale
//     (SellWalkInTicketCommandHandler), the command has no buyer/guest param at all, and the only
//     "search users" endpoint (GetUsersQuery) is Admin-only (GET /admin/users) -- Staff has no
//     reachable lookup. A walk-in ticket is genuinely anonymous today.
//   - "Taxes & Fees" line: no separate tax field exists anywhere in this app's money model (every
//     other price/total in the codebase is VND-inclusive, no VAT breakout) -- would be a fabricated
//     number on a money screen, the exact class of bug multiple prior BLOCKERs here were about.
//     Subtotal = Total.
//
// "Tonight's Performances" with a sold% badge per card also has no cheap real backing (no
// precomputed sold% field, no "today at my venue" query) -- simplified to a plain show picker list
// (name + time + format), sold% only shown once a show is selected and its real tier
// quota/availableSlots are already being fetched anyway for the sale panel itself.
//
// 2026-08-18: two real gaps closed, not decorative additions --
//   - Payment method: SellWalkInTicketCommand was hardcoded to PaymentMethod.Cash server-side, so
//     a customer paying by bank transfer at the counter still got recorded as Cash. Backend now
//     takes Method (Cash | BankTransfer, PaymentMethod.Gateway rejected -- that's the VNPay-redirect
//     online path, meaningless at a physical counter) -- see SellWalkInTicketCommandValidator.
//   - Seating zone picker: GET /lounge-shows/{id}/seating-map (SeatingMapDto/ZoneMapEntryDto) has
//     existed since the venue-360-tour work but was never called from any frontend page -- a fully
//     wired, verified-live endpoint sitting completely orphaned. Wired in here as a zone chip row
//     (always renders, since TicketTierSummary.zoneId is already on every tier) plus a % -positioned
//     floor-plan visual on top of Lounge.AreaLayoutImageUrl when a zone actually has Layout2D
//     coordinates set. Confirmed via SetZoneLayout2DCommandValidator that this frontend has NO
//     Owner-facing editor for those coordinates anywhere yet, so the floor-plan is a real progressive
//     enhancement that will render empty today for most/all venues -- the chip row is what actually
//     carries the feature until that editor exists, not a fallback for a broken case.

interface CartLine {
  priceId: number;
  tierName: string;
  priceName: string;
  unitPrice: number;
  quantity: number;
  maxQuantity: number;
}

function formatShowTime(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString("vi-VN", { weekday: "short", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
}

function isToday(iso: string): boolean {
  const d = new Date(iso);
  const now = new Date();
  return d.toDateString() === now.toDateString();
}

export default function StaffBoxOffice() {
  const navigate = useNavigate();
  const user = getStoredUser();
  const loungeId = user?.loungeId ?? null;

  const [shows, setShows] = useState<LoungeShowListItem[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [selectedShowId, setSelectedShowId] = useState<number | null>(null);
  const [showDetail, setShowDetail] = useState<LoungeShowDetail | null>(null);
  const [seatingMap, setSeatingMap] = useState<SeatingMap | null>(null);
  const [selectedZoneId, setSelectedZoneId] = useState<number | null>(null);

  const [quantities, setQuantities] = useState<Record<number, number>>({});
  const [paymentMethod, setPaymentMethod] = useState<WalkInPaymentMethod>("Cash");

  const [selling, setSelling] = useState(false);
  const [saleError, setSaleError] = useState<string | null>(null);
  const [receipt, setReceipt] = useState<{ ticketCount: number; amount: number; method: WalkInPaymentMethod } | null>(null);

  useEffect(() => {
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }
    if (loungeId === null) return;
    let cancelled = false;
    getLoungeShowsByLounge(loungeId, 1, 50)
      .then((result) => {
        if (!cancelled) setShows(result.items);
      })
      .catch((err) => {
        if (!cancelled) setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
    return () => {
      cancelled = true;
    };
  }, [navigate, loungeId]);

  const todayShows = useMemo(() => (shows ?? []).filter((s) => isToday(s.scheduledStart)), [shows]);
  const otherUpcoming = useMemo(
    () => (shows ?? []).filter((s) => !isToday(s.scheduledStart) && new Date(s.scheduledStart) > new Date()),
    [shows]
  );

  function selectShow(id: number) {
    setSelectedShowId(id);
    setShowDetail(null);
    setSeatingMap(null);
    setSelectedZoneId(null);
    setQuantities({});
    setReceipt(null);
    setSaleError(null);
    getShowDetail(id)
      .then(setShowDetail)
      .catch((err) => setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ."));
    // Best-effort -- a show with no zoned tiers (pure general-admission) legitimately has an empty
    // zones list, not an error; the zone section itself just renders nothing in that case.
    getShowSeatingMap(id)
      .then(setSeatingMap)
      .catch(() => setSeatingMap({ areaLayoutImageUrl: null, zones: [] }));
  }

  // Only Physical-access tiers with an Offline/Both price can be sold at the box office -- the
  // backend enforces this too (SellWalkInTicketCommandHandler rejects Online-only prices), this is
  // just pre-filtering for a cleaner picker rather than letting Staff pick something guaranteed to
  // fail.
  const sellableRows = useMemo(() => {
    if (!showDetail) return [];
    const rows: {
      priceId: number;
      tierName: string;
      priceName: string;
      unitPrice: number;
      availableSlots: number | null;
      zoneId: number | null;
    }[] = [];
    for (const tier of showDetail.ticketTiers) {
      if (tier.accessType !== "Physical") continue;
      for (const price of tier.prices) {
        if (price.purchaseChannel === "Online") continue;
        rows.push({
          priceId: price.id,
          tierName: tier.name,
          priceName: price.name,
          unitPrice: price.price,
          availableSlots: price.availableSlots,
          zoneId: tier.zoneId,
        });
      }
    }
    return rows;
  }, [showDetail]);

  // Zone chip row is a filter over sellableRows, not a separate data source -- clicking a zone
  // narrows the tier/price picker below to just that zone so Staff can point out the right options
  // to a walk-in customer choosing where to sit, then click again (or "Tất cả") to clear it.
  const visibleRows = useMemo(
    () => (selectedZoneId === null ? sellableRows : sellableRows.filter((r) => r.zoneId === selectedZoneId)),
    [sellableRows, selectedZoneId]
  );
  const zonesOnMap = seatingMap?.zones ?? [];
  const hasZoneLayout = zonesOnMap.some(
    (z) => z.layout2DX !== null && z.layout2DY !== null && z.layout2DWidth !== null && z.layout2DHeight !== null
  );

  const cart: CartLine[] = sellableRows
    .map((row) => ({
      priceId: row.priceId,
      tierName: row.tierName,
      priceName: row.priceName,
      unitPrice: row.unitPrice,
      quantity: quantities[row.priceId] ?? 0,
      maxQuantity: row.availableSlots ?? 99,
    }))
    .filter((line) => line.quantity > 0);

  const subtotal = cart.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0);
  const itemCount = cart.reduce((sum, line) => sum + line.quantity, 0);

  function setQuantity(priceId: number, next: number, max: number) {
    setQuantities((prev) => ({ ...prev, [priceId]: Math.max(0, Math.min(max, next)) }));
  }

  async function handleProcessSale() {
    if (cart.length === 0 || selling) return;
    setSelling(true);
    setSaleError(null);
    let totalTickets = 0;
    let totalAmount = 0;
    try {
      // Backend has no atomic multi-tier cart -- SellWalkInTicketCommand is one priceId+quantity
      // per call. Sell sequentially; if one line fails partway (e.g. capacity ran out on that
      // specific price between page-load and checkout), stop and report exactly what succeeded
      // rather than silently losing track of already-confirmed sales.
      for (const line of cart) {
        const result = await sellWalkInTicket(line.priceId, line.quantity, paymentMethod);
        totalTickets += result.ticketIds.length;
        totalAmount += result.amount;
      }
      setReceipt({ ticketCount: totalTickets, amount: totalAmount, method: paymentMethod });
      setQuantities({});
      if (selectedShowId) {
        getShowDetail(selectedShowId).then(setShowDetail).catch(() => {});
      }
    } catch (err) {
      setSaleError(
        `${err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ."}` +
          (totalTickets > 0 ? ` (Đã bán thành công ${totalTickets} vé trước khi lỗi xảy ra.)` : "")
      );
    } finally {
      setSelling(false);
    }
  }

  if (loungeId === null) {
    return (
      <div className="min-h-screen flex flex-col bg-surface font-body-md text-body-md">
        <StaffHeader active="Box Office" />
        <div className="flex-1 flex items-center justify-center text-center px-6">
          <div>
            <Storefront size={40} className="mx-auto mb-4 text-on-surface-variant/50" weight="light" />
            <p className="text-on-surface mb-1">Tài khoản chưa được gán venue nào.</p>
            <p className="text-sm text-on-surface-variant">Liên hệ chủ phòng trà để được gán vào 1 venue trước khi bán vé.</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-surface font-body-md text-body-md">
      <StaffHeader active="Box Office" />

      <main className="flex-1 max-w-container-max mx-auto w-full px-margin-mobile md:px-lg py-md grid grid-cols-1 lg:grid-cols-[1fr_380px] gap-gutter">
        {/* Left: show picker + sale panel */}
        <section className="flex flex-col gap-lg">
          <div>
            <div className="flex justify-between items-end mb-4 border-b border-outline-variant/20 pb-4">
              <h1 className="font-headline-md text-headline-sm text-primary">Show hôm nay</h1>
            </div>

            {errorMessage && <p className="text-sm text-error mb-4">{errorMessage}</p>}
            {shows === null && !errorMessage && <p className="text-sm text-on-surface-variant">Đang tải...</p>}

            {shows !== null && todayShows.length === 0 && (
              <p className="text-sm text-on-surface-variant mb-4">Không có show nào diễn ra hôm nay tại venue này.</p>
            )}

            <div className="flex gap-4 overflow-x-auto pb-2">
              {[...todayShows, ...otherUpcoming].map((show) => (
                <button
                  key={show.id}
                  onClick={() => selectShow(show.id)}
                  className={`flex-shrink-0 w-64 text-left bg-surface-container-lowest border rounded-lg p-4 transition-colors ${
                    selectedShowId === show.id ? "border-primary" : "border-outline-variant/30 hover:border-primary/50"
                  }`}
                >
                  <h3 className="font-headline-md text-headline-sm text-primary truncate mb-1">{show.name}</h3>
                  <p className="text-xs text-on-surface-variant">{formatShowTime(show.scheduledStart)}</p>
                  <p className="text-xs text-on-surface-variant">{show.format === "Offline" ? "Tại venue" : "Livestream"}</p>
                </button>
              ))}
            </div>
          </div>

          {selectedShowId && zonesOnMap.length > 0 && (
            <div className="bg-surface-container-lowest border border-outline-variant/20 rounded-lg p-md">
              <div className="flex items-center justify-between mb-4">
                <h2 className="font-headline-md text-headline-sm text-primary flex items-center gap-2">
                  <Armchair size={20} weight="light" />
                  Khu vực chỗ ngồi
                </h2>
                {selectedZoneId !== null && (
                  <button
                    onClick={() => setSelectedZoneId(null)}
                    className="text-xs text-secondary hover:text-primary underline"
                  >
                    Xem tất cả khu vực
                  </button>
                )}
              </div>

              {hasZoneLayout && (
                <div
                  className="relative w-full aspect-[16/9] mb-4 rounded border border-outline-variant/20 bg-surface-container overflow-hidden bg-cover bg-center"
                  style={seatingMap?.areaLayoutImageUrl ? { backgroundImage: `url(${seatingMap.areaLayoutImageUrl})` } : undefined}
                >
                  {zonesOnMap
                    .filter((z) => z.layout2DX !== null && z.layout2DY !== null && z.layout2DWidth !== null && z.layout2DHeight !== null)
                    .map((z) => (
                      <button
                        key={z.zoneId}
                        onClick={() => setSelectedZoneId(selectedZoneId === z.zoneId ? null : z.zoneId)}
                        title={z.name}
                        style={{
                          left: `${z.layout2DX}%`,
                          top: `${z.layout2DY}%`,
                          width: `${z.layout2DWidth}%`,
                          height: `${z.layout2DHeight}%`,
                          transform: z.layout2DRotationDeg ? `rotate(${z.layout2DRotationDeg}deg)` : undefined,
                          backgroundColor: z.color ?? "#8c4a1e",
                        }}
                        className={`absolute flex items-center justify-center text-center text-[10px] font-medium text-white px-1 rounded-sm transition-opacity ${
                          selectedZoneId === null || selectedZoneId === z.zoneId ? "opacity-80 hover:opacity-100" : "opacity-30"
                        }`}
                      >
                        {z.name}
                      </button>
                    ))}
                </div>
              )}

              <div className="flex gap-3 overflow-x-auto pb-1">
                {zonesOnMap.map((z) => {
                  const soldOut = z.availableCount === 0;
                  const isSelected = selectedZoneId === z.zoneId;
                  return (
                    <button
                      key={z.zoneId}
                      onClick={() => setSelectedZoneId(isSelected ? null : z.zoneId)}
                      disabled={soldOut}
                      className={`flex-shrink-0 w-44 text-left border rounded p-3 transition-colors ${
                        soldOut
                          ? "border-outline-variant/10 bg-surface-container opacity-50 cursor-not-allowed"
                          : isSelected
                          ? "border-primary bg-primary/5"
                          : "border-outline-variant/20 bg-surface hover:border-primary/50"
                      }`}
                    >
                      <div className="flex items-center gap-1.5 mb-1">
                        <span className="w-2.5 h-2.5 rounded-full shrink-0" style={{ backgroundColor: z.color ?? "#8c4a1e" }} />
                        <p className="text-sm font-medium text-on-surface truncate">{z.name}</p>
                      </div>
                      <p className="text-xs text-on-surface-variant">
                        {soldOut ? "Hết chỗ" : z.availableCount !== null ? `Còn ${z.availableCount}/${z.capacity} chỗ` : `Sức chứa ${z.capacity}`}
                      </p>
                      {z.minPrice !== null && (
                        <p className="text-xs text-on-surface-variant">
                          {z.minPrice === z.maxPrice ? formatVnd(z.minPrice) : `${formatVnd(z.minPrice)} – ${formatVnd(z.maxPrice ?? z.minPrice)}`}
                        </p>
                      )}
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {selectedShowId && (
            <div className="flex-1 flex flex-col bg-surface-container-lowest border border-outline-variant/20 rounded-lg p-md">
              <h2 className="font-headline-md text-headline-sm text-primary mb-4">Chọn hạng vé &amp; số lượng</h2>
              {!showDetail ? (
                <p className="text-sm text-on-surface-variant">Đang tải...</p>
              ) : sellableRows.length === 0 ? (
                <p className="text-sm text-on-surface-variant">
                  Show này không có hạng vé nào bán được tại quầy (chỉ bán online, hoặc là show Livestream).
                </p>
              ) : visibleRows.length === 0 ? (
                <p className="text-sm text-on-surface-variant">Khu vực này không có hạng vé nào bán được tại quầy.</p>
              ) : (
                <div className="space-y-3">
                  {visibleRows.map((row) => {
                    const qty = quantities[row.priceId] ?? 0;
                    const soldOut = row.availableSlots === 0;
                    return (
                      <div
                        key={row.priceId}
                        className={`flex items-center justify-between px-4 py-3.5 rounded border ${
                          soldOut ? "border-outline-variant/10 bg-surface-container opacity-50" : "border-outline-variant/20 bg-surface"
                        }`}
                      >
                        <div>
                          <p className="text-sm font-medium text-on-surface">
                            {row.tierName} — {row.priceName}
                          </p>
                          <p className="text-xs text-on-surface-variant">
                            {soldOut ? "Hết vé" : row.availableSlots !== null ? `Còn ${row.availableSlots} vé` : "Còn vé"} ·{" "}
                            {formatVnd(row.unitPrice)}
                          </p>
                        </div>
                        <div className="flex items-center gap-3">
                          <button
                            onClick={() => setQuantity(row.priceId, qty - 1, row.availableSlots ?? 99)}
                            disabled={qty === 0}
                            className="w-8 h-8 rounded-full border border-outline-variant/40 flex items-center justify-center hover:bg-surface-container disabled:opacity-30"
                          >
                            <Minus size={14} />
                          </button>
                          <span className="w-6 text-center text-sm">{qty}</span>
                          <button
                            onClick={() => setQuantity(row.priceId, qty + 1, row.availableSlots ?? 99)}
                            disabled={soldOut || qty >= (row.availableSlots ?? 99)}
                            className="w-8 h-8 rounded-full border border-outline-variant/40 flex items-center justify-center hover:bg-surface-container disabled:opacity-30"
                          >
                            <Plus size={14} />
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}
        </section>

        {/* Right: order summary (sticky) */}
        <aside className="flex flex-col lg:sticky lg:top-[88px] lg:h-[calc(100vh-120px)]">
          <div className="bg-surface-container-lowest border border-outline-variant/20 rounded-lg flex flex-col h-full overflow-hidden shadow-[0_8px_40px_rgba(45,41,38,0.03)]">
            <div className="p-md border-b border-outline-variant/20 bg-surface-bright">
              <h2 className="font-headline-md text-headline-sm text-primary mb-1 flex items-center gap-2">
                <Receipt size={20} weight="light" />
                Đơn bán vé
              </h2>
            </div>

            <div className="flex-1 overflow-y-auto p-md flex flex-col gap-3">
              {cart.length === 0 ? (
                <p className="text-sm text-on-surface-variant text-center py-8">
                  {selectedShowId ? "Chưa chọn vé nào." : "Chọn show và hạng vé để bắt đầu."}
                </p>
              ) : (
                cart.map((line, i) => (
                  <div key={line.priceId} className={`flex justify-between items-start ${i > 0 ? "border-t border-outline-variant/10 pt-3" : ""}`}>
                    <div>
                      <p className="text-sm text-on-surface">
                        {line.tierName} — {line.priceName}
                      </p>
                      <p className="text-xs text-on-surface-variant">Số lượng: {line.quantity}</p>
                    </div>
                    <span className="text-sm text-on-surface">{formatVnd(line.unitPrice * line.quantity)}</span>
                  </div>
                ))
              )}
            </div>

            <div className="p-md bg-surface-bright border-t border-outline-variant/20">
              {saleError && <p className="text-xs text-error mb-3">{saleError}</p>}
              {receipt && (
                <div className="mb-4 px-3 py-2.5 rounded bg-tertiary-container/20 border border-tertiary/30 text-sm text-on-surface flex items-center gap-2">
                  <TicketIcon size={18} className="text-tertiary" weight="fill" />
                  Đã bán {receipt.ticketCount} vé — {formatVnd(receipt.amount)} (
                  {receipt.method === "Cash" ? "tiền mặt" : "chuyển khoản"}).
                </div>
              )}

              <div className="grid grid-cols-2 gap-2 mb-4">
                <button
                  onClick={() => setPaymentMethod("Cash")}
                  className={`flex items-center justify-center gap-2 py-2.5 rounded border text-sm transition-colors ${
                    paymentMethod === "Cash"
                      ? "border-primary bg-primary/5 text-primary"
                      : "border-outline-variant/30 text-on-surface-variant hover:border-primary/50"
                  }`}
                >
                  <Money size={16} weight="light" />
                  Tiền mặt
                </button>
                <button
                  onClick={() => setPaymentMethod("BankTransfer")}
                  className={`flex items-center justify-center gap-2 py-2.5 rounded border text-sm transition-colors ${
                    paymentMethod === "BankTransfer"
                      ? "border-primary bg-primary/5 text-primary"
                      : "border-outline-variant/30 text-on-surface-variant hover:border-primary/50"
                  }`}
                >
                  <Bank size={16} weight="light" />
                  Chuyển khoản
                </button>
              </div>

              <div className="flex justify-between items-end mb-4">
                <span className="font-headline-md text-headline-sm text-primary">Tổng cộng</span>
                <span className="font-display-md text-display-lg-mobile text-primary tracking-tight">{formatVnd(subtotal)}</span>
              </div>
              <button
                onClick={handleProcessSale}
                disabled={cart.length === 0 || selling}
                className="w-full bg-primary text-on-primary font-button text-button py-4 rounded hover:bg-primary/90 transition-colors flex items-center justify-center gap-2 disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <Storefront size={18} weight="light" />
                {selling
                  ? "Đang xử lý..."
                  : `Bán ${itemCount > 0 ? `${itemCount} vé` : "vé"} (${paymentMethod === "Cash" ? "tiền mặt" : "chuyển khoản"})`}
              </button>
            </div>
          </div>
        </aside>
      </main>
    </div>
  );
}
