import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Sparkle } from "@phosphor-icons/react";
import { getStoredToken } from "../api/client";
import { RecommendedShow, getRecommendedShows } from "../api/shows";
import { getLounges, LoungeListItem } from "../api/lounges";
import AudienceHeader from "../components/AudienceHeader";
import AudienceFooter from "../components/AudienceFooter";

// New page, 2026-08-18 -- ported literally from stitch_ai_audience/auralounge_desktop_discovery_hub
// (read in full). This used to be merged into DiscoverShows.tsx as just a styled section, but the
// user pointed out event_directory and discovery_hub are two separate screens in the mock and
// should stay that way ("là 2 view rõ ràng cho tôi không gộp") -- this file is the discovery_hub
// half; DiscoverShows.tsx (event_directory) is the other, reachable via the "Sự kiện" nav item.
//
// Two elements in the mock have no real data behind them and are adapted, not faked:
// - "Refine Taste Profile" button: no taste-profile-editing feature exists anywhere in this app.
//   Relabelled to an honest CTA ("Khám phá tất cả show") that links to the real Event Directory
//   instead of pretending to open a settings flow that doesn't exist.
// - "Trending Sanctuaries" venue cards: the mock's "warmth %" progress bar has no backing metric on
//   the backend at all. Kept the real card structure (real venue photo/name/district) but swapped
//   the fake bar for LoungeListItem's actual followerCount/upcomingShowCount -- real numbers, same
//   "which venues are hot right now" intent, ranked by upcomingShowCount.
//
// Border-radius: DEFAULT=2px, lg=4px, xl=8px, full=12px per this mock's own tailwind-config, EXCEPT
// icon-only circular buttons and the hero's pill CTA, which use a true rounded-full (9999px) per
// stitch_ai_audience/aura_echo/DESIGN.md's canonical "full" value -- the per-screen config capping
// "full" at 12px is a Stitch-generation quirk, not the documented design intent for round shapes.

function formatShowDateTime(iso: string): string {
  const d = new Date(iso);
  const datePart = d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" });
  const timePart = d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  return `${timePart} · ${datePart}`;
}

