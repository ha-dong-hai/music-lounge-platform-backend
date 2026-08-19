import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  CalendarCheck,
  ChartLine,
  House,
  MusicNotes,
  PencilSimple,
  Plus,
  Storefront,
  UserCircle,
} from "@phosphor-icons/react";
import { ApiError, clearSession, getStoredToken } from "../api/client";
import { getMyLounges, LoungeListItem, LoungeStatus } from "../api/lounges";
import OwnerHeader from "../components/OwnerHeader";

// Ported from fontend/Fontend_Final/stitch/owner_my_venues/code.html (folder was originally
// named "page_event" -- a Stitch export mislabel confirmed against screen.png/code.html, which
// are actually the Owner's venue dashboard, not an event/show page). Same "Warm Luxury Lounge"
// system as the auth screens (tailwind.config.ts tokens), adapted from Stitch's 3-example mock
// (1 Approved/Pending/Rejected each) into a real list driven by GET /lounges?mine=true.
//
// Dropped from the mock: "Submitted" date and "Type" stat boxes on the Pending card -- neither
// field exists on LoungeListItemDto, and this port doesn't invent placeholder data BE doesn't
// return. Shown instead: real address + follower/upcoming-show counts, available on every status.

const STATUS_META: Record<LoungeStatus, { label: string; dotClass: string; textClass: string }> = {
  Pending: { label: "Đang chờ duyệt", dotClass: "bg-secondary-fixed animate-pulse", textClass: "text-secondary" },
  Approved: { label: "Đã duyệt", dotClass: "bg-tertiary", textClass: "text-tertiary" },
  Rejected: { label: "Cần chỉnh sửa", dotClass: "bg-error", textClass: "text-error" },
  Warned: { label: "Đang bị cảnh cáo", dotClass: "bg-secondary-fixed", textClass: "text-secondary" },
  Suspended: { label: "Đang bị tạm khoá", dotClass: "bg-error", textClass: "text-error" },
  Locked: { label: "Đã bị khoá vĩnh viễn", dotClass: "bg-error", textClass: "text-error" },
};

function formatAddress(v: LoungeListItem): string {
  return [v.street, v.district, v.city].filter(Boolean).join(", ");
}

function VenueImage({ url, alt, grayscale = false }: { url: string | null; alt: string; grayscale?: boolean }) {
  if (!url) {
    return (
      <div className="w-full h-full bg-surface-container-high flex items-center justify-center">
        <Storefront weight="light" size={36} className="text-on-surface-variant/40" />
      </div>
    );
  }
  return (
    <img
      className={`w-full h-full object-cover ${grayscale ? "grayscale-[30%] opacity-70" : ""}`}
      src={url}
      alt={alt}
    />
  );
}

function ApprovedCard({ venue }: { venue: LoungeListItem }) {
  return (
    <article className="relative group">
      {/* Photography stays sharp-cornered on purpose -- a deliberate contrast against the
          rounded UI chrome everywhere else, not an oversight. See DESIGN.md's own precedent:
          Polaroid photo frames are square, only buttons/inputs/cards are rounded. */}
      <div className="aspect-[21/9] w-full overflow-hidden relative shadow-[0_24px_60px_rgba(43,42,39,0.08)] rounded-none">
        <VenueImage url={venue.primaryImageUrl} alt={venue.name} />
        <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-black/20 to-transparent" />
        <div className="absolute bottom-0 left-0 p-8 md:p-12 w-full flex flex-col md:flex-row justify-between items-end gap-6">
          <div>
            <h2 className="font-display-lg text-headline-md md:text-display-lg text-white mb-2">{venue.name}</h2>
            <p className="font-label-md text-label-md text-white/80 tracking-widest uppercase">
              {formatAddress(venue)} • {venue.followerCount} người theo dõi
            </p>
          </div>
          <div className="flex gap-4">
            <Link
              to={`/owner/venues/${venue.id}/edit`}
              className="w-10 h-10 rounded-full bg-white/10 backdrop-blur-md flex items-center justify-center text-white hover:bg-white hover:text-primary transition-all duration-300 border border-white/20"
              aria-label={`Sửa ${venue.name}`}
            >
              <PencilSimple weight="light" size={20} />
            </Link>
            <Link
              to={`/owner/venues/${venue.id}/analytics`}
              className="w-10 h-10 rounded-full bg-white/10 backdrop-blur-md flex items-center justify-center text-white hover:bg-white hover:text-primary transition-all duration-300 border border-white/20"
              aria-label={`Thống kê ${venue.name}`}
            >
              <ChartLine weight="light" size={20} />
            </Link>
          </div>
        </div>
      </div>
    </article>
  );
}

