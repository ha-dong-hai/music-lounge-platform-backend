import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ArrowRight, CalendarBlank, MagnifyingGlass, MapPin, Star } from "@phosphor-icons/react";
import { ApiError } from "../api/client";
import { LoungeShowFormat, LoungeShowListItem, LoungeShowSortBy, searchShows } from "../api/shows";
import { getLounges, LoungeListItem } from "../api/lounges";
import { formatVnd } from "../lib/currency";
import AudienceHeader from "../components/AudienceHeader";
import AudienceFooter from "../components/AudienceFooter";

// Fifth pass, 2026-08-18 -- user pointed out that auralounge_event_directory and
// auralounge_desktop_discovery_hub are two genuinely separate screens in the mock ("là 2 view rõ
// ràng cho tôi không gộp") and should not have been merged into one page. This file is now a pure
// literal port of auralounge_event_directory ONLY (filter bar, featured hero, "Upcoming Schedule"
// grid, pagination) -- the AI-recommendations block that used to live here (styled after
// discovery_hub's "Curated Soundscape") has moved to its own page, DiscoverHub.tsx, which is what
// that section actually belongs to. Nav item for this page is "Sự kiện" (the Event Directory);
// "Khám phá" now points at the separate Discovery Hub landing page.
//
// Border-radius scale is the mock's own sharp one -- DEFAULT=2px, lg=4px, xl=8px, full=12px, not
// Tailwind's generic rounded-lg/rounded-full -- expressed as rounded-[2px]/[8px]/[12px].
//
// Public route -- AllowAnonymous + SwaggerOptionalAuth on every endpoint this page calls, works
// fully for a guest with zero token. Ticket/wishlist actions from here still hit RequireAuthenticated
// endpoints elsewhere (see docs/journeys/21-anonymous-journey.md Journey 4) -- not this screen's concern.

const SORT_OPTIONS: { value: LoungeShowSortBy; label: string }[] = [
  { value: "Newest", label: "Mới nhất" },
  { value: "StartingSoon", label: "Sắp diễn ra" },
  { value: "Popular", label: "Nổi bật" },
  { value: "PriceAsc", label: "Giá thấp đến cao" },
  { value: "PriceDesc", label: "Giá cao đến thấp" },
];

const FORMAT_OPTIONS: { value: LoungeShowFormat | ""; label: string }[] = [
  { value: "", label: "Mọi hình thức" },
  { value: "Offline", label: "Tại venue" },
  { value: "Online", label: "Livestream" },
];

function formatShowDateTime(iso: string): string {
  const d = new Date(iso);
  const datePart = d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" });
  const timePart = d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  return `${timePart} · ${datePart}`;
}

function formatShowDateLong(iso: string): string {
  const d = new Date(iso);
  const weekday = d.toLocaleDateString("vi-VN", { weekday: "long" });
  const date = d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
  const time = d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  return `${weekday.charAt(0).toUpperCase()}${weekday.slice(1)}, ${date} · ${time}`;
}

function priceRangeLabel(item: { minPrice: number | null; maxPrice: number | null }): string {
  if (item.minPrice === null || item.maxPrice === null) return "Chưa có giá";
  if (item.minPrice === item.maxPrice) return formatVnd(item.minPrice);
  return `${formatVnd(item.minPrice)} – ${formatVnd(item.maxPrice)}`;
}

// Numbered pagination matching auralounge_event_directory's literal "‹ 1 2 3 … 8 ›" row.
function paginationItems(current: number, total: number): (number | "...")[] {
  const items: (number | "...")[] = [];
  const window = 1;
  for (let p = 1; p <= total; p++) {
    if (p === 1 || p === total || (p >= current - window && p <= current + window)) {
      items.push(p);
    } else if (items[items.length - 1] !== "...") {
      items.push("...");
    }
  }
  return items;
}