export default function DiscoveryHub() {
  // null = not fetched (loading, or anonymous -- section hides for both instead of an empty state).
  const [recommended, setRecommended] = useState<RecommendedShow[] | null>(null);
  const [trending, setTrending] = useState<LoungeListItem[] | null>(null);

  useEffect(() => {
    if (!getStoredToken()) return;
    getRecommendedShows(6)
      .then(setRecommended)
      .catch(() => setRecommended(null));
  }, []);

  useEffect(() => {
    getLounges({ pageSize: 100 })
      .then((result) => {
        const ranked = [...result.items].sort((a, b) => b.upcomingShowCount - a.upcomingShowCount);
        setTrending(ranked.slice(0, 6));
      })
      .catch(() => setTrending([]));
  }, []);

  const heroImage = trending?.find((l) => l.primaryImageUrl)?.primaryImageUrl ?? null;

  return (
    <div className="bg-[#fbf9f4] min-h-screen text-[#1b1c19] flex flex-col" style={{ fontFamily: "'Manrope', sans-serif" }}>
      <AudienceHeader active="Khám phá" />

      <main className="flex-1">
        {/* Hero */}
        <section className="relative w-full h-[70vh] min-h-[500px] flex items-center justify-center px-6 md:px-16 overflow-hidden">
          <div className="absolute inset-0 bg-[#181512]">
            {heroImage && (
              <div
                className="absolute inset-0 bg-cover bg-center w-full h-full"
                style={{ backgroundImage: `url('${heroImage}')` }}
              />
            )}
            <div className="absolute inset-0 bg-gradient-to-t from-[#181512] via-[#181512]/50 to-[#181512]/20" />
          </div>
          <div className="relative z-10 max-w-[1440px] mx-auto text-center flex flex-col items-center gap-6">
            <h1 className="text-[36px] md:text-[64px] leading-[1.15] md:leading-[72px] tracking-[-0.02em] text-white max-w-[800px] mx-auto">
              Bớt ồn ào hơn, nhiều cộng hưởng hơn.
            </h1>
            <p className="text-[16px] md:text-[18px] text-[#e4e2dd] max-w-[600px] mx-auto">
              Khám phá những buổi biểu diễn thân mật, chọn lọc theo đúng gu nghe nhạc của bạn tại những không gian âm nhạc độc đáo nhất Việt Nam.
            </p>
            <Link
              to="/shows"
              className="mt-2 flex items-center gap-2 border border-white/40 px-6 py-3 rounded-full text-white hover:bg-white/10 transition-colors bg-white/10 backdrop-blur-md"
            >
              <span>Khám phá tất cả show</span>
            </Link>
          </div>
        </section>

        {/* Curated Soundscape -- real GET /recommendations data (MLNetRecommendationService), not
            decorative. A fresh account with no AiConsent/interaction history yet gets a real "Đang
            thịnh hành" fallback from the backend itself, rendered through this same UI. */}
        {recommended && recommended.length > 0 && (
          <section className="max-w-[1440px] mx-auto px-6 md:px-16 py-16">
            <div className="flex justify-between items-end mb-8">
              <div>
                <h2 className="text-[24px] md:text-[32px] leading-[40px] text-[#181512] mb-2" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                  Gợi ý dành cho bạn
                </h2>
                <p className="text-[16px] text-[#4d4540]">Show phù hợp với gu nghe nhạc của bạn.</p>
              </div>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
              {recommended.map((show, idx) => {
                const matchPercent = Math.round(Math.min(1, Math.max(0, show.recommendationScore)) * 100);
                const tags = show.genres.length > 0 ? show.genres.slice(0, 2).map((g) => g.name) : [show.recommendationReason];
                return (
                  <Link
                    key={show.id}
                    to={`/shows/${show.id}`}
                    className={`group cursor-pointer ${idx === 1 ? "md:mt-8" : ""}`}
                  >
                    <div className="relative aspect-[4/5] rounded-[8px] overflow-hidden mb-4 bg-[#eae8e3] border border-[#7e756f]/10">
                      {show.coverImageUrl ? (
                        <img
                          src={show.coverImageUrl}
                          alt={show.name}
                          className="w-full h-full object-cover transform group-hover:scale-105 transition-transform duration-700"
                        />
                      ) : (
                        <div className="w-full h-full bg-[#181512] flex items-center justify-center text-white/30 text-xs">
                          Không có ảnh
                        </div>
                      )}
                      {matchPercent > 0 && (
                        <div className="absolute top-4 right-4 bg-white/70 backdrop-blur-md px-3 py-1 rounded-[12px] border border-white/20 flex items-center gap-1 shadow-sm">
                          <Sparkle size={16} weight="fill" className="text-[#775a19]" />
                          <span className="text-[12px] tracking-[0.1em] font-semibold text-[#181512]">{matchPercent}% phù hợp</span>
                        </div>
                      )}
                    </div>
                    <div className="flex flex-col gap-1">
                      <h3 className="text-[24px] leading-[32px] text-[#181512]" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                        {show.name}
                      </h3>
                      <p className="text-[16px] text-[#4d4540]">
                        {show.loungeName} • {formatShowDateTime(show.scheduledStart)}
                      </p>
                      <div className="flex flex-wrap gap-2 mt-2">
                        {tags.map((t) => (
                          <span key={t} className="px-2 py-1 bg-[#f0eee9] rounded-[2px] text-[12px] tracking-[0.1em] text-[#4d4540] border border-[#7e756f]/10">
                            {t}
                          </span>
                        ))}
                      </div>
                    </div>
                  </Link>
                );
              })}
            </div>
          </section>
        )}

        {/* Trending Sanctuaries -- real venues (GET /lounges), ranked by real upcomingShowCount
            instead of the mock's fabricated "warmth %" bar. See file header comment. */}
        {trending && trending.length > 0 && (
          <section className="w-full bg-white border-y border-[#7e756f]/10 py-16 overflow-hidden">
            <div className="max-w-[1440px] mx-auto px-6 md:px-16 mb-8">
              <h2 className="text-[24px] md:text-[32px] leading-[40px] text-[#181512] mb-2" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                Địa Điểm Nổi Bật
              </h2>
              <p className="text-[16px] text-[#4d4540]">Những venue có nhiều show sắp diễn ra nhất.</p>
            </div>
            <div className="flex overflow-x-auto gap-8 px-6 md:px-16 pb-4" style={{ scrollbarWidth: "none" }}>
              {trending.map((lounge) => (
                <Link
                  key={lounge.id}
                  to={`/shows?loungeId=${lounge.id}`}
                  className="min-w-[300px] md:min-w-[400px] flex-shrink-0 flex flex-col group cursor-pointer"
                >
                  <div className="aspect-video bg-[#eae8e3] rounded-[4px] mb-4 overflow-hidden relative border border-[#7e756f]/10">
                    {lounge.primaryImageUrl ? (
                      <img
                        src={lounge.primaryImageUrl}
                        alt={lounge.name}
                        className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                      />
                    ) : (
                      <div className="w-full h-full bg-[#181512]" />
                    )}
                  </div>
                  <h4 className="text-[24px] leading-[32px] text-[#181512]" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
                    {lounge.name}
                  </h4>
                  <p className="text-[16px] text-[#4d4540] mb-2">
                    {lounge.district}, {lounge.city}
                  </p>
                  <p className="text-[12px] tracking-[0.1em] text-[#775a19] font-semibold uppercase">
                    {lounge.upcomingShowCount} show sắp tới · {lounge.followerCount} người theo dõi
                  </p>
                </Link>
              ))}
            </div>
          </section>
        )}
      </main>

      <AudienceFooter />
    </div>
  );
}