function PendingLikeCard({ venue, reversed = false }: { venue: LoungeListItem; reversed?: boolean }) {
  const meta = STATUS_META[venue.status];
  return (
    <article
      className={`relative flex flex-col md:flex-row${reversed ? "-reverse" : ""} gap-12 items-center opacity-90 hover:opacity-100 transition-opacity duration-500`}
    >
      <div className="w-full md:w-2/5 aspect-[4/5] relative overflow-hidden rounded-none">
        <VenueImage url={venue.primaryImageUrl} alt={venue.name} grayscale />
      </div>
      <div className="w-full md:w-3/5 space-y-6">
        <div className="inline-flex items-center gap-3">
          <span className={`w-1.5 h-1.5 rounded-full ${meta.dotClass}`} />
          <span className={`font-label-md text-xs ${meta.textClass} uppercase tracking-[0.2em] font-light`}>
            {meta.label}
          </span>
        </div>
        <h2 className="font-display-lg text-headline-md text-on-surface">{venue.name}</h2>
        <p className="font-body-lg text-body-lg text-on-surface-variant max-w-lg leading-relaxed">
          {formatAddress(venue)}
        </p>
        <div className="pt-8 border-t border-outline-variant/20 flex gap-12">
          <div>
            <p className="font-label-md text-[10px] text-on-surface-variant uppercase tracking-[0.2em] mb-2 font-light">
              Người theo dõi
            </p>
            <p className="font-body-md text-on-surface font-light">{venue.followerCount}</p>
          </div>
          <div>
            <p className="font-label-md text-[10px] text-on-surface-variant uppercase tracking-[0.2em] mb-2 font-light">
              Show sắp diễn
            </p>
            <p className="font-body-md text-on-surface font-light">{venue.upcomingShowCount}</p>
          </div>
        </div>
      </div>
    </article>
  );
}

function RejectedCard({ venue, reversed = true }: { venue: LoungeListItem; reversed?: boolean }) {
  return (
    <article
      className={`relative flex flex-col md:flex-row${reversed ? "-reverse" : ""} gap-12 items-center`}
    >
      <div className="w-full md:w-2/5 aspect-square relative overflow-hidden bg-surface-variant flex items-center justify-center p-8 rounded-none">
        <VenueImage url={venue.primaryImageUrl} alt={venue.name} grayscale />
      </div>
      <div className="w-full md:w-3/5 space-y-6">
        <div className="inline-flex items-center gap-3">
          <span className="w-2 h-[1px] bg-error" />
          <span className="font-label-md text-xs text-error uppercase tracking-[0.2em] font-light">
            Cần chỉnh sửa
          </span>
        </div>
        <h2 className="font-display-lg text-headline-md text-on-surface">{venue.name}</h2>
        {venue.rejectionReason && (
          <div className="bg-surface-container-low p-8 border-l border-error/50 rounded-lg">
            <h3 className="font-title-lg text-xs text-on-surface mb-3 uppercase tracking-[0.2em] font-light">
              Ghi chú từ đội duyệt
            </h3>
            <p className="font-body-md text-on-surface-variant italic font-light leading-relaxed">
              &ldquo;{venue.rejectionReason}&rdquo;
            </p>
          </div>
        )}
        <p className="font-body-md text-on-surface-variant text-sm">
          Venue đã bị từ chối không nộp duyệt lại được — vui lòng tạo venue mới với thông tin đã sửa.
        </p>
        <Link
          to="/owner/venues/new"
          className="inline-block font-label-md text-xs text-primary hover:text-primary-container transition-colors uppercase tracking-[0.2em] border-b border-primary/30 pb-1 font-light"
        >
          Tạo venue mới
        </Link>
      </div>
    </article>
  );
}

