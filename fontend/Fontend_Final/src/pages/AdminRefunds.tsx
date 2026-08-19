import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { animate, stagger } from "animejs";
import { ArrowsLeftRight, Bell, Clock, Receipt, User, Warning } from "@phosphor-icons/react";
import { ApiError, getStoredToken, getStoredUser } from "../api/client";
import { getPendingRefundRequests, processRefundRequest, RefundRequest } from "../api/admin";
import { formatRelativeTimeVi } from "../lib/relativeTime";
import { formatVnd } from "../lib/currency";
import AdminSidebar from "../components/AdminSidebar";

// Ported 2026-08-18 from the `admin_refund_approval_queue` screen in the AuraLounge Stitch export
// (fontend/Fontend_Final/stitch/Views/...zip), restyled onto this project's own tokens -- that
// library ships a different design system (Libre Caslon + Manrope, "AuraLounge" branding, USD) so
// only its layout and information architecture were reused, never its visual language.
//
// Three things in the mock were dropped rather than faked, because RefundRequestDto cannot back
// them (same discipline as AdminPendingVenues dropping Stitch's "Mới" badge):
//   * "Filter By: All Pending / Buyer Request / Owner Cancel / Admin Complaint" -- there is no
//     origin/category field on the DTO, and GetPendingRefundRequestsQueryHandler hard-filters to
//     Status == Pending, so every row is already "All Pending". Nothing to filter on either axis.
//   * "User / Event" column -- the DTO exposes `requestedBy` and `paymentId` as bare ints, with no
//     name or event title. Rendered as explicit reference ids instead of inventing labels.
//   * "Original Amt" column -- the original payment's gross is not on this DTO at all. The approve
//     panel still guards the ceiling, but server-side: the handler throws DomainException (-> 422)
//     if the approved amount exceeds payment.GrossAmount, and that message is surfaced verbatim.
//
// Kept from the mock: the inline editable approved-amount on a row (its most useful idea -- partial
// refunds are the common case, and forcing a separate detail page for a number edit is friction),
// and the paginated footer.
//
// Motion follows AdminPendingVenues exactly: staggered entrance, fade-out when a row leaves the
// queue. Both are real state changes; nothing else is animated.

