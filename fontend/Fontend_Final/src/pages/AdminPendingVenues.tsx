import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { animate, stagger } from "animejs";
import { Bell, Clock, FileText, MapPin, User } from "@phosphor-icons/react";
import { ApiError, getStoredToken, getStoredUser } from "../api/client";
import { approveLounge, getPendingLounges, PendingLounge, rejectLounge } from "../api/admin";
import { formatRelativeTimeVi } from "../lib/relativeTime";
import AdminSidebar from "../components/AdminSidebar";

// Ported from a Stitch MCP generation (docs/stitch/Stitch-Master-Brief.md "Pending Venues", prompted with
// the real PendingLoungeDto fields + the "Design Execution Principles" luxury-restraint checklist
// added the same day) -- first Admin screen built, so <AdminSidebar> is extracted here rather than
// copied later, same reasoning as <OwnerHeader>. Dropped Stitch's "Mới" (New) badge on the mock's
// first entry -- PendingLoungeDto has no such flag, nothing to honestly render there.
//
// Motion (user's explicit anime.js request, same day): a staggered card entrance on load and a
// fade-out confirmation when a venue leaves the queue (approved or rejected) -- both are genuine
// state changes worth a purposeful moment. The reject-reason panel's expand/collapse stays a plain
// CSS grid-template-rows transition (no JS sequencing needed for a simple show/hide), per the same
// principle's guidance not to route every micro-interaction through JS.

function formatAddress(v: PendingLounge): string {
  return [v.street, v.ward, v.district, v.city].filter(Boolean).join(", ");
}