// Warned/Suspended/Locked are penalty outcomes (VenuePenaltiesController), a completely different
// workflow from the approve/reject moderation above -- LoungeListItemDto carries no penalty reason
// text at all (that lives on VenuePenalty, a separate not-yet-ported view), so this deliberately
// doesn't reuse RejectedCard's copy/rejectionReason display, which would be wrong for this case.
function PenaltyCard({ venue, reversed = true }: { venue: LoungeListItem; reversed?: boolean }) {
  const meta = STATUS_META[venue.status];
  const isLocked = venue.status === "Locked";
  return (
    <article
      className={`relative flex flex-col md:flex-row${reversed ? "-reverse" : ""} gap-12 items-center opacity-90`}
    >
      <div className="w-full md:w-2/5 aspect-square relative overflow-hidden bg-surface-variant flex items-center justify-center p-8 rounded-none">
        <VenueImage url={venue.primaryImageUrl} alt={venue.name} grayscale />
      </div>
      <div className="w-full md:w-3/5 space-y-6">
        <div className="inline-flex items-center gap-3">
          <span className={`w-2 h-2 rounded-full ${meta.dotClass}`} />
          <span className={`font-label-md text-xs ${meta.textClass} uppercase tracking-[0.2em] font-light`}>
            {meta.label}
          </span>
        </div>
        <h2 className={`font-display-lg text-headline-md text-on-surface ${isLocked ? "opacity-60" : ""}`}>
          {venue.name}
        </h2>
        <p className="font-body-md text-on-surface-variant text-sm leading-relaxed">
          {isLocked
            ? "Venue đã bị khoá vĩnh viễn theo quyết định xử phạt — không thể vận hành cho tới khi có kháng cáo được chấp nhận."
            : "Venue đang bị hạn chế một phần chức năng vận hành theo quyết định xử phạt. Xem chi tiết và gửi kháng cáo nếu cần."}
        </p>
        <Link
          to="/owner/penalties"
          className="inline-block font-label-md text-xs text-primary hover:text-primary-container transition-colors uppercase tracking-[0.2em] border-b border-primary/30 pb-1 font-light"
        >
          Xem xử phạt &amp; kháng cáo
        </Link>
      </div>
    </article>
  );
}

