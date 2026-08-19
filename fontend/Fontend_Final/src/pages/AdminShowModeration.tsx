import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { animate, stagger } from "animejs";
import { Bell, Clock, Flag, Image as ImageIcon, MusicNotes, Sparkle, User, VideoCamera, WarningCircle } from "@phosphor-icons/react";
import { ApiError, getStoredToken, getStoredUser } from "../api/client";
import {
  AiModerationRecommendation,
  EventModeration,
  getPendingModerations,
  ModerationRiskLevel,
  ModerationTargetType,
  reviewModeration,
} from "../api/moderation";
import { formatRelativeTimeVi } from "../lib/relativeTime";
import AdminSidebar from "../components/AdminSidebar";

// Ported 2026-08-18 from `admin_content_moderation_queue_*` / `admin_moderation_detail_view` in the
// AuraLounge Stitch export, restyled onto this project's tokens and folded into one screen (queue +
// inline decision) rather than a list page plus a separate detail page.
//
// Covers all FOUR ModerationTargetTypes. Show and Livestream already had review endpoints; the
// GalleryImage and TourScene rows had none until `POST /moderations/images/{moderationId}/review`
// was added the same day -- before that they accumulated in this queue permanently and blew the
// NĐ 147/2024 24h SLA that `slaDeadline` exists to track.
//
// The three endpoints are keyed differently (target id for show/livestream, moderation row id for
// images) -- that routing lives in `reviewModeration` in api/moderation.ts, deliberately not here,
// so no screen has to remember which id to send.
//
// The SLA column is the point of this screen, not decoration: an overdue row is a compliance breach,
// so overdue rows are visually escalated rather than sorted quietly to the bottom.

const TARGET_META: Record<ModerationTargetType, { label: string; icon: typeof MusicNotes }> = {
  Show: { label: "Chương trình", icon: MusicNotes },
  Livestream: { label: "Livestream", icon: VideoCamera },
  GalleryImage: { label: "Ảnh thư viện", icon: ImageIcon },
  TourScene: { label: "Cảnh tour 360°", icon: ImageIcon },
};

const RISK_META: Record<ModerationRiskLevel, { label: string; className: string }> = {
  Low: { label: "Thấp", className: "bg-surface-container-high text-on-surface-variant" },
  Medium: { label: "Trung bình", className: "bg-secondary-container text-on-secondary-container" },
  High: { label: "Cao", className: "bg-error-container text-on-error-container" },
  Critical: { label: "Nghiêm trọng", className: "bg-error text-on-error" },
};

const RECOMMENDATION_LABELS: Record<AiModerationRecommendation, string> = {
  SuggestApprove: "AI đề xuất: duyệt",
  NeedsReview: "AI đề xuất: cần người xem",
  SuggestReject: "AI đề xuất: từ chối",
};

const FILTERS: { value: ModerationTargetType | ""; label: string }[] = [
  { value: "", label: "Tất cả" },
  { value: "Show", label: "Chương trình" },
  { value: "Livestream", label: "Livestream" },
  { value: "GalleryImage", label: "Ảnh thư viện" },
  { value: "TourScene", label: "Cảnh tour 360°" },
];

function isOverdue(m: EventModeration): boolean {
  return m.slaDeadline !== null && new Date(m.slaDeadline).getTime() < Date.now();
}

