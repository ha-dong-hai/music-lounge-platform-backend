import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { animate, stagger } from "animejs";
import { Bell, Clock, LinkSimple, Phone, Target, User } from "@phosphor-icons/react";
import { ApiError, getStoredToken, getStoredUser } from "../api/client";
import {
  Complaint,
  ComplaintCategory,
  ComplaintResolvedAction,
  getPendingComplaints,
  resolveComplaint,
} from "../api/admin";
import { formatRelativeTimeVi } from "../lib/relativeTime";
import AdminSidebar from "../components/AdminSidebar";

// Ported 2026-08-18 from `admin_dispute_resolution_detail` in the AuraLounge Stitch export, restyled
// onto this project's tokens. The mock was a single-complaint detail page; folded into the queue as
// an expanding row instead, matching how AdminPendingVenues and AdminRefunds already work -- an
// Admin working a queue shouldn't lose their place to a full page navigation for each decision.
//
// The resolution form mirrors ResolveComplaintCommandValidator's rules exactly rather than letting
// the server be the first thing that says no:
//   * Status is limited to Investigating / Resolved / Rejected -- "Open" is deliberately absent,
//     the validator's AllowedStatuses does not include it (you cannot un-triage a complaint).
//   * Picking "Resolved" makes an action mandatory (validator: NotNull When Status == "Resolved").
//   * Picking "Compensate" makes an amount mandatory (validator: NotNull When action == Compensate).
//     Refund needs no amount -- it defaults server-side to the full ticket price.
//   * "TakeDownContent" is only offered when targetType is "show": the handler routes it through
//     CancelLoungeShowCommand, so it is structurally meaningless for any other target.
//   * Resolution text is capped at 2000 (validator: MaximumLength(2000)).

const CATEGORY_LABELS: Record<ComplaintCategory, string> = {
  EventMisrepresentation: "Sai lệch thông tin sự kiện",
  RefundDispute: "Tranh chấp hoàn tiền",
  DonationNotPaid: "Chưa thanh toán donation",
  TechnicalIssue: "Sự cố kỹ thuật",
  VenueConduct: "Hành vi của phòng trà",
  PenaltyAppeal: "Kháng cáo xử phạt",
  Other: "Khác",
};

const ACTION_LABELS: Record<ComplaintResolvedAction, string> = {
  Refund: "Hoàn tiền",
  Compensate: "Bồi thường",
  IssueWarning: "Cảnh cáo",
  Dismiss: "Bác bỏ",
  TakeDownContent: "Gỡ nội dung",
};

const RESOLUTION_MAX = 2000;

type DecisionStatus = "Investigating" | "Resolved" | "Rejected";