export default function OwnerMyVenues() {
  const navigate = useNavigate();
  const [venues, setVenues] = useState<LoungeListItem[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }

    let cancelled = false;
    getMyLounges()
      .then((result) => {
        if (!cancelled) setVenues(result.items);
      })
      .catch((err) => {
        if (cancelled) return;
        if (err instanceof ApiError && err.status === 401) {
          clearSession();
          navigate("/login");
          return;
        }
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });

    return () => {
      cancelled = true;
    };
  }, [navigate]);

  return (
    <div className="text-on-background min-h-screen flex flex-col selection:bg-primary selection:text-on-primary font-body-md text-body-md bg-surface">
      <OwnerHeader active="Venues" />

      {/* Main Content */}
      <main className="flex-1 w-full min-h-screen pb-24 md:pb-0">
        <div className="max-w-container-max mx-auto px-margin-mobile md:px-lg py-16 md:py-24">
          {/* Header Section */}
          <div className="flex flex-col md:flex-row justify-between items-start md:items-end mb-24 gap-8">
            <div>
              <p className="font-label-md text-label-md text-secondary uppercase tracking-[0.2em] mb-4">
                Curated Spaces
              </p>
              <h1 className="font-display-lg text-display-lg-mobile md:text-display-lg text-on-surface max-w-2xl leading-tight">
                Your bespoke listening environments.
              </h1>
            </div>
            <Link
              to="/owner/venues/new"
              className="group flex items-center gap-4 px-8 py-3 border border-primary/30 hover:border-primary hover:bg-primary/5 transition-all duration-700 relative overflow-hidden rounded-lg"
            >
              <span className="font-caption-handwriting text-lg text-primary italic tracking-wide">
                Add New Space
              </span>
              <Plus
                weight="light"
                size={20}
                className="text-primary group-hover:rotate-90 transition-transform duration-700"
              />
            </Link>
          </div>

          {/* Loading */}
          {venues === null && !errorMessage && (
            <p className="font-body-md text-body-md text-on-surface-variant text-center py-24">Đang tải...</p>
          )}

          {/* Error */}
          {errorMessage && (
            <div
              role="alert"
              aria-live="assertive"
              className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 max-w-lg mx-auto text-center"
            >
              {errorMessage}
            </div>
          )}

          {/* Empty state */}
          {venues !== null && venues.length === 0 && (
            <div className="text-center py-24 space-y-4">
              <p className="font-headline-md text-headline-sm text-on-surface">Bạn chưa có venue nào.</p>
              <p className="font-body-md text-body-md text-on-surface-variant">
                Tạo venue đầu tiên để bắt đầu mở bán vé và tổ chức show.
              </p>
              <Link
                to="/owner/venues/new"
                className="inline-block mt-4 px-8 py-3 bg-primary text-on-primary font-label-md text-label-md rounded-xl hover:opacity-90 transition-opacity"
              >
                Add New Space
              </Link>
            </div>
          )}

          {/* Venues list */}
          {venues !== null && venues.length > 0 && (
            <div className="space-y-32">
              {venues.map((venue, i) => {
                if (venue.status === "Approved") return <ApprovedCard key={venue.id} venue={venue} />;
                if (venue.status === "Rejected") return <RejectedCard key={venue.id} venue={venue} reversed={i % 2 === 0} />;
                if (venue.status === "Warned" || venue.status === "Suspended" || venue.status === "Locked")
                  return <PenaltyCard key={venue.id} venue={venue} reversed={i % 2 === 0} />;
                return <PendingLikeCard key={venue.id} venue={venue} reversed={i % 2 === 1} />;
              })}
            </div>
          )}
        </div>
      </main>

      {/* Footer */}
      <footer className="w-full py-xl mt-32 bg-surface-container-highest flex flex-col md:flex-row justify-between items-center px-margin-mobile md:px-lg border-t border-outline-variant/10">
        <p className="font-body-md text-body-md text-secondary mb-4 md:mb-0 font-light">
          © 2026 MusicLounge. High-Fidelity Discovery.
        </p>
        <div className="flex gap-8">
          {["Privacy", "Terms", "Instagram", "Journal"].map((label) => (
            <a
              key={label}
              className="font-label-md text-xs uppercase tracking-[0.1em] text-on-surface-variant hover:text-primary transition-all duration-200 font-light"
              href="#"
            >
              {label}
            </a>
          ))}
        </div>
      </footer>

      {/* Bottom Navigation (Mobile) */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 bg-surface/90 backdrop-blur-xl border-t border-outline-variant/20 z-50 flex justify-around items-center py-4 px-6">
        <Link className="flex flex-col items-center text-primary" to="/owner/venues">
          <House weight="fill" size={22} />
          <span className="text-[10px] uppercase tracking-widest mt-1">Venues</span>
        </Link>
        <Link className="flex flex-col items-center text-on-surface-variant" to="/owner/shows">
          <MusicNotes weight="light" size={22} />
          <span className="text-[10px] uppercase tracking-widest mt-1">Shows</span>
        </Link>
        <Link className="flex flex-col items-center text-on-surface-variant" to="/owner/bookings">
          <CalendarCheck weight="light" size={22} />
          <span className="text-[10px] uppercase tracking-widest mt-1">Bookings</span>
        </Link>
        <Link className="flex flex-col items-center text-on-surface-variant" to="/owner/settings">
          <UserCircle weight="light" size={22} />
          <span className="text-[10px] uppercase tracking-widest mt-1">Profile</span>
        </Link>
      </nav>
    </div>
  );
}