export default function AdminPendingVenues() {
  const navigate = useNavigate();
  const [venues, setVenues] = useState<PendingLounge[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [rejectReason, setRejectReason] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);
  const cardRefs = useRef<Record<number, HTMLDivElement | null>>({});
  const hasAnimatedEntrance = useRef(false);

  useEffect(() => {
    const token = getStoredToken();
    const user = getStoredUser();
    if (!token || user?.role !== "Admin") {
      navigate("/login");
      return;
    }
    let cancelled = false;
    getPendingLounges()
      .then((result) => {
        if (!cancelled) setVenues(result.items);
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
    if (venues && venues.length > 0 && !hasAnimatedEntrance.current) {
      hasAnimatedEntrance.current = true;
      animate(".pending-venue-card", {
        opacity: [0, 1],
        translateY: [16, 0],
        delay: stagger(80),
        duration: 500,
        ease: "outQuad",
      });
    }
  }, [venues]);

  function fadeOutThenRemove(id: number, after: () => void) {
    const el = cardRefs.current[id];
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

  async function handleApprove(id: number) {
    setBusyId(id);
    setActionError(null);
    try {
      await approveLounge(id);
      fadeOutThenRemove(id, () => setVenues((prev) => prev?.filter((v) => v.id !== id) ?? prev));
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyId(null);
    }
  }

  function toggleReject(id: number) {
    setExpandedId((cur) => (cur === id ? null : id));
    setRejectReason("");
    setActionError(null);
  }

  async function handleConfirmReject(id: number) {
    if (!rejectReason.trim()) {
      setActionError("Vui lòng nhập lý do từ chối.");
      return;
    }
    setBusyId(id);
    setActionError(null);
    try {
      await rejectLounge(id, rejectReason.trim());
      fadeOutThenRemove(id, () => {
        setVenues((prev) => prev?.filter((v) => v.id !== id) ?? prev);
        setExpandedId(null);
      });
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="bg-surface text-on-surface font-body-md min-h-screen flex">
      <AdminSidebar active="Phòng trà" />

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
            <h1 className="font-headline-md text-headline-md text-on-surface mb-2">Phòng trà chờ duyệt</h1>
            <p className="font-body-md text-body-md text-on-surface-variant">
              {venues === null ? "Đang tải..." : `${venues.length} phòng trà đang chờ xét duyệt`}
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

          {venues !== null && venues.length === 0 && (
            <p className="font-body-md text-body-md text-on-surface-variant text-center py-24">
              Không có phòng trà nào đang chờ duyệt.
            </p>
          )}

          {/* Editorial list, not a repeated card-per-row -- DESIGN.md's own "Lists: Clean,
              high-contrast rows with subtle dividers" spec, applied for real 2026-08-17 instead
              of the identical bordered-card-per-row pattern every AI-generated queue defaults to. */}
          <div className="border-t border-outline-variant/20">
            {venues?.map((v) => (
              <div
                key={v.id}
                ref={(el) => {
                  cardRefs.current[v.id] = el;
                }}
                className={`pending-venue-card border-b border-outline-variant/20 transition-colors ${
                  expandedId === v.id ? "bg-surface-container-low" : ""
                }`}
              >
                <div className="py-8 flex flex-col lg:flex-row lg:items-center justify-between gap-6">
                  <div className="flex-1">
                    <h3 className="font-headline-sm text-headline-sm text-on-surface mb-2">{v.name}</h3>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm font-body-md text-on-surface-variant">
                      <div>
                        <p className="flex items-center gap-2">
                          <User weight="light" size={16} />
                          {v.ownerName} ({v.ownerEmail})
                        </p>
                        <p className="flex items-center gap-2 mt-1">
                          <MapPin weight="light" size={16} />
                          {formatAddress(v)}
                        </p>
                      </div>
                      <div>
                        <p className="flex items-center gap-2">
                          <Clock weight="light" size={16} />
                          Nộp lúc: {formatRelativeTimeVi(v.createdAt)}
                        </p>
                        {v.businessLicenseUrl ? (
                          <a
                            className="flex items-center gap-2 mt-1 text-primary hover:underline group"
                            href={v.businessLicenseUrl}
                            target="_blank"
                            rel="noreferrer"
                          >
                            <FileText weight="light" size={16} className="group-hover:-translate-y-0.5 transition-transform" />
                            Xem giấy phép kinh doanh
                          </a>
                        ) : (
                          <p className="flex items-center gap-2 mt-1 text-on-surface-variant/70 italic">
                            <FileText weight="light" size={16} />
                            Chưa nộp giấy phép kinh doanh
                          </p>
                        )}
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 shrink-0">
                    <button
                      onClick={() => toggleReject(v.id)}
                      disabled={busyId === v.id}
                      className="px-6 py-2.5 rounded-lg border border-outline text-on-surface font-label-md text-label-md hover:bg-surface-variant transition-colors disabled:opacity-50"
                    >
                      {expandedId === v.id ? "Đang từ chối..." : "Từ chối"}
                    </button>
                    <button
                      onClick={() => handleApprove(v.id)}
                      disabled={busyId === v.id || expandedId === v.id}
                      className="px-6 py-2.5 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      Duyệt
                    </button>
                  </div>
                </div>

                {/* Reject reason panel -- plain CSS grid-rows expand, no JS sequencing needed. */}
                <div
                  className={`grid transition-[grid-template-rows] duration-300 ease-out ${
                    expandedId === v.id ? "grid-rows-[1fr]" : "grid-rows-[0fr]"
                  }`}
                >
                  <div className="overflow-hidden">
                    <div className="bg-surface-container p-md border-t border-outline-variant/20">
                      <label className="block font-label-md text-label-md text-on-surface mb-2" htmlFor={`reject-reason-${v.id}`}>
                        Lý do từ chối
                      </label>
                      <textarea
                        id={`reject-reason-${v.id}`}
                        rows={3}
                        placeholder="Lý do từ chối (bắt buộc)..."
                        className="w-full bg-surface border border-outline-variant rounded-lg p-3 text-on-surface focus:ring-2 focus:ring-primary focus:border-primary transition-all font-body-md"
                        value={expandedId === v.id ? rejectReason : ""}
                        onChange={(e) => setRejectReason(e.target.value)}
                      />
                      <div className="flex justify-end gap-3 mt-4">
                        <button
                          onClick={() => toggleReject(v.id)}
                          className="px-4 py-2 rounded-lg text-on-surface-variant hover:bg-surface-variant transition-colors font-label-md text-label-md"
                        >
                          Hủy
                        </button>
                        <button
                          onClick={() => handleConfirmReject(v.id)}
                          disabled={busyId === v.id}
                          className="px-6 py-2 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50"
                        >
                          Xác nhận từ chối
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </main>
    </div>
  );
}
