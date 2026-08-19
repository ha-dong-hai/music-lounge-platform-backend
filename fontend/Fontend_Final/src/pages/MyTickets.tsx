import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { CheckCircle, HandHeart, HourglassMedium, QrCode, Waveform } from "@phosphor-icons/react";
import { ApiError, getStoredToken, getStoredUser } from "../api/client";
import { getMyTickets, initiateTicketTransfer, TicketListItem, TicketStatus } from "../api/tickets";
import { getMyDonations, MyDonation } from "../api/donations";
import { getFollowedLounges, FollowedLounge } from "../api/follows";
import { getRecommendedShows } from "../api/shows";
import { formatVnd } from "../lib/currency";
import AudienceHeader from "../components/AudienceHeader";
import AudienceFooter from "../components/AudienceFooter";

// Third pass, 2026-08-18 -- rebuilt directly from the raw markup in
// stitch/stitch_ai_audience/auralounge_my_resonances_tickets/code.html (431 lines, read in full)
// rather than a restyled reinterpretation of it: horizontal image-left/content-right ticket cards
// (glass-panel, image block carries the date+venue text directly on it), the "Current Vibe" pill
// under the greeting, a 12-col grid (8-col tickets+following / 4-col sticky Patronage Ledger), the
// ledger's avatar-row-with-status-icon layout, and the sharper border-radius scale the mock's own
// tailwind-config uses (DEFAULT=2px, lg=4px, xl=8px, full=12px -- not Tailwind's generic rounded-
// lg/rounded-full) are all ported literally below via rounded-[2px]/[4px]/[12px] arbitrary values.
//
// Two mock elements have no real data behind them and are adapted rather than faked:
//   - "Current Vibe: Deep Groove" is decorative in the mock. Wired here to the real top result of
//     GET /recommendations (MLNetRecommendationService) instead -- shows its real Reason string
//     (e.g. a genuine personalized reason, or "Đang thịnh hành" for a fresh account), hidden
//     entirely if that call fails or returns nothing.
//   - "Following" in the mock includes both Artists and Venues. Only Lounges are kept -- Performer
//     has no login/account to follow as its own entity in this system (same call made everywhere
//     else this session, see docs memory musiclounge_no_artist_actor).
// Ticket cards also have no cover-image field on TicketListItemDto -- the mock's photo-left block
// is kept as a solid #181512 panel (not a fake photo) with the same date-badge-on-image text
// treatment, consistent with how ShowDetail/DiscoverShows already handle a missing image.

const STATUS_LABEL: Record<TicketStatus, string> = {
  Pending: "Chờ thanh toán",
  Confirmed: "Đã xác nhận",
  Used: "Đã sử dụng",
  Cancelled: "Đã huỷ",
  Refunded: "Đã hoàn tiền",
};

// Domain enum values (DonationStatus), rendered as-is with a VN label -- read-only history, not
// worth a full typed union for a display-only page. Bucketed into the mock's 2-state "pending vs
// settled" visual language (pending icon/color vs check-circle icon/color).
const DONATION_STATUS_LABEL: Record<string, string> = {
  PendingOwnerAck: "Chờ chủ quán xác nhận",
  OwnerReceived: "Chủ quán đã nhận",
  PerformerPaid: "Đã trả nghệ sĩ",
  Refunded: "Đã hoàn tiền",
};

function isDonationSettled(status: string): boolean {
  return status === "PerformerPaid" || status === "Refunded";
}

function formatShowDateBadge(iso: string): string {
  const d = new Date(iso);
  const date = d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" });
  const time = d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  return `${date} • ${time}`;
}

