import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { animate, stagger } from "animejs";
import { Bell, Clock, FileText, Gavel, Storefront, User, WarningCircle } from "@phosphor-icons/react";
import { ApiError, getStoredToken, getStoredUser } from "../api/client";
import {
  AdminVenuePenalty,
  getVenuePenalties,
  PenaltyStatus,
  PenaltyType,
  reviewAppeal,
} from "../api/penalties";
import { formatRelativeTimeVi } from "../lib/relativeTime";
import AdminSidebar from "../components/AdminSidebar";

// Ported 2026-08-18 from `admin_resolution_config_portal` / `admin_dispute_resolution_detail` in the
// AuraLounge Stitch export, restyled onto this project's tokens.
//
// Defaults to the Appealed filter rather than "all", because that is the only status
// ReviewAppealCommand will act on (it throws 422 otherwise) -- opening on a list where most rows
// have no available action would misrepresent the work. The other statuses stay one click away for
// auditing.
//
// Wording trap this screen has to get right: the API's decision values are outcomes for the
// PENALTY, not for the appeal. "Overturned" = the appeal SUCCEEDED and the penalty is cancelled;
// "Upheld" = the penalty stands and the appeal failed. Labelling the buttons with the raw enum
// names would invite an Admin to click the exact opposite of what they intend, so they are labelled
// by consequence instead.

const TYPE_META: Record<PenaltyType, { label: string; className: string }> = {
  Warning: { label: "Cảnh cáo", className: "bg-surface-container-high text-on-surface-variant" },
  Suspension: { label: "Đình chỉ", className: "bg-secondary-container text-on-secondary-container" },
  Ban: { label: "Cấm vĩnh viễn", className: "bg-error text-on-error" },
};

const STATUS_META: Record<PenaltyStatus, { label: string; className: string }> = {
  Active: { label: "Đang hiệu lực", className: "bg-secondary-container text-on-secondary-container" },
  Appealed: { label: "Chờ xử lý kháng cáo", className: "bg-primary-container text-on-primary-container" },
  Overturned: { label: "Đã hủy bỏ", className: "bg-surface-container-high text-on-surface-variant" },
  Upheld: { label: "Giữ nguyên", className: "bg-surface-container-high text-on-surface-variant" },
  Expired: { label: "Hết hạn", className: "bg-surface-container-high text-on-surface-variant" },
};

const FILTERS: { value: PenaltyStatus | ""; label: string }[] = [
  { value: "Appealed", label: "Chờ xử lý kháng cáo" },
  { value: "Active", label: "Đang hiệu lực" },
  { value: "Overturned", label: "Đã hủy bỏ" },
  { value: "Upheld", label: "Giữ nguyên" },
  { value: "Expired", label: "Hết hạn" },
  { value: "", label: "Tất cả" },
];