export default function AdminRefunds() {
  const navigate = useNavigate();
  const [requests, setRequests] = useState<RefundRequest[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [approvedAmount, setApprovedAmount] = useState("");
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
    setRequests(null);
    getPendingRefundRequests(page, pageSize)
      .then((result) => {
        if (cancelled) return;
        setRequests(result.items);
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
    if (requests && requests.length > 0 && !animatedPages.current.has(page)) {
      animatedPages.current.add(page);
      animate(".refund-row", {
        opacity: [0, 1],
        translateY: [16, 0],
        delay: stagger(80),
        duration: 500,
        ease: "outQuad",
      });
    }
  }, [requests, page]);

  function fadeOutThenRemove(id: number, after: () => void) {
    const el = rowRefs.current[id];
    if (!el) {
      after();
      return;
    }
    animate(el, {
      opacity: [1, 0],
      scale: [1, 0.98],
      translateY: [0, -8],
      duration: 350,
      ease: "inQuad",
      onComplete: after,
    });
  }

  function toggleApprove(r: RefundRequest) {
    setExpandedId((cur) => (cur === r.id ? null : r.id));
    // Pre-fill with the full requested amount -- approving in full is the common case, so the
    // Admin edits down to a partial figure rather than typing the whole number every time.
    setApprovedAmount(String(Math.round(r.amountRequested)));
    setActionError(null);
  }

  function removeResolved(id: number) {
    fadeOutThenRemove(id, () => {
      setRequests((prev) => prev?.filter((r) => r.id !== id) ?? prev);
      setTotalCount((c) => Math.max(0, c - 1));
      setExpandedId(null);
    });
  }

  async function handleConfirmApprove(r: RefundRequest) {
    const parsed = Number(approvedAmount);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setActionError("Số tiền hoàn phải là một số lớn hơn 0.");
      return;
    }
    setBusyId(r.id);
    setActionError(null);
    try {
      // Send null when the figure is unchanged so the server applies its own default rather than
      // us round-tripping a value it already knows.
      const unchanged = parsed === Math.round(r.amountRequested);
      await processRefundRequest(r.id, "Approved", unchanged ? null : parsed);
      removeResolved(r.id);
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyId(null);
    }
  }

  async function handleReject(id: number) {
    setBusyId(id);
    setActionError(null);
    try {
      await processRefundRequest(id, "Rejected");
      removeResolved(id);
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="bg-surface text-on-surface font-body-md min-h-screen flex">
      <AdminSidebar active="Hoàn tiền" />

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
            <h1 className="font-headline-md text-headline-md text-on-surface mb-2">Yêu cầu hoàn tiền</h1>
            <p className="font-body-md text-body-md text-on-surface-variant">
              {requests === null ? "Đang tải..." : `${totalCount} yêu cầu đang chờ xử lý`}
            </p>
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
          {actionError && (
            <div
              role="alert"
              aria-live="assertive"
              className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 mb-6"
            >
              {actionError}
            </div>
          )}

          {requests !== null && requests.length === 0 && (
            <p className="font-body-md text-body-md text-on-surface-variant text-center py-24">
              Không có yêu cầu hoàn tiền nào đang chờ xử lý.
            </p>
          )}

          <div className="border-t border-outline-variant/20">
            {requests?.map((r) => (
              <div
                key={r.id}
                ref={(el) => {
                  rowRefs.current[r.id] = el;
                }}
                className={`refund-row border-b border-outline-variant/20 transition-colors ${
                  expandedId === r.id ? "bg-surface-container-low" : ""
                }`}
              >
                <div className="py-8 flex flex-col lg:flex-row lg:items-center justify-between gap-6">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-3 mb-2 flex-wrap">
                      <h3 className="font-headline-sm text-headline-sm text-on-surface">
                        RF-{String(r.id).padStart(4, "0")}
                      </h3>
                      {r.requiresManualTransfer && (
                        <span className="inline-flex items-center gap-1.5 bg-secondary-container text-on-secondary-container font-label-md text-xs px-2.5 py-1 rounded-full">
                          <ArrowsLeftRight weight="bold" size={12} />
                          Cần chuyển khoản thủ công
                        </span>
                      )}
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm font-body-md text-on-surface-variant">
                      <div>
                        <p className="flex items-center gap-2">
                          <Receipt weight="light" size={16} />
                          Thanh toán #{r.paymentId}
                          {r.requestedBy !== null && <span className="opacity-70">· người yêu cầu #{r.requestedBy}</span>}
                        </p>
                        <p className="flex items-center gap-2 mt-1">
                          <Clock weight="light" size={16} />
                          Gửi lúc: {formatRelativeTimeVi(r.createdAt)}
                        </p>
                      </div>
                      <div>
                        <p className="flex items-start gap-2">
                          <Warning weight="light" size={16} className="shrink-0 mt-0.5" />
                          <span className="min-w-0">{r.reason}</span>
                        </p>
                      </div>
                    </div>
                  </div>

                  <div className="flex items-center gap-6 shrink-0">
                    <div className="text-right">
                      <p className="font-label-md text-xs uppercase tracking-wider text-on-surface-variant mb-1">
                        Số tiền yêu cầu
                      </p>
                      <p className="font-headline-sm text-headline-sm text-on-surface">{formatVnd(r.amountRequested)}</p>
                    </div>
                    <div className="flex items-center gap-3">
                      <button
                        onClick={() => handleReject(r.id)}
                        disabled={busyId === r.id || expandedId === r.id}
                        className="px-6 py-2.5 rounded-lg border border-outline text-on-surface font-label-md text-label-md hover:bg-surface-variant transition-colors disabled:opacity-50"
                      >
                        Từ chối
                      </button>
                      <button
                        onClick={() => toggleApprove(r)}
                        disabled={busyId === r.id}
                        className="px-6 py-2.5 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {expandedId === r.id ? "Đang duyệt..." : "Duyệt"}
                      </button>
                    </div>
                  </div>
                </div>

                {/* Approve panel -- plain CSS grid-rows expand, same as the reject panel in
                    AdminPendingVenues; no JS sequencing needed for a show/hide. */}
                <div
                  className={`grid transition-[grid-template-rows] duration-300 ease-out ${
                    expandedId === r.id ? "grid-rows-[1fr]" : "grid-rows-[0fr]"
                  }`}
                >
                  <div className="overflow-hidden">
                    <div className="bg-surface-container p-md border-t border-outline-variant/20">
                      <label
                        className="block font-label-md text-label-md text-on-surface mb-2"
                        htmlFor={`approved-amount-${r.id}`}
                      >
                        Số tiền hoàn (VNĐ)
                      </label>
                      <input
                        id={`approved-amount-${r.id}`}
                        type="number"
                        min={1}
                        step={1000}
                        className="w-full max-w-xs bg-surface border border-outline-variant rounded-lg p-3 text-on-surface focus:ring-2 focus:ring-primary focus:border-primary transition-all font-body-md"
                        value={expandedId === r.id ? approvedAmount : ""}
                        onChange={(e) => setApprovedAmount(e.target.value)}
                      />
                      <p className="text-[12px] text-on-surface-variant mt-2">
                        Để nguyên để hoàn đúng số tiền đã yêu cầu, hoặc sửa xuống nếu hoàn một phần.
                      </p>
                      <div className="flex justify-end gap-3 mt-4">
                        <button
                          onClick={() => setExpandedId(null)}
                          className="px-4 py-2 rounded-lg text-on-surface-variant hover:bg-surface-variant transition-colors font-label-md text-label-md"
                        >
                          Hủy
                        </button>
                        <button
                          onClick={() => handleConfirmApprove(r)}
                          disabled={busyId === r.id}
                          className="px-6 py-2 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50"
                        >
                          Xác nhận hoàn tiền
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            ))}
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
