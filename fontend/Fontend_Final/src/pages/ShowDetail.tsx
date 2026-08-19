import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Heart, MapPin, Star } from "@phosphor-icons/react";
import { ApiError, getStoredToken } from "../api/client";
import {
  addToWishlist,
  getShowDetail,
  LoungeShowDetail,
  removeFromWishlist,
  TicketTierSummary,
} from "../api/shows";
import { formatVnd } from "../lib/currency";
import AudienceHeader from "../components/AudienceHeader";
import AudienceFooter from "../components/AudienceFooter";

// Second Audience/Guest screen -- same AuraLounge visual system as DiscoverShows.tsx (see that
// file's header comment for the theme decision). Not a 1:1 port: the two Stitch candidates for
// this feature aren't the same screen split into two states, they're two different scopes --
// `auralounge_event_details_tiers` is a desktop lineup/overview page with a "View Tickets" button
// but no actual tier list, and `auralounge_event_details_tickets` is a mobile screen with a real
// tier-selection list but no desktop layout. Combined: desktop hero/info/performer-grid structure
// from `_tiers`, the real tier-with-prices list concept from `_tickets`, both re-laid-out for
// desktop and driven entirely by LoungeShowDetailDto (nothing here is a Stitch placeholder value).

const FORMAT_LABELS: Record<string, string> = { Offline: "Tại venue", Online: "Livestream" };

function formatFullDateTime(iso: string): string {
  const d = new Date(iso);
  const weekday = d.toLocaleDateString("vi-VN", { weekday: "long" });
  const date = d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
  const time = d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  return `${weekday.charAt(0).toUpperCase()}${weekday.slice(1)}, ${date} · ${time}`;
}

function TierCard({ tier }: { tier: TicketTierSummary }) {
  return (
    <div className="border border-[#1b1c19]/10 rounded-[4px] p-4">
      <p className="text-base mb-0.5" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
        {tier.name}
      </p>
      {tier.description && <p className="text-xs text-[#7e756f] mb-3">{tier.description}</p>}
      <div className="space-y-2">
        {tier.prices.map((price) => {
          const soldOut = price.availableSlots === 0;
          return (
            <div
              key={price.id}
              className={`flex items-center justify-between px-3 py-2.5 rounded-[4px] ${
                soldOut ? "bg-[#f0eee9] opacity-60" : "bg-[#f5f3ee]"
              }`}
            >
              <div>
                <p className="text-sm text-[#1b1c19]">{price.name}</p>
                {price.availableSlots !== null && (
                  <p className="text-xs text-[#7e756f]">
                    {soldOut ? "Hết vé" : `Còn ${price.availableSlots} vé`}
                  </p>
                )}
              </div>
              <p className={`text-sm font-medium ${soldOut ? "text-[#7e756f] line-through" : "text-[#181512]"}`}>
                {formatVnd(price.price)}
              </p>
            </div>
          );
        })}
      </div>
    </div>
  );
}