export default function AdminShowModeration() {
  const navigate = useNavigate();
  const [items, setItems] = useState<EventModeration[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState<ModerationTargetType | "">("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [decision, setDecision] = useState<"Approved" | "Rejected">("Approved");
  const [reviewNote, setReviewNote] = useState("");
  const [busyId, setBusyId] = useState<number | null>(null);
  const rowRefs = useRef<Record<number, HTMLDivElement | null>>({});
  const animatedKeys = useRef<Set<string>>(new Set());

  const pageSize = 20;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const overdueCount = items?.filter(isOverdue).length ?? 0;

  useEffect(() => {
    const token = getStoredToken();
    const user = getStoredUser();
    if (!token || user?.role !== "Admin") {
      navigate("/login");
      return;
    }
    let cancelled = false;
    setItems(null);
    getPendingModerations(filter === "" ? null : filter, page, pageSize)
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
        setTotalCount(result.totalCount);
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
    return () => {
      cancelled = true;
    };
  }, [navigate, page, filter]);

  useEffect(() => {
    const key = `${page}|${filter}`;
    if (items && items.length > 0 && !animatedKeys.current.has(key)) {
      animatedKeys.current.add(key);
      animate(".moderation-row", {
        opacity: [0, 1],
        translateY: [16, 0],
        delay: stagger(70),
        duration: 450,
        ease: "outQuad",
      });
    }
  }, [items, page, filter]);

  function openDecision(m: EventModeration) {
    setExpandedId((cur) => (cur === m.id ? null : m.id));
    // Default to whatever the AI leaned towards -- an Admin overriding a SuggestReject should be a
    // deliberate act, not the path of least resistance.
    setDecision(m.aiRecommendation === "SuggestReject" ? "Rejected" : "Approved");
    setReviewNote("");
    setActionError(null);
  }

  async function handleSubmit(m: EventModeration) {
    // Mirrors ReviewShowCommandValidator / ReviewImageCommandValidator: a rejection must say why.
    if (decision === "Rejected" && reviewNote.trim() === "") {
      setActionError("Phải ghi lý do khi từ chối.");
      return;
    }
    setBusyId(m.id);
    setActionError(null);
    try {
      await reviewModeration(m, {
        decision,
        reviewNote: reviewNote.trim() === "" ? null : reviewNote.trim(),
      });
      const el = rowRefs.current[m.id];
      const remove = () => {
        setItems((prev) => prev?.filter((x) => x.id !== m.id) ?? prev);
        setTotalCount((n) => Math.max(0, n - 1));
        setExpandedId(null);
      };
      if (!el) {
        remove();
        return;
      }
      animate(el, {
        opacity: [1, 0],
        scale: [1, 0.98],
        translateY: [0, -8],
        duration: 350,
        ease: "inQuad",
        onComplete: remove,
      });
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="bg-surface text-on-surface font-body-md min-h-screen flex">
      <AdminSidebar active="Kiểm duyệt Show" />

      <main className="ml-72 flex-1 flex flex-col min-h-screen">
        <header className="bg-surface border-b border-outline-variant/20 h-20 px-xl flex justify-between items-center sticky top-0 z-40">
          <div className="flex-1" />
          <div className="flex items-center gap-4">
            <button className="text-on-surface-variant hover:text-primary transition-colors p-2 rounded-full hover:bg-surface-variant/50">
              <Bell weight="light" size={22} />
            </button>
            <div className="w-10 h-10 rounded-full border border-outline-variant/30 bg-surface-container flex items-center justify-center">
              <User weight="light" size={20} className="text-on-surface-variant" />
            </div>
          </div>
        </header>

        <div className="px-xl py-lg max-w-container-max mx-auto w-full flex-1">
          <div className="mb-lg">
            <h1 className="font-headline-md text-headline-md text-on-surface mb-2">Kiểm duyệt nội dung</h1>
            <p className="font-body-md text-body-md text-on-surface-variant">
              {items === null ? "Đang tải..." : `${totalCount} nội dung chờ duyệt`}
              {overdueCount > 0 && (
                <span className="text-error font-semibold"> · {overdueCount} đã quá hạn SLA</span>
              )}
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2 mb-lg">
            {FILTERS.map((f) => (
              <button
                key={f.label}
                onClick={() => {
                  setFilter(f.value);
                  setPage(1);
                }}
                className={`px-4 py-2 rounded-full font-label-md text-label-md border transition-colors ${
                  filter === f.value
                    ? "border-primary bg-primary text-on-primary"
                    : "border-outline-variant bg-surface-container-low text-on-surface-variant hover:border-primary hover:text-primary"
                }`}
              >
                {f.label}
              </button>
            ))}
          </div>

          {errorMessage && (
            <div role="alert" aria-live="assertive" className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 mb-6">
              {errorMessage}
            </div>
          )}
          {actionError && (
            <div role="alert" aria-live="assertive" className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 mb-6">
              {actionError}
            </div>
          )}

          {items !== null && items.length === 0 && (
            <p className="font-body-md text-body-md text-on-surface-variant text-center py-24">
              Không có nội dung nào chờ duyệt.
            </p>
          )}

          <div className="border-t border-outline-variant/20">
            {items?.map((m) => {
              const meta = TARGET_META[m.targetType];
              const TargetIcon = meta?.icon ?? Flag;
              const overdue = isOverdue(m);
              const isOpen = expandedId === m.id;
              return (
                <div
                  key={m.id}
                  ref={(el) => {
                    rowRefs.current[m.id] = el;
                  }}
                  className={`moderation-row border-b border-outline-variant/20 transition-colors ${
                    isOpen ? "bg-surface-container-low" : ""
                  }`}
                >
                  <div className="py-8 flex flex-col lg:flex-row lg:items-start justify-between gap-6">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-3 mb-2 flex-wrap">
                        <TargetIcon weight="light" size={20} className="text-on-surface-variant shrink-0" />
                        <h3 className="font-headline-sm text-headline-sm text-on-surface">
                          {meta?.label ?? m.targetType} #{m.targetId}
                        </h3>
                        {m.riskLevel && (
                          <span
                            className={`font-label-md text-xs px-2.5 py-1 rounded-full ${RISK_META[m.riskLevel].className}`}
                          >
                            Rủi ro: {RISK_META[m.riskLevel].label}
                          </span>
                        )}
                        {overdue && (
                          <span className="inline-flex items-center gap-1.5 bg-error text-on-error font-label-md text-xs px-2.5 py-1 rounded-full">
                            <WarningCircle weight="fill" size={12} />
                            Quá hạn SLA
                          </span>
                        )}
                      </div>

                      {m.flagReason && (
                        <p className="font-body-md text-body-md text-on-surface mb-3">{m.flagReason}</p>
                      )}

                      <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm font-body-md text-on-surface-variant">
                        <div>
                          {m.aiRecommendation && (
                            <p className="flex items-center gap-2">
                              <Sparkle weight="light" size={16} />
                              {RECOMMENDATION_LABELS[m.aiRecommendation]}
                              {m.aiScore !== null && (
                                <span className="opacity-70">(điểm {m.aiScore.toFixed(2)})</span>
                              )}
                            </p>
                          )}
                          <p className="flex items-center gap-2 mt-1">
                            <Clock weight="light" size={16} />
                            Gắn cờ: {formatRelativeTimeVi(m.createdAt)}
                          </p>
                        </div>
                        <div>
                          {m.slaDeadline ? (
                            <p className={`flex items-center gap-2 ${overdue ? "text-error font-semibold" : ""}`}>
                              <WarningCircle weight="light" size={16} />
                              Hạn duyệt: {formatRelativeTimeVi(m.slaDeadline)}
                            </p>
                          ) : (
                            <p className="flex items-center gap-2 opacity-70 italic">
                              <WarningCircle weight="light" size={16} />
                              Không có hạn SLA được ghi nhận
                            </p>
                          )}
                        </div>
                      </div>
                    </div>

                    <div className="shrink-0">
                      <button
                        onClick={() => openDecision(m)}
                        disabled={busyId === m.id}
                        className="px-6 py-2.5 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50"
                      >
                        {isOpen ? "Đang duyệt..." : "Duyệt"}
                      </button>
                    </div>
                  </div>

                  <div
                    className={`grid transition-[grid-template-rows] duration-300 ease-out ${
                      isOpen ? "grid-rows-[1fr]" : "grid-rows-[0fr]"
                    }`}
                  >
                    <div className="overflow-hidden">
                      <div className="bg-surface-container p-md border-t border-outline-variant/20 flex flex-col gap-md">
                        <div>
                          <span className="block font-label-md text-label-md text-on-surface mb-2">Quyết định</span>
                          <div className="flex flex-wrap gap-2">
                            {(
                              [
                                ["Approved", "Duyệt"],
                                ["Rejected", "Từ chối"],
                              ] as const
                            ).map(([value, label]) => (
                              <button
                                key={value}
                                type="button"
                                onClick={() => setDecision(value)}
                                className={`px-5 py-2 rounded-full border font-label-md text-label-md transition-colors ${
                                  decision === value
                                    ? "border-primary bg-primary text-on-primary"
                                    : "border-outline-variant bg-surface text-on-surface-variant hover:border-primary hover:text-primary"
                                }`}
                              >
                                {label}
                              </button>
                            ))}
                          </div>
                          {decision === "Rejected" && (
                            <p className="text-[12px] text-on-surface-variant mt-2">
                              {m.targetType === "GalleryImage" || m.targetType === "TourScene"
                                ? "Từ chối sẽ gỡ bỏ vĩnh viễn hình ảnh này khỏi phòng trà."
                                : m.targetType === "Show"
                                  ? "Từ chối sẽ đưa chương trình về trạng thái Nháp để chủ phòng trà sửa lại."
                                  : "Từ chối sẽ chặn livestream này phát sóng."}
                            </p>
                          )}
                        </div>

                        <div>
                          <label
                            className="block font-label-md text-label-md text-on-surface mb-2"
                            htmlFor={`review-note-${m.id}`}
                          >
                            Ghi chú {decision === "Rejected" && <span className="text-error">*</span>}
                          </label>
                          <textarea
                            id={`review-note-${m.id}`}
                            rows={3}
                            maxLength={1000}
                            value={reviewNote}
                            onChange={(e) => setReviewNote(e.target.value)}
                            placeholder={
                              decision === "Rejected" ? "Bắt buộc: lý do từ chối..." : "Ghi chú thêm (tùy chọn)..."
                            }
                            className="w-full bg-surface border border-outline-variant rounded-lg p-3 text-on-surface focus:ring-2 focus:ring-primary focus:border-primary transition-all font-body-md"
                          />
                          <p className="text-[12px] text-on-surface-variant mt-1 text-right">
                            {reviewNote.length}/1000
                          </p>
                        </div>

                        <div className="flex justify-end gap-3">
                          <button
                            onClick={() => setExpandedId(null)}
                            className="px-4 py-2 rounded-lg text-on-surface-variant hover:bg-surface-variant transition-colors font-label-md text-label-md"
                          >
                            Hủy
                          </button>
                          <button
                            onClick={() => handleSubmit(m)}
                            disabled={busyId === m.id}
                            className="px-6 py-2 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50"
                          >
                            Xác nhận
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between mt-lg">
              <p className="font-body-md text-sm text-on-surface-variant">
                Trang {page} / {totalPages}
              </p>
              <div className="flex items-center gap-3">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="px-4 py-2 rounded-lg border border-outline text-on-surface font-label-md text-label-md hover:bg-surface-variant transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  Trước
                </button>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page >= totalPages}
                  className="px-4 py-2 rounded-lg border border-outline text-on-surface font-label-md text-label-md hover:bg-surface-variant transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  Sau
                </button>
              </div>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