export default function AdminComplaints() {
  const navigate = useNavigate();
  const [complaints, setComplaints] = useState<Complaint[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [status, setStatus] = useState<DecisionStatus>("Resolved");
  const [resolvedAction, setResolvedAction] = useState<ComplaintResolvedAction | "">("");
  const [refundAmount, setRefundAmount] = useState("");
  const [resolution, setResolution] = useState("");
  const [busyId, setBusyId] = useState<number | null>(null);
  const rowRefs = useRef<Record<number, HTMLDivElement | null>>({});
  const animatedPages = useRef<Set<number>>(new Set());

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
    setComplaints(null);
    getPendingComplaints(page, pageSize)
      .then((result) => {
        if (cancelled) return;
        setComplaints(result.items);
        setTotalCount(result.totalCount);
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
    return () => {
      cancelled = true;
    };
  }, [navigate, page]);

  useEffect(() => {
    if (complaints && complaints.length > 0 && !animatedPages.current.has(page)) {
      animatedPages.current.add(page);
      animate(".complaint-row", {
        opacity: [0, 1],
        translateY: [16, 0],
        delay: stagger(80),
        duration: 500,
        ease: "outQuad",
      });
    }
  }, [complaints, page]);

  function openDecision(c: Complaint) {
    setExpandedId((cur) => (cur === c.id ? null : c.id));
    setStatus("Resolved");
    setResolvedAction("");
    setRefundAmount("");
    setResolution("");
    setActionError(null);
  }

  // Only a Resolved decision removes the complaint from this queue -- GetPendingComplaintsQuery
  // returns unresolved items, and "Investigating" is still unresolved, so that row stays put and
  // just updates its status badge in place.
  function applyOutcome(c: Complaint, decided: DecisionStatus) {
    if (decided === "Investigating") {
      setComplaints((prev) => prev?.map((x) => (x.id === c.id ? { ...x, status: "Investigating" } : x)) ?? prev);
      setExpandedId(null);
      return;
    }
    const el = rowRefs.current[c.id];
    const remove = () => {
      setComplaints((prev) => prev?.filter((x) => x.id !== c.id) ?? prev);
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
  }

  async function handleSubmit(c: Complaint) {
    if (status === "Resolved" && resolvedAction === "") {
      setActionError("Cần chọn hướng xử lý khi đánh dấu Đã giải quyết.");
      return;
    }
    const needsAmount = resolvedAction === "Compensate";
    const parsedAmount = Number(refundAmount);
    if (needsAmount && (!Number.isFinite(parsedAmount) || parsedAmount <= 0)) {
      setActionError("Bồi thường cần khai rõ số tiền lớn hơn 0.");
      return;
    }
    setBusyId(c.id);
    setActionError(null);
    try {
      await resolveComplaint(c.id, {
        status,
        resolution: resolution.trim() === "" ? null : resolution.trim(),
        resolvedAction: status === "Resolved" ? (resolvedAction as ComplaintResolvedAction) : null,
        // Refund's amount is optional (server defaults to the full ticket price); only send a
        // figure when one was actually typed.
        refundAmount: refundAmount.trim() === "" ? null : parsedAmount,
      });
      applyOutcome(c, status);
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyId(null);
    }
  }

  function evidenceLinks(c: Complaint): string[] {
    if (!c.evidenceUrls) return [];
    return c.evidenceUrls
      .split(/[,\s]+/)
      .map((s) => s.trim())
      .filter(Boolean);
  }

  function actionsFor(c: Complaint): ComplaintResolvedAction[] {
    const base: ComplaintResolvedAction[] = ["Refund", "Compensate", "IssueWarning", "Dismiss"];
    return c.targetType?.toLowerCase() === "show" ? [...base, "TakeDownContent"] : base;
  }

  return (
    <div className="bg-surface text-on-surface font-body-md min-h-screen flex">
      <AdminSidebar active="Khiếu nại" />

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
            <h1 className="font-headline-md text-headline-md text-on-surface mb-2">Khiếu nại</h1>
            <p className="font-body-md text-body-md text-on-surface-variant">
              {complaints === null ? "Đang tải..." : `${totalCount} khiếu nại chưa giải quyết`}
            </p>
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

          {complaints !== null && complaints.length === 0 && (
            <p className="font-body-md text-body-md text-on-surface-variant text-center py-24">
              Không có khiếu nại nào chưa giải quyết.
            </p>
          )}

          <div className="border-t border-outline-variant/20">
            {complaints?.map((c) => {
              const links = evidenceLinks(c);
              const isOpen = expandedId === c.id;
              return (
                <div
                  key={c.id}
                  ref={(el) => {
                    rowRefs.current[c.id] = el;
                  }}
                  className={`complaint-row border-b border-outline-variant/20 transition-colors ${
                    isOpen ? "bg-surface-container-low" : ""
                  }`}
                >
                  <div className="py-8 flex flex-col lg:flex-row lg:items-start justify-between gap-6">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-3 mb-2 flex-wrap">
                        <h3 className="font-headline-sm text-headline-sm text-on-surface">
                          KN-{String(c.id).padStart(4, "0")}
                        </h3>
                        <span className="bg-surface-container-high text-on-surface-variant font-label-md text-xs px-2.5 py-1 rounded-full">
                          {CATEGORY_LABELS[c.category] ?? c.category}
                        </span>
                        {c.status === "Investigating" && (
                          <span className="bg-secondary-container text-on-secondary-container font-label-md text-xs px-2.5 py-1 rounded-full">
                            Đang điều tra
                          </span>
                        )}
                      </div>

                      <p className="font-body-md text-body-md text-on-surface mb-3 whitespace-pre-line">{c.description}</p>

                      <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm font-body-md text-on-surface-variant">
                        <div>
                          <p className="flex items-center gap-2">
                            <User weight="light" size={16} />
                            {c.complainantName ?? "Khách vãng lai"}
                            {c.contactPhone && (
                              <span className="flex items-center gap-1.5 opacity-80">
                                <Phone weight="light" size={14} />
                                {c.contactPhone}
                              </span>
                            )}
                          </p>
                          <p className="flex items-center gap-2 mt-1">
                            <Target weight="light" size={16} />
                            Đối tượng: {c.targetType} #{c.targetId}
                          </p>
                        </div>
                        <div>
                          <p className="flex items-center gap-2">
                            <Clock weight="light" size={16} />
                            Gửi lúc: {formatRelativeTimeVi(c.createdAt)}
                          </p>
                          {links.length > 0 && (
                            <div className="flex flex-col gap-1 mt-1">
                              {links.map((url, i) => (
                                <a
                                  key={url}
                                  href={url}
                                  target="_blank"
                                  rel="noreferrer"
                                  className="flex items-center gap-2 text-primary hover:underline group"
                                >
                                  <LinkSimple
                                    weight="light"
                                    size={16}
                                    className="group-hover:-translate-y-0.5 transition-transform"
                                  />
                                  Bằng chứng {links.length > 1 ? i + 1 : ""}
                                </a>
                              ))}
                            </div>
                          )}
                        </div>
                      </div>
                    </div>

                    <div className="shrink-0">
                      <button
                        onClick={() => openDecision(c)}
                        disabled={busyId === c.id}
                        className="px-6 py-2.5 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50"
                      >
                        {isOpen ? "Đang xử lý..." : "Xử lý"}
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
                          <span className="block font-label-md text-label-md text-on-surface mb-2">Kết luận</span>
                          <div className="flex flex-wrap gap-2">
                            {(
                              [
                                ["Resolved", "Đã giải quyết"],
                                ["Investigating", "Đang điều tra"],
                                ["Rejected", "Bác bỏ khiếu nại"],
                              ] as const
                            ).map(([value, label]) => (
                              <button
                                key={value}
                                type="button"
                                onClick={() => {
                                  setStatus(value);
                                  if (value !== "Resolved") setResolvedAction("");
                                }}
                                className={`px-4 py-2 rounded-full border font-label-md text-label-md transition-colors ${
                                  status === value
                                    ? "border-primary bg-primary text-on-primary"
                                    : "border-outline-variant bg-surface text-on-surface-variant hover:border-primary hover:text-primary"
                                }`}
                              >
                                {label}
                              </button>
                            ))}
                          </div>
                        </div>

                        {status === "Resolved" && (
                          <div>
                            <span className="block font-label-md text-label-md text-on-surface mb-2">
                              Hướng xử lý <span className="text-error">*</span>
                            </span>
                            <div className="flex flex-wrap gap-2">
                              {actionsFor(c).map((a) => (
                                <button
                                  key={a}
                                  type="button"
                                  onClick={() => {
                                    setResolvedAction(a);
                                    if (a !== "Compensate" && a !== "Refund") setRefundAmount("");
                                  }}
                                  className={`px-4 py-2 rounded-full border font-label-md text-label-md transition-colors ${
                                    resolvedAction === a
                                      ? "border-primary bg-primary text-on-primary"
                                      : "border-outline-variant bg-surface text-on-surface-variant hover:border-primary hover:text-primary"
                                  }`}
                                >
                                  {ACTION_LABELS[a]}
                                </button>
                              ))}
                            </div>
                            {resolvedAction === "TakeDownContent" && (
                              <p className="text-[12px] text-on-surface-variant mt-2">
                                Gỡ show sẽ hủy toàn bộ vé đã xác nhận và hoàn 100% cho người mua.
                              </p>
                            )}
                          </div>
                        )}

                        {(resolvedAction === "Compensate" || resolvedAction === "Refund") && status === "Resolved" && (
                          <div>
                            <label
                              className="block font-label-md text-label-md text-on-surface mb-2"
                              htmlFor={`refund-amount-${c.id}`}
                            >
                              Số tiền (VNĐ){" "}
                              {resolvedAction === "Compensate" && <span className="text-error">*</span>}
                            </label>
                            <input
                              id={`refund-amount-${c.id}`}
                              type="number"
                              min={1}
                              step={1000}
                              value={refundAmount}
                              onChange={(e) => setRefundAmount(e.target.value)}
                              placeholder={resolvedAction === "Refund" ? "Để trống = hoàn toàn bộ giá vé" : "0"}
                              className="w-full max-w-xs bg-surface border border-outline-variant rounded-lg p-3 text-on-surface focus:ring-2 focus:ring-primary focus:border-primary transition-all font-body-md"
                            />
                          </div>
                        )}

                        <div>
                          <label
                            className="block font-label-md text-label-md text-on-surface mb-2"
                            htmlFor={`resolution-${c.id}`}
                          >
                            Nội dung phản hồi
                          </label>
                          <textarea
                            id={`resolution-${c.id}`}
                            rows={3}
                            maxLength={RESOLUTION_MAX}
                            value={resolution}
                            onChange={(e) => setResolution(e.target.value)}
                            placeholder="Giải thích quyết định cho người khiếu nại..."
                            className="w-full bg-surface border border-outline-variant rounded-lg p-3 text-on-surface focus:ring-2 focus:ring-primary focus:border-primary transition-all font-body-md"
                          />
                          <p className="text-[12px] text-on-surface-variant mt-1 text-right">
                            {resolution.length}/{RESOLUTION_MAX}
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
                            onClick={() => handleSubmit(c)}
                            disabled={busyId === c.id}
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