export default function ShowDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [show, setShow] = useState<LoungeShowDetail | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [wishlistBusy, setWishlistBusy] = useState(false);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setShow(null);
    setErrorMessage(null);
    getShowDetail(Number(id))
      .then((result) => {
        if (!cancelled) setShow(result);
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(
          err instanceof ApiError && err.status === 404
            ? "Không tìm thấy show này."
            : "Không thể kết nối tới máy chủ."
        );
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  async function handleToggleWishlist() {
    if (!show) return;
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }
    setWishlistBusy(true);
    try {
      if (show.isWishlisted) {
        await removeFromWishlist(show.id);
        setShow({ ...show, isWishlisted: false });
      } else {
        await addToWishlist(show.id);
        setShow({ ...show, isWishlisted: true });
      }
    } catch {
      // Non-critical action -- leave state unchanged, no blocking error UI for a heart toggle.
    } finally {
      setWishlistBusy(false);
    }
  }

  function handleBuyClick() {
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }
    // Ticket-hold/checkout flow isn't built yet (docs/journeys/21-anonymous-journey.md Journey 4: POST
    // /tickets/holds requires auth, that's as far as this page's job goes).
    navigate(`/shows/${id}/tickets`);
  }

  if (errorMessage) {
    return (
      <div
        className="bg-[#fbf9f4] min-h-screen flex items-center justify-center text-[#1b1c19] px-6"
        style={{ fontFamily: "'Manrope', sans-serif" }}
      >
        <div className="text-center">
          <p className="text-lg mb-2" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
            {errorMessage}
          </p>
          <Link to="/shows" className="text-sm text-[#4d4540] hover:text-[#181512] underline">
            Quay lại danh sách show
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-[#fbf9f4] min-h-screen text-[#1b1c19] flex flex-col" style={{ fontFamily: "'Manrope', sans-serif" }}>
      <AudienceHeader active="Sự kiện" />

      <div className="flex-1">
      {!show ? (
        <div className="px-6 md:px-16 py-16 text-center text-[#4d4540]">Đang tải...</div>
      ) : (
        <>
          {/* Hero */}
          <div className="relative h-[420px] bg-[#181512]">
            {show.coverImageUrl && (
              <img src={show.coverImageUrl} alt={show.name} className="absolute inset-0 w-full h-full object-cover opacity-70" />
            )}
            <div className="absolute inset-0 bg-gradient-to-t from-[#181512] via-[#181512]/40 to-transparent" />
            <div className="absolute bottom-0 left-0 right-0 px-6 md:px-16 py-10 text-white">
              <div className="flex flex-wrap gap-2 mb-3">
                <span className="text-[10px] tracking-[0.1em] px-2.5 py-1 rounded-[2px] bg-white/15">
                  {FORMAT_LABELS[show.format] ?? show.format}
                </span>
                {show.genres.slice(0, 3).map((g) => (
                  <span key={g.id} className="text-[10px] tracking-[0.1em] px-2.5 py-1 rounded-[2px] bg-white/15">
                    {g.name.toUpperCase()}
                  </span>
                ))}
              </div>
              <div className="flex items-start justify-between gap-4">
                <h1 className="text-4xl md:text-5xl max-w-2xl" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                  {show.name}
                </h1>
                <button
                  onClick={handleToggleWishlist}
                  disabled={wishlistBusy}
                  aria-label={show.isWishlisted ? "Bỏ theo dõi" : "Theo dõi show"}
                  className="flex-shrink-0 w-11 h-11 rounded-full bg-white/15 hover:bg-white/25 flex items-center justify-center transition-colors disabled:opacity-50"
                >
                  <Heart size={20} weight={show.isWishlisted ? "fill" : "regular"} className={show.isWishlisted ? "text-[#e9c176]" : "text-white"} />
                </button>
              </div>
            </div>
          </div>

          <div className="px-6 md:px-16 py-10 flex flex-col lg:flex-row gap-10">
            {/* Left: info + description + performers */}
            <div className="flex-1 min-w-0">
              <div className="flex flex-wrap gap-x-8 gap-y-3 mb-8 text-sm">
                <p>{formatFullDateTime(show.scheduledStart)}</p>
                <p className="flex items-center gap-1.5">
                  <MapPin size={16} className="text-[#7e756f]" />
                  {show.lounge.name} — {show.lounge.district}, {show.lounge.city}
                </p>
                {show.ratings.totalCount > 0 && (
                  <p className="flex items-center gap-1.5">
                    <Star size={16} weight="fill" className="text-[#e9c176]" />
                    {show.ratings.averageScore.toFixed(1)} ({show.ratings.totalCount} đánh giá)
                  </p>
                )}
              </div>

              {show.description && (
                <p className="text-[#4d4540] leading-relaxed mb-10 whitespace-pre-line">{show.description}</p>
              )}

              {show.performers.length > 0 && (
                <div>
                  <h2 className="text-2xl mb-5" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                    Nghệ sĩ biểu diễn
                  </h2>
                  <div className="grid grid-cols-2 sm:grid-cols-3 gap-5">
                    {show.performers.map((p) => (
                      <div key={p.performanceId}>
                        <div className="aspect-square rounded-[8px] bg-[#181512] overflow-hidden mb-2">
                          {p.avatarUrl && <img src={p.avatarUrl} alt={p.name} className="w-full h-full object-cover" />}
                        </div>
                        <p className="text-sm font-medium truncate">{p.name}</p>
                        {p.genres.length > 0 && (
                          <p className="text-xs text-[#7e756f] truncate">{p.genres.map((g) => g.name).join(", ")}</p>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>

            {/* Right: ticket tiers */}
            <div className="w-full lg:w-96 flex-shrink-0">
              <div className="lg:sticky lg:top-6 border border-[#1b1c19]/10 rounded-[8px] p-5 bg-white">
                <h2 className="text-xl mb-4" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                  Chọn vé
                </h2>
                {show.ticketTiers.length === 0 ? (
                  <p className="text-sm text-[#7e756f]">Chưa mở bán vé cho show này.</p>
                ) : (
                  <div className="space-y-4">
                    {show.ticketTiers.map((tier) => (
                      <TierCard key={tier.id} tier={tier} />
                    ))}
                  </div>
                )}
                {show.userHasTicket && (
                  <p className="text-xs text-[#4d4540] mt-3">Bạn đã có vé cho show này.</p>
                )}
                <button
                  onClick={handleBuyClick}
                  disabled={show.ticketTiers.length === 0 || show.status !== "Published" && show.status !== "Ongoing"}
                  className="w-full mt-5 bg-[#181512] text-white text-sm font-medium py-3.5 rounded-[4px] hover:bg-[#181512]/90 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  Mua vé
                </button>
              </div>
            </div>
          </div>
        </>
      )}
      </div>

      <AudienceFooter />
    </div>
  );
}