function TicketCard({ ticket }: { ticket: TicketListItem }) {
  const [transferOpen, setTransferOpen] = useState(false);
  const [email, setEmail] = useState("");
  const [sending, setSending] = useState(false);
  const [result, setResult] = useState<string | null>(null);

  async function handleTransfer() {
    if (!email.trim()) return;
    setSending(true);
    setResult(null);
    try {
      await initiateTicketTransfer(ticket.id, email.trim());
      setResult("Đã gửi yêu cầu chuyển nhượng. Chờ người nhận xác nhận.");
      setEmail("");
    } catch (err) {
      setResult(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setSending(false);
    }
  }

  const canTransfer = ticket.status === "Confirmed" && !ticket.hasPendingTransfer;

  return (
    <div className="bg-white/60 backdrop-blur-md border border-[#7e756f]/10 flex flex-col md:flex-row rounded-[4px] overflow-hidden relative group">
      <div className="md:w-1/3 h-40 md:h-auto bg-[#e4e2dd] relative flex-shrink-0 bg-[#181512]">
        <div className="absolute bottom-4 left-4 text-[#fbf9f4]">
          <div className="text-[12px] tracking-[0.1em] font-semibold uppercase mb-1">
            {formatShowDateBadge(ticket.showScheduledStart)}
          </div>
          <div className="text-[20px] leading-[28px]" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
            {ticket.loungeName}
          </div>
        </div>
      </div>
      <div className="p-6 flex-grow flex flex-col justify-between bg-[#f5f3ee]">
        <div>
          <div className="flex items-start justify-between gap-2 mb-2">
            <Link to={`/me/tickets/${ticket.id}`} className="hover:opacity-80 transition-opacity">
              <h3 className="text-[24px] leading-[32px] text-[#181512]" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                {ticket.showName}
              </h3>
            </Link>
            <span className="flex-shrink-0 text-[12px] px-2 py-1 rounded-[2px] bg-white border border-[#7e756f]/20 text-[#4d4540] font-medium">
              {STATUS_LABEL[ticket.status]}
            </span>
          </div>
          <p className="text-[16px] text-[#4d4540] mb-4">
            {ticket.tierName} • {ticket.priceName} • {formatVnd(ticket.pricePaid)}
          </p>
          <div className="flex gap-2">
            <span className="inline-block px-2 py-1 bg-[#fbf9f4] border border-[#7e756f]/20 text-[#4d4540] text-[12px] tracking-[0.1em] uppercase rounded-[2px]">
              {ticket.accessType === "Physical" ? "Tại venue" : "Livestream"}
            </span>
            <span className="inline-block px-2 py-1 bg-[#fbf9f4] border border-[#7e756f]/20 text-[#4d4540] text-[12px] tracking-[0.1em] uppercase rounded-[2px]">
              {ticket.loungeCity}
            </span>
          </div>
        </div>
        <div className="mt-6 flex flex-wrap gap-3 items-center">
          <Link
            to={`/me/tickets/${ticket.id}`}
            className="bg-[#181512] text-[#fbf9f4] text-[14px] font-medium tracking-[0.02em] px-6 py-3 rounded-[2px] hover:bg-[#181512]/85 transition-colors flex items-center gap-2"
          >
            <QrCode size={18} weight="bold" />
            Xem vé
          </Link>
          {ticket.hasPendingTransfer ? (
            <span className="text-[14px] text-[#7e756f]">Đang chờ người nhận xác nhận chuyển nhượng.</span>
          ) : (
            canTransfer &&
            (!transferOpen ? (
              <button
                onClick={() => setTransferOpen(true)}
                className="border border-[#7e756f]/20 text-[#181512] text-[14px] font-medium tracking-[0.02em] px-6 py-3 rounded-[2px] hover:bg-[#e4e2dd] transition-colors"
              >
                Chuyển nhượng
              </button>
            ) : (
              <div className="flex items-center gap-2 flex-1 min-w-[220px]">
                <input
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="Email người nhận"
                  className="flex-1 px-3 py-2.5 text-[14px] bg-white border border-[#7e756f]/20 rounded-[2px] outline-none focus:border-[#181512]"
                />
                <button
                  onClick={handleTransfer}
                  disabled={sending || !email.trim()}
                  className="px-4 py-2.5 text-[14px] rounded-[2px] bg-[#181512] text-[#fbf9f4] disabled:opacity-40"
                >
                  {sending ? "..." : "Gửi"}
                </button>
              </div>
            ))
          )}
        </div>
        {result && <p className="text-[12px] text-[#7e756f] mt-2">{result}</p>}
      </div>
    </div>
  );
}

export default function MyTickets() {
  const navigate = useNavigate();
  const user = getStoredUser();
  const [tickets, setTickets] = useState<TicketListItem[] | null>(null);
  const [tab, setTab] = useState<"upcoming" | "past">("upcoming");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [donations, setDonations] = useState<MyDonation[] | null>(null);
  const [donationsTotal, setDonationsTotal] = useState(0);
  const [donationsShown, setDonationsShown] = useState(5);
  const [followed, setFollowed] = useState<FollowedLounge[] | null>(null);
  const [vibe, setVibe] = useState<string | null>(null);

  useEffect(() => {
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }
    let cancelled = false;
    getMyTickets(1, 50)
      .then((result) => {
        if (!cancelled) setTickets(result.items);
      })
      .catch(() => {
        if (!cancelled) setErrorMessage("Không thể kết nối tới máy chủ.");
      });
    getFollowedLounges(1, 6)
      .then((result) => {
        if (!cancelled) setFollowed(result.items);
      })
      .catch(() => {
        if (!cancelled) setFollowed([]);
      });
    getRecommendedShows(1)
      .then((result) => {
        if (!cancelled && result.length > 0) setVibe(result[0].recommendationReason);
      })
      .catch(() => {
        // Purely decorative pill -- no error state needed, just doesn't render.
      });
    return () => {
      cancelled = true;
    };
  }, [navigate]);

  useEffect(() => {
    if (!getStoredToken()) return;
    let cancelled = false;
    getMyDonations(1, donationsShown)
      .then((result) => {
        if (cancelled) return;
        setDonations(result.items);
        setDonationsTotal(result.totalCount);
      })
      .catch(() => {
        if (!cancelled) setDonations([]);
      });
    return () => {
      cancelled = true;
    };
  }, [donationsShown]);

  const now = Date.now();
  const upcoming = (tickets ?? [])
    .filter((t) => new Date(t.showScheduledStart).getTime() >= now)
    .sort((a, b) => new Date(a.showScheduledStart).getTime() - new Date(b.showScheduledStart).getTime());
  const past = (tickets ?? [])
    .filter((t) => new Date(t.showScheduledStart).getTime() < now)
    .sort((a, b) => new Date(b.showScheduledStart).getTime() - new Date(a.showScheduledStart).getTime());
  const visible = tab === "upcoming" ? upcoming : past;

  return (
    <div className="bg-[#fbf9f4] min-h-screen text-[#1b1c19] flex flex-col" style={{ fontFamily: "'Manrope', sans-serif" }}>
      <AudienceHeader active="Vé của tôi" />

      <main className="flex-1 max-w-[1440px] mx-auto w-full px-6 md:px-16 py-16">
        {/* Dashboard Header */}
        <section className="mb-16">
          <h1 className="text-[32px] md:text-[64px] leading-[1.1] md:leading-[72px] tracking-[-0.02em] text-[#181512] mb-4" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
            Chào mừng trở lại, {user?.fullName ?? "bạn"}
          </h1>
          {vibe && (
            <div className="inline-flex items-center gap-2 bg-[#f5f3ee] px-4 py-2 rounded-[12px] border border-[#7e756f]/10">
              <Waveform size={18} className="text-[#775a19]" />
              <span className="text-[16px] text-[#4d4540]">
                Gợi ý dành cho bạn: <strong className="text-[#181512] font-bold">{vibe}</strong>
              </span>
            </div>
          )}
        </section>

        {errorMessage && <p className="text-sm text-[#ba1a1a] mb-8">{errorMessage}</p>}

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
          {/* Left Column: Tickets & Following */}
          <div className="lg:col-span-8 flex flex-col gap-16">
            {/* My Tickets */}
            <section>
              <div className="flex justify-between items-end border-b border-[#7e756f]/10 mb-8 pb-2">
                <h2 className="text-[24px] md:text-[32px] leading-[40px] text-[#181512]" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                  Vé của tôi
                </h2>
                <div className="flex gap-4 text-[12px] tracking-[0.1em] uppercase font-semibold">
                  <button
                    onClick={() => setTab("upcoming")}
                    className={`pb-2 ${tab === "upcoming" ? "text-[#181512] border-b-2 border-[#181512]" : "text-[#4d4540] hover:text-[#181512] transition-colors"}`}
                  >
                    Sắp diễn ra
                  </button>
                  <button
                    onClick={() => setTab("past")}
                    className={`pb-2 ${tab === "past" ? "text-[#181512] border-b-2 border-[#181512]" : "text-[#4d4540] hover:text-[#181512] transition-colors"}`}
                  >
                    Đã qua
                  </button>
                </div>
              </div>

              {tickets === null && !errorMessage && <p className="text-[#4d4540] py-10 text-center">Đang tải...</p>}

              {tickets !== null && tickets.length === 0 && (
                <div className="text-center py-16 border border-[#7e756f]/10 bg-white rounded-[4px]">
                  <p className="text-lg mb-2" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                    Bạn chưa có vé nào
                  </p>
                  <Link to="/shows" className="text-sm text-[#181512] underline">
                    Khám phá show đang mở bán
                  </Link>
                </div>
              )}

              {tickets !== null && tickets.length > 0 && visible.length === 0 && (
                <p className="text-sm text-[#7e756f] py-10 text-center">
                  {tab === "upcoming" ? "Không có show sắp diễn ra." : "Chưa có show nào đã diễn ra."}
                </p>
              )}

              <div className="grid gap-8">
                {visible.map((t) => (
                  <TicketCard key={t.id} ticket={t} />
                ))}
              </div>
            </section>

            {/* Following Grid */}
            <section>
              <h2 className="text-[24px] md:text-[32px] leading-[40px] text-[#181512] border-b border-[#7e756f]/10 mb-8 pb-2" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                Đang theo dõi
              </h2>
              {followed === null ? (
                <p className="text-sm text-[#7e756f]">Đang tải...</p>
              ) : followed.length === 0 ? (
                <p className="text-sm text-[#7e756f]">
                  Bạn chưa theo dõi venue nào.{" "}
                  <Link to="/shows" className="underline text-[#181512]">
                    Khám phá show
                  </Link>
                </p>
              ) : (
                <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
                  {followed.map((l) => (
                    <Link key={l.id} to={`/shows?loungeId=${l.id}`} className="group cursor-pointer">
                      <div className="aspect-square bg-[#e4e2dd] mb-2 overflow-hidden rounded-[2px] relative">
                        {l.primaryImageUrl ? (
                          <img
                            src={l.primaryImageUrl}
                            alt={l.name}
                            className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                          />
                        ) : (
                          <div className="w-full h-full bg-[#181512]" />
                        )}
                        <div className="absolute inset-0 bg-[#181512]/0 group-hover:bg-[#181512]/20 transition-colors duration-300" />
                      </div>
                      <h4 className="text-[16px] text-[#181512] truncate">{l.name}</h4>
                      <p className="text-[12px] tracking-[0.1em] text-[#4d4540] uppercase mt-1">Địa điểm</p>
                    </Link>
                  ))}
                </div>
              )}
            </section>
          </div>

          {/* Right Column: Patronage Ledger */}
          <div className="lg:col-span-4">
            <section className="bg-[#f5f3ee] border border-[#7e756f]/10 p-6 rounded-[4px] lg:sticky lg:top-32">
              <h2 className="text-[24px] leading-[32px] text-[#181512] mb-6 flex items-center gap-2" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                <HandHeart size={22} className="text-[#775a19]" />
                Sổ Ủng Hộ
              </h2>
              {donations === null ? (
                <p className="text-sm text-[#7e756f]">Đang tải...</p>
              ) : donations.length === 0 ? (
                <p className="text-sm text-[#7e756f]">Bạn chưa ủng hộ nghệ sĩ nào.</p>
              ) : (
                <div className="flex flex-col gap-0">
                  <div className="grid grid-cols-4 text-[12px] tracking-[0.1em] font-semibold text-[#4d4540] border-b border-[#7e756f]/10 pb-2 mb-4 uppercase">
                    <div className="col-span-2">Người nhận</div>
                    <div className="text-right">Số tiền</div>
                    <div className="text-right">Trạng thái</div>
                  </div>
                  {donations.map((d, i) => (
                    <div
                      key={d.id}
                      className={`grid grid-cols-4 py-4 items-center text-[16px] text-[#181512] group hover:bg-[#eae8e3] transition-colors -mx-4 px-4 ${
                        i < donations.length - 1 ? "border-b border-[#7e756f]/10" : ""
                      }`}
                    >
                      <div className="col-span-2 flex items-center gap-3 min-w-0">
                        <div className="w-8 h-8 rounded-[12px] bg-white flex items-center justify-center text-xs font-medium text-[#4d4540] flex-shrink-0">
                          {d.performerName.charAt(0).toUpperCase()}
                        </div>
                        <span className="truncate">{d.performerName}</span>
                      </div>
                      <div className="text-right text-[16px]">{formatVnd(d.gross)}</div>
                      <div className="text-right">
                        {isDonationSettled(d.status) ? (
                          <span className="inline-flex items-center gap-1 text-[#4d4540] text-[12px] tracking-[0.1em] uppercase">
                            <CheckCircle size={16} />
                            Xong
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 text-[#775a19] text-[12px] tracking-[0.1em] uppercase">
                            <HourglassMedium size={16} />
                            Chờ
                          </span>
                        )}
                      </div>
                      <div className="col-span-4 text-[12px] text-[#7e756f] mt-1 truncate">{DONATION_STATUS_LABEL[d.status] ?? d.status}</div>
                    </div>
                  ))}
                </div>
              )}
              {donations !== null && donationsShown < donationsTotal && (
                <button
                  onClick={() => setDonationsShown((n) => n + 10)}
                  className="w-full mt-6 py-3 border border-[#7e756f]/20 text-[#181512] text-[14px] font-medium tracking-[0.02em] rounded-[2px] hover:bg-[#e4e2dd] transition-colors text-center"
                >
                  Xem thêm lịch sử
                </button>
              )}
            </section>
          </div>
        </div>
      </main>

      <AudienceFooter />
    </div>
  );
}