export default function AdminPenalties() {
  const navigate = useNavigate();
  const [items, setItems] = useState<AdminVenuePenalty[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState<PenaltyStatus | "">("Appealed");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [decision, setDecision] = useState<"Overturned" | "Upheld">("Upheld");
  const [reviewNote, setReviewNote] = useState("");
  const [busyId, setBusyId] = useState<number | null>(null);
  const rowRefs = useRef<Record<number, HTMLDivElement | null>>({});
  const animatedKeys = useRef<Set<string>>(new Set());

  const pageSize = 20;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  useEffect(() => {
    const token = getStoredToken();
    const user = getStoredUser();
    if (!token || user?.role !== "Admin") {
      navigate("/login");
      return;
    }
    let cancelled = false;
    setItems(null);
    getVenuePenalties(filter === "" ? null : filter, page, pageSize)
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
      animate(".penalty-row", {
        opacity: [0, 1],
        translateY: [16, 0],
        delay: stagger(70),
        duration: 450,
        ease: "outQuad",
      });
    }
  }, [items, page, filter]);

  function openDecision(p: AdminVenuePenalty) {
    setExpandedId((cur) => (cur === p.id ? null : p.id));
    // No pre-selected sympathetic default: "Upheld" keeps the penalty as issued, which is the
    // no-change outcome. Overturning cancels a sanction and can restore a suspended venue, so it
    // should be a deliberate click.
    setDecision("Upheld");
    setReviewNote("");
    setActionError(null);
  }

  async function handleSubmit(p: AdminVenuePenalty) {
    setBusyId(p.id);
    setActionError(null);
    try {
      await reviewAppeal(p.id, decision, reviewNote.trim() === "" ? null : reviewNote.trim());
      // Reviewing moves the row out of Appealed, so it only leaves the list while that filter is
      // active; on any other filter it stays and just changes status.
      const leavesList = filter === "Appealed";
      const el = rowRefs.current[p.id];
      const apply = () => {
        setItems((prev) =>
          leavesList
            ? (prev?.filter((x) => x.id !== p.id) ?? prev)
            : (prev?.map((x) => (x.id === p.id ? { ...x, status: decision, appealResult: decision } : x)) ?? prev),
        );
        if (leavesList) setTotalCount((n) => Math.max(0, n - 1));
        setExpandedId(null);
      };
      if (!leavesList || !el) {
        apply();
        return;
      }
      animate(el, {
        opacity: [1, 0],
        scale: [1, 0.98],
        translateY: [0, -8],
        duration: 350,
        ease: "inQuad",
        onComplete: apply,
      });
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="bg-surface text-on-surface font-body-md min-h-screen flex">
      <AdminSidebar active="Xử phạt & Kháng cáo" />

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
            <h1 className="font-headline-md text-headline-md text-on-surface mb-2">Xử phạt &amp; Kháng cáo</h1>
            <p className="font-body-md text-body-md text-on-surface-variant">
              {items === null
                ? "Đang tải..."
                : filter === "Appealed"
                  ? `${totalCount} kháng cáo đang chờ xử lý`
                  : `${totalCount} bản ghi`}
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2 mb-lg">
            {FILTERS.map((f) => (
              <button
                key={f.label}
                onClick={() => {
                  setFilter(f.value);
                  setPage(1);
                  setExpandedId(null);
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
              {filter === "Appealed" ? "Không có kháng cáo nào đang chờ xử lý." : "Không có bản ghi nào."}
            </p>
          )}

          <div className="border-t border-outline-variant/20">
            {items?.map((p) => {
              const typeMeta = TYPE_META[p.penaltyType];
              const statusMeta = STATUS_META[p.status];
              const canReview = p.status === "Appealed";
              const isOpen = expandedId === p.id;
              return (
                <div
                  key={p.id}
                  ref={(el) => {
                    rowRefs.current[p.id] = el;
                  }}
                  className={`penalty-row border-b border-outline-variant/20 transition-colors ${
                    isOpen ? "bg-surface-container-low" : ""
                  }`}
                >
                  <div className="py-8 flex flex-col lg:flex-row lg:items-start justify-between gap-6">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-3 mb-2 flex-wrap">
                        <Gavel weight="light" size={20} className="text-on-surface-variant shrink-0" />
                        <h3 className="font-headline-sm text-headline-sm text-on-surface">
                          {p.loungeName || `Phòng trà #${p.loungeId}`}
                        </h3>
                        <span className={`font-label-md text-xs px-2.5 py-1 rounded-full ${typeMeta.className}`}>
                          {typeMeta.label}
                          {p.penaltyType === "Suspension" && p.suspensionDays ? ` ${p.suspensionDays} ngày` : ""}
                        </span>
                        <span className={`font-label-md text-xs px-2.5 py-1 rounded-full ${statusMeta.className}`}>
                          {statusMeta.label}
                        </span>
                      </div>

                      <p className="font-body-md text-body-md text-on-surface mb-3">{p.reason}</p>

                      <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm font-body-md text-on-surface-variant">
                        <div>
                          <p className="flex items-center gap-2">
                            <Storefront weight="light" size={16} />
                            {p.ownerName} ({p.ownerEmail})
                          </p>
                          <p className="flex items-center gap-2 mt-1">
                            <Clock weight="light" size={16} />
                            Ban hành: {formatRelativeTimeVi(p.issuedAt)}
                          </p>
                          {p.evidenceRef && (
                            <p className="flex items-center gap-2 mt-1">
                              <FileText weight="light" size={16} />
                              Bằng chứng: {p.evidenceRef}
                            </p>
                          )}
                        </div>
                        <div>
                          {p.appealedAt && (
                            <p className="flex items-center gap-2">
                              <WarningCircle weight="light" size={16} />
                              Kháng cáo lúc: {formatRelativeTimeVi(p.appealedAt)}
                            </p>
                          )}
                          {p.suspensionEnd && (
                            <p className="flex items-center gap-2 mt-1">
                              Đình chỉ đến: {formatRelativeTimeVi(p.suspensionEnd)}
                            </p>
                          )}
                          {p.reviewedAt && (
                            <p className="flex items-center gap-2 mt-1 opacity-80">
                              Đã xử lý: {formatRelativeTimeVi(p.reviewedAt)}
                              {p.appealResult ? ` · ${STATUS_META[p.appealResult as PenaltyStatus]?.label ?? p.appealResult}` : ""}
                            </p>
                          )}
                        </div>
                      </div>

                      {p.appealReason && (
                        <div className="mt-3 border-l-2 border-primary/40 pl-4">
                          <p className="font-label-md text-xs uppercase tracking-wider text-on-surface-variant mb-1">
                            Nội dung kháng cáo của chủ phòng trà
                          </p>
                          <p className="font-body-md text-body-md text-on-surface whitespace-pre-line">
                            {p.appealReason}
                          </p>
                        </div>
                      )}
                    </div>

                    <div className="shrink-0">
                      {canReview ? (
                        <button
                          onClick={() => openDecision(p)}
                          disabled={busyId === p.id}
                          className="px-6 py-2.5 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50"
                        >
                          {isOpen ? "Đang xử lý..." : "Xử lý kháng cáo"}
                        </button>
                      ) : (
                        <span className="inline-block px-4 py-2.5 font-body-md text-sm text-on-surface-variant/70 italic">
                          Không có hành động
                        </span>
                      )}
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
                            {/* Labelled by consequence, not by the raw enum name -- see the file
                                header: "Overturned" is an outcome for the PENALTY, so it means the
                                Owner's appeal was ACCEPTED. */}
                            {(
                              [
                                ["Overturned", "Chấp thuận kháng cáo (hủy án phạt)"],
                                ["Upheld", "Từ chối kháng cáo (giữ án phạt)"],
                              ] as const
                            ).map(([value, label]) => (
                              <button
                                key={value}
                                type="button"
                                onClick={() => setDecision(value)}
                                className={`px-4 py-2 rounded-full border font-label-md text-label-md transition-colors ${
                                  decision === value
                                    ? "border-primary bg-primary text-on-primary"
                                    : "border-outline-variant bg-surface text-on-surface-variant hover:border-primary hover:text-primary"
                                }`}
                              >
                                {label}
                              </button>
                            ))}
                          </div>
                          {decision === "Overturned" && (
                            <p className="text-[12px] text-on-surface-variant mt-2">
                              Án phạt sẽ bị hủy. Nếu đây là án phạt còn hiệu lực duy nhất, phòng trà được khôi phục
                              hoạt động.
                            </p>
                          )}
                        </div>

                        <div>
                          <label
                            className="block font-label-md text-label-md text-on-surface mb-2"
                            htmlFor={`review-note-${p.id}`}
                          >
                            Ghi chú gửi chủ phòng trà
                          </label>
                          <textarea
                            id={`review-note-${p.id}`}
                            rows={3}
                            maxLength={500}
                            value={reviewNote}
                            onChange={(e) => setReviewNote(e.target.value)}
                            placeholder="Giải thích quyết định..."
                            className="w-full bg-surface border border-outline-variant rounded-lg p-3 text-on-surface focus:ring-2 focus:ring-primary focus:border-primary transition-all font-body-md"
                          />
                          <p className="text-[12px] text-on-surface-variant mt-1 text-right">
                            {reviewNote.length}/500
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
                            onClick={() => handleSubmit(p)}
                            disabled={busyId === p.id}
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