export default function DiscoverShows() {
  // loungeId can arrive via ?loungeId= (e.g. clicking a followed venue from MyTickets) -- read
  // once on mount as the initial filter value, same pattern as any other filter's initial state.
  const [searchParams] = useSearchParams();
  const initialLoungeId = (() => {
    const raw = searchParams.get("loungeId");
    const n = raw ? Number(raw) : NaN;
    return Number.isFinite(n) ? n : null;
  })();

  const [shows, setShows] = useState<LoungeShowListItem[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [searchInput, setSearchInput] = useState("");
  const [keyword, setKeyword] = useState("");
  const [loungeId, setLoungeId] = useState<number | null>(initialLoungeId);
  const [lounges, setLounges] = useState<LoungeListItem[] | null>(null);
  const [format, setFormat] = useState<LoungeShowFormat | "">("");
  const [sortBy, setSortBy] = useState<LoungeShowSortBy>("Newest");
  const [page, setPage] = useState(1);

  const pageSize = 12;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  useEffect(() => {
    getLounges({ pageSize: 100 })
      .then((result) => setLounges(result.items))
      .catch(() => setLounges([]));
  }, []);

  // Debounce free-text into the actual query param, same 300ms settle as AdminUsers.tsx.
  useEffect(() => {
    const timer = setTimeout(() => {
      setKeyword(searchInput.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    let cancelled = false;
    setShows(null);
    setErrorMessage(null);
    searchShows({
      keyword: keyword || undefined,
      loungeId: loungeId ?? undefined,
      format: format || undefined,
      page,
      pageSize,
      sortBy,
    })
      .then((result) => {
        if (cancelled) return;
        setShows(result.items);
        setTotalCount(result.totalCount);
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
    return () => {
      cancelled = true;
    };
  }, [keyword, loungeId, format, sortBy, page]);

  // Featured/hero card -- first result of the current (unfiltered) list, matching the mock's
  // "Featured Headliner" section. Only on page 1 with no active filters, so it never duplicates
  // the grid's own first card once someone has actually narrowed the list down.
  const isUnfiltered = !keyword && !loungeId && !format && page === 1;
  const featured = isUnfiltered ? shows?.[0] : undefined;
  const gridShows = featured ? shows?.slice(1) : shows;

  return (
    <div className="bg-[#fbf9f4] min-h-screen text-[#1b1c19] flex flex-col" style={{ fontFamily: "'Manrope', sans-serif" }}>
      <AudienceHeader active="Sự kiện" />

      <main className="flex-1 max-w-[1440px] mx-auto w-full">
        {/* Page header & filters */}
        <header className="px-6 md:px-16 pt-16 pb-8 w-full">
          <h1 className="text-[40px] md:text-[64px] leading-[1.1] md:leading-[72px] tracking-[-0.02em] text-[#181512] mb-8" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
            Danh Mục Sự Kiện
          </h1>
          <div className="bg-white p-6 border border-[#cfc4bd]/50 flex flex-wrap gap-4 items-end">
            <div className="flex-1 min-w-[250px]">
              <label className="block text-[12px] tracking-[0.1em] font-semibold text-[#4d4540] mb-2 uppercase">Tìm kiếm</label>
              <div className="relative">
                <MagnifyingGlass size={18} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#7e756f]" />
                <input
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  placeholder="Tên show, nghệ sĩ, venue..."
                  className="w-full pl-10 pr-4 py-3 bg-[#fbf9f4] border-b border-[#7e756f] focus:border-[#181512] focus:outline-none text-[16px] text-[#1b1c19] placeholder:text-[#96908b] transition-colors"
                />
              </div>
            </div>
            <div className="w-full md:w-auto flex gap-4 flex-wrap">
              {lounges && lounges.length > 0 && (
                <div className="min-w-[170px]">
                  <label className="block text-[12px] tracking-[0.1em] font-semibold text-[#4d4540] mb-2 uppercase">Venue</label>
                  <select
                    value={loungeId ?? ""}
                    onChange={(e) => {
                      setLoungeId(e.target.value ? Number(e.target.value) : null);
                      setPage(1);
                    }}
                    className="w-full px-4 py-3 bg-[#fbf9f4] border-b border-[#7e756f] focus:border-[#181512] focus:outline-none text-[16px] text-[#1b1c19] appearance-none"
                  >
                    <option value="">Mọi venue</option>
                    {lounges.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.name}
                      </option>
                    ))}
                  </select>
                </div>
              )}
              <div className="min-w-[150px]">
                <label className="block text-[12px] tracking-[0.1em] font-semibold text-[#4d4540] mb-2 uppercase">Hình thức</label>
                <select
                  value={format}
                  onChange={(e) => {
                    setFormat(e.target.value as LoungeShowFormat | "");
                    setPage(1);
                  }}
                  className="w-full px-4 py-3 bg-[#fbf9f4] border-b border-[#7e756f] focus:border-[#181512] focus:outline-none text-[16px] text-[#1b1c19] appearance-none"
                >
                  {FORMAT_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </div>
        </header>

        <div className="flex flex-col gap-16 px-6 md:px-16 pb-20">
          {/* Featured Event */}
          {featured && (
            <section className="relative h-[420px] md:h-[600px] w-full bg-[#f0eee9] overflow-hidden group border border-[#cfc4bd]/50">
              <Link to={`/shows/${featured.id}`} className="absolute inset-0">
                {featured.coverImageUrl && (
                  <div
                    className="absolute inset-0 bg-cover bg-center w-full h-full transition-transform duration-700 group-hover:scale-105"
                    style={{ backgroundImage: `url('${featured.coverImageUrl}')` }}
                  />
                )}
                <div className="absolute inset-0 bg-gradient-to-t from-[#181512]/90 via-[#181512]/40 to-transparent" />
                <div className="absolute bottom-0 left-0 p-8 md:p-12 w-full md:w-2/3">
                  <div className="inline-flex items-center gap-2 px-3 py-1 bg-white/20 backdrop-blur-md border border-white/30 rounded-[2px] mb-4">
                    <Star size={16} weight="fill" className="text-white" />
                    <span className="text-[12px] tracking-[0.1em] font-semibold text-white uppercase">Nổi bật</span>
                  </div>
                  <h2 className="text-[28px] md:text-[48px] leading-[1.15] md:leading-[56px] text-white mb-4" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                    {featured.name}
                  </h2>
                  <div className="flex flex-wrap items-center gap-6 mb-8 text-[#f5f3ee] text-[16px]">
                    <div className="flex items-center gap-2">
                      <CalendarBlank size={20} />
                      <span>{formatShowDateLong(featured.scheduledStart)}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <MapPin size={20} />
                      <span>{featured.loungeName}</span>
                    </div>
                  </div>
                  <span className="inline-block bg-white text-[#181512] text-[14px] font-medium tracking-[0.02em] px-8 py-4 hover:bg-[#fbf9f4] transition-colors">
                    Xem vé — {priceRangeLabel(featured)}
                  </span>
                </div>
              </Link>
            </section>
          )}

          {/* Main List Grid */}
          <section>
            <div className="flex justify-between items-center mb-8 border-b border-[#cfc4bd]/50 pb-4">
              <h3 className="text-[24px] md:text-[32px] leading-[40px] text-[#181512]" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                Show sắp diễn ra
              </h3>
              <div className="flex items-center gap-4">
                <span className="text-[16px] text-[#4d4540]">
                  {shows === null ? "Đang tải..." : `Hiện ${totalCount} show`}
                </span>
                <select
                  value={sortBy}
                  onChange={(e) => {
                    setSortBy(e.target.value as LoungeShowSortBy);
                    setPage(1);
                  }}
                  className="px-3 py-2 bg-white border-b border-[#7e756f] focus:border-[#181512] text-[14px] outline-none"
                >
                  {SORT_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {errorMessage && <p className="text-sm text-[#ba1a1a] mb-4">{errorMessage}</p>}

            {shows && shows.length === 0 && !errorMessage && (
              <div className="text-center py-20 text-[#4d4540]">
                <p className="text-lg mb-1" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                  Không tìm thấy show nào
                </p>
                <p className="text-sm">Thử đổi bộ lọc hoặc từ khoá tìm kiếm.</p>
              </div>
            )}

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
              {(gridShows ?? Array.from({ length: 6 })).map((item, i) => {
                if (!item) {
                  return <div key={i} className="bg-[#f0eee9] animate-pulse aspect-[3/4]" />;
                }
                const show = item as LoungeShowListItem;
                return (
                  <Link
                    key={show.id}
                    to={`/shows/${show.id}`}
                    className="group flex flex-col border border-[#cfc4bd]/50 bg-white hover:bg-[#f5f3ee] transition-colors cursor-pointer"
                  >
                    <div className="h-64 w-full relative overflow-hidden">
                      {show.coverImageUrl ? (
                        <img
                          src={show.coverImageUrl}
                          alt={show.name}
                          className="object-cover w-full h-full transition-transform duration-500 group-hover:scale-105"
                        />
                      ) : (
                        <div className="w-full h-full bg-[#181512] flex items-center justify-center text-white/30 text-xs">
                          Không có ảnh
                        </div>
                      )}
                      <div className="absolute top-4 right-4 bg-white px-3 py-1 text-[12px] tracking-[0.1em] font-semibold text-[#181512] shadow-sm border border-[#cfc4bd]/50">
                        {priceRangeLabel(show)}
                      </div>
                    </div>
                    <div className="p-6 flex flex-col flex-1">
                      {show.genres.length > 0 && (
                        <div className="text-[12px] tracking-[0.1em] font-semibold text-[#775a19] mb-2 uppercase">
                          {show.genres.slice(0, 2).map((g) => g.name).join(" • ")}
                        </div>
                      )}
                      <h4 className="text-[24px] leading-[32px] text-[#181512] mb-2 line-clamp-2" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                        {show.name}
                      </h4>
                      {show.performerNames.length > 0 && (
                        <p className="text-[16px] text-[#4d4540] mb-6 line-clamp-2 flex-1">
                          Biểu diễn: {show.performerNames.join(", ")}
                        </p>
                      )}
                      <div className={`pt-4 border-t border-[#cfc4bd]/50 flex justify-between items-end ${show.performerNames.length === 0 ? "mt-auto" : ""}`}>
                        <div>
                          <div className="text-[14px] font-medium tracking-[0.02em] text-[#181512] mb-1">
                            {formatShowDateTime(show.scheduledStart)}
                          </div>
                          <div className="text-[14px] text-[#4d4540]">{show.loungeName}</div>
                        </div>
                        <ArrowRight size={20} className="text-[#7e756f] group-hover:text-[#181512] transition-colors" />
                      </div>
                    </div>
                  </Link>
                );
              })}
            </div>

            {totalPages > 1 && shows && (
              <div className="flex justify-center items-center gap-4 mt-8">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  aria-label="Trang trước"
                  className="w-10 h-10 flex items-center justify-center border border-[#cfc4bd]/50 text-[#7e756f] hover:text-[#181512] hover:border-[#181512] transition-colors disabled:opacity-50"
                >
                  ‹
                </button>
                <div className="flex gap-2 text-[14px] font-medium tracking-[0.02em]">
                  {paginationItems(page, totalPages).map((item, i) =>
                    item === "..." ? (
                      <span key={`ellipsis-${i}`} className="w-10 h-10 flex items-center justify-center text-[#7e756f]">
                        …
                      </span>
                    ) : (
                      <button
                        key={item}
                        onClick={() => setPage(item)}
                        className={`w-10 h-10 flex items-center justify-center transition-colors ${
                          item === page ? "bg-[#181512] text-white" : "text-[#1b1c19] hover:bg-[#f0eee9]"
                        }`}
                      >
                        {item}
                      </button>
                    )
                  )}
                </div>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page >= totalPages}
                  aria-label="Trang sau"
                  className="w-10 h-10 flex items-center justify-center border border-[#cfc4bd]/50 text-[#7e756f] hover:text-[#181512] hover:border-[#181512] transition-colors disabled:opacity-50"
                >
                  ›
                </button>
              </div>
            )}
          </section>
        </div>
      </main>

      <AudienceFooter />
    </div>
  );
}
