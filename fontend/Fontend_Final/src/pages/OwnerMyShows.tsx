import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { animate, stagger } from "animejs";
import { CalendarBlank, Image, MapPin, Plus } from "@phosphor-icons/react";
import { ApiError, getStoredToken } from "../api/client";
import { getMyShows, LoungeShowFormat, LoungeShowListItem, LoungeShowStatus } from "../api/shows";
import OwnerHeader from "../components/OwnerHeader";

// Ported from a Stitch MCP generation (docs/stitch/Stitch-Master-Brief.md "My Shows"), wired to the real
// GET /lounge-shows?mine=true (returns every lifecycle status across all the owner's venues, not
// just published ones -- confirmed by reading GetPublishedLoungeShowsQueryHandler/GetMineAsync
// before building this, not assumed). Two generations came back from the same prompt this time
// (Stitch MCP had a rough patch mid-session -- see docs' own note) -- picked the one whose header
// matched this project's real nav exactly, and took its editorial-list structure, but swapped the
// status treatment from filled pills to plain colored text (closer to the original ask and this
// project's own restraint principle -- a pill on every row is close to the same "badge-everywhere"
// tell already fixed elsewhere). Photography thumbnails stay softly rounded here, not sharp-cornered
// like hero images elsewhere -- at 80-96px in a dense list they read as small UI elements, not
// feature photography, so the "sharp corners for photography" rule doesn't mechanically apply.

const STATUS_META: Record<LoungeShowStatus, { label: string; textClass: string; dot?: boolean }> = {
  Draft: { label: "Bản nháp", textClass: "text-on-surface-variant" },
  Pending: { label: "Chờ duyệt", textClass: "text-secondary" },
  Published: { label: "Đã xuất bản", textClass: "text-primary" },
  Ongoing: { label: "Đang diễn ra", textClass: "text-tertiary", dot: true },
  Ended: { label: "Đã kết thúc", textClass: "text-on-surface-variant/70" },
  Cancelled: { label: "Đã huỷ", textClass: "text-error" },
};

const FORMAT_LABEL: Record<LoungeShowFormat, string> = {
  Offline: "Trực tiếp",
  Online: "Truyền hình",
};

function formatShowDateTime(iso: string): string {
  const d = new Date(iso);
  const date = d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
  const time = d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  return `${date}, ${time}`;
}

export default function OwnerMyShows() {
  const navigate = useNavigate();
  const [shows, setShows] = useState<LoungeShowListItem[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const hasAnimatedEntrance = useRef(false);

  useEffect(() => {
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }
    let cancelled = false;
    getMyShows()
      .then((result) => {
        if (!cancelled) setShows(result.items);
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
    return () => {
      cancelled = true;
    };
  }, [navigate]);

  useEffect(() => {
    if (shows && shows.length > 0 && !hasAnimatedEntrance.current) {
      hasAnimatedEntrance.current = true;
      animate(".my-show-row", {
        opacity: [0, 1],
        translateY: [12, 0],
        delay: stagger(60),
        duration: 450,
        ease: "outQuad",
      });
    }
  }, [shows]);

  return (
    <div className="bg-surface text-on-background min-h-screen flex flex-col">
      <OwnerHeader active="Shows" />

      <main className="flex-grow max-w-container-max mx-auto px-margin-mobile md:px-gutter w-full py-xl">
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center mb-lg gap-md">
          <h1 className="font-headline-md text-headline-md text-on-background">Show của bạn</h1>
          <Link
            to="/owner/shows/new"
            className="bg-primary text-on-primary font-headline-sm text-[16px] py-3 px-6 rounded-xl flex items-center gap-2 hover:bg-primary-container transition-colors"
          >
            <Plus weight="bold" size={18} />
            Tạo show mới
          </Link>
        </div>

        {errorMessage && (
          <div
            role="alert"
            aria-live="assertive"
            className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 mb-6"
          >
            {errorMessage}
          </div>
        )}

        {shows === null && !errorMessage && (
          <p className="font-body-md text-body-md text-on-surface-variant py-24 text-center">Đang tải...</p>
        )}

        {shows !== null && shows.length === 0 && (
          <div className="text-center py-24 space-y-4">
            <p className="font-headline-md text-headline-sm text-on-surface">Bạn chưa có show nào.</p>
            <p className="font-body-md text-body-md text-on-surface-variant">
              Tạo show đầu tiên để bắt đầu mở bán vé.
            </p>
            <Link
              to="/owner/shows/new"
              className="inline-block mt-4 px-8 py-3 bg-primary text-on-primary font-label-md text-label-md rounded-xl hover:bg-primary-container transition-colors"
            >
              Tạo show mới
            </Link>
          </div>
        )}

        {shows !== null && shows.length > 0 && (
          <div className="flex flex-col border-t border-outline-variant/30">
            {shows.map((show) => {
              const meta = STATUS_META[show.status];
              const isCancelled = show.status === "Cancelled";
              const isDraft = show.status === "Draft";
              return (
                <div
                  key={show.id}
                  onClick={() => navigate(`/owner/shows/${show.id}`)}
                  className={`my-show-row flex items-center gap-4 py-4 border-b border-outline-variant/30 px-2 md:px-3 -mx-2 md:-mx-3 rounded-xl cursor-pointer hover:bg-surface-container-lowest transition-colors ${
                    isDraft ? "opacity-80" : ""
                  } ${isCancelled ? "opacity-60 grayscale-[50%]" : ""}`}
                >
                  {show.coverImageUrl ? (
                    <img
                      src={show.coverImageUrl}
                      alt={show.name}
                      className="w-20 h-20 md:w-24 md:h-24 object-cover rounded-lg flex-shrink-0 shadow-sm"
                    />
                  ) : (
                    <div className="w-20 h-20 md:w-24 md:h-24 rounded-lg flex-shrink-0 bg-surface-variant flex items-center justify-center border border-dashed border-outline-variant/50">
                      <Image weight="light" size={28} className="text-outline" />
                    </div>
                  )}
                  <div className="flex-grow min-w-0">
                    <h3
                      className={`font-title-lg text-title-lg text-on-surface truncate mb-1 ${
                        isCancelled ? "line-through" : ""
                      }`}
                    >
                      {show.name}
                    </h3>
                    <div className="font-body-md text-body-md text-on-surface-variant flex flex-col md:flex-row md:items-center gap-1 md:gap-4">
                      <span className="flex items-center gap-1">
                        <MapPin weight="light" size={16} />
                        {show.loungeName} ({show.loungeDistrict})
                      </span>
                      <span className="hidden md:inline text-outline-variant">•</span>
                      <span className="flex items-center gap-1">
                        <CalendarBlank weight="light" size={16} />
                        {formatShowDateTime(show.scheduledStart)}
                      </span>
                      <span className="hidden md:inline text-outline-variant">•</span>
                      <span className="uppercase tracking-wide text-[11px]">{FORMAT_LABEL[show.format]}</span>
                    </div>
                  </div>
                  <div className="flex-shrink-0 text-right">
                    <span className={`inline-flex items-center gap-2 font-label-md text-label-md ${meta.textClass}`}>
                      {meta.dot && <span className="w-1.5 h-1.5 rounded-full bg-tertiary animate-pulse" />}
                      {meta.label}
                    </span>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </main>
    </div>
  );
}
