import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { animate, stagger } from "animejs";
import { Bank, Bell, Clock, FileText, Storefront, User, WarningCircle } from "@phosphor-icons/react";
import { ApiError, getStoredToken, getStoredUser } from "../api/client";
import { getPendingBankAccounts, PendingBankAccount, verifyBankAccount } from "../api/admin";
import { formatRelativeTimeVi } from "../lib/relativeTime";
import AdminSidebar from "../components/AdminSidebar";

// Ported 2026-08-18 from `admin_bank_account_verification_hub` / `_detail` in the AuraLounge Stitch
// export, restyled onto this project's tokens and merged into one screen (the mock's hub + detail
// split would make an Admin navigate away just to read an account number).
//
// This screen exists because verification is NOT cosmetic: SettlementSchedulingService refuses to
// schedule a payout while a venue's default account is unverified, so every row here is a venue
// whose money is currently stuck. Verifying a default account also triggers
// RetryBlockedForLoungeAsync, releasing the payments that were skipped. The confirm step spells that
// out rather than presenting "Xác minh" as a neutral toggle.
//
// Performer accounts are absent by design — the platform never transfers to one. That is enforced
// server-side in GetPendingBankAccountsQueryHandler, not by a filter here.
//
// The account number is decrypted PII. It is rendered for eyeball comparison against the business
// licence (the actual verification method — there is no automated bank API), and deliberately not
// put in any URL, log, or animation payload.

export default function AdminBankAccounts() {
  const navigate = useNavigate();
  const [accounts, setAccounts] = useState<PendingBankAccount[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [confirmingId, setConfirmingId] = useState<number | null>(null);
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
    setAccounts(null);
    getPendingBankAccounts(page, pageSize)
      .then((result) => {
        if (cancelled) return;
        setAccounts(result.items);
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
    if (accounts && accounts.length > 0 && !animatedPages.current.has(page)) {
      animatedPages.current.add(page);
      animate(".bank-row", {
        opacity: [0, 1],
        translateY: [16, 0],
        delay: stagger(80),
        duration: 500,
        ease: "outQuad",
      });
    }
  }, [accounts, page]);

  async function handleVerify(a: PendingBankAccount) {
    setBusyId(a.id);
    setActionError(null);
    try {
      await verifyBankAccount(a.id);
      const el = rowRefs.current[a.id];
      const remove = () => {
        setAccounts((prev) => prev?.filter((x) => x.id !== a.id) ?? prev);
        setTotalCount((n) => Math.max(0, n - 1));
        setConfirmingId(null);
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
      <AdminSidebar active="Xác minh tài khoản ngân hàng" />

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
            <h1 className="font-headline-md text-headline-md text-on-surface mb-2">Xác minh tài khoản ngân hàng</h1>
            <p className="font-body-md text-body-md text-on-surface-variant">
              {accounts === null ? "Đang tải..." : `${totalCount} tài khoản phòng trà đang chờ xác minh`}
            </p>
          </div>

          {/* Not decoration: an unverified default account is a hard block on that venue's payouts. */}
          <div className="rounded-xl bg-secondary-container text-on-secondary-container text-body-md font-body-md px-4 py-3 mb-lg flex items-start gap-3">
            <WarningCircle weight="fill" size={18} className="shrink-0 mt-0.5" />
            <span>
              Chưa xác minh nghĩa là <strong>tiền của phòng trà đang bị treo</strong> — hệ thống từ chối lên lịch
              thanh toán cho tới khi tài khoản mặc định được xác minh. Đối chiếu tên chủ tài khoản với giấy phép
              kinh doanh trước khi xác nhận.
            </span>
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

          {accounts !== null && accounts.length === 0 && (
            <p className="font-body-md text-body-md text-on-surface-variant text-center py-24">
              Không có tài khoản nào đang chờ xác minh.
            </p>
          )}

          <div className="border-t border-outline-variant/20">
            {accounts?.map((a) => (
              <div
                key={a.id}
                ref={(el) => {
                  rowRefs.current[a.id] = el;
                }}
                className={`bank-row border-b border-outline-variant/20 transition-colors ${
                  confirmingId === a.id ? "bg-surface-container-low" : ""
                }`}
              >
                <div className="py-8 flex flex-col lg:flex-row lg:items-start justify-between gap-6">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-3 mb-3 flex-wrap">
                      <Storefront weight="light" size={20} className="text-on-surface-variant shrink-0" />
                      <h3 className="font-headline-sm text-headline-sm text-on-surface">{a.loungeName}</h3>
                      {a.isDefault && (
                        <span className="bg-primary-container text-on-primary-container font-label-md text-xs px-2.5 py-1 rounded-full">
                          Tài khoản mặc định
                        </span>
                      )}
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm font-body-md text-on-surface-variant">
                      <div>
                        <p className="flex items-center gap-2">
                          <User weight="light" size={16} />
                          {a.ownerName} ({a.ownerEmail})
                        </p>
                        <p className="flex items-center gap-2 mt-1">
                          <Clock weight="light" size={16} />
                          Đăng ký: {formatRelativeTimeVi(a.createdAt)}
                        </p>
                        {a.businessLicenseUrl ? (
                          <a
                            className="flex items-center gap-2 mt-1 text-primary hover:underline group"
                            href={a.businessLicenseUrl}
                            target="_blank"
                            rel="noreferrer"
                          >
                            <FileText
                              weight="light"
                              size={16}
                              className="group-hover:-translate-y-0.5 transition-transform"
                            />
                            Xem giấy phép kinh doanh để đối chiếu
                          </a>
                        ) : (
                          <p className="flex items-center gap-2 mt-1 text-on-surface-variant/70 italic">
                            <FileText weight="light" size={16} />
                            Phòng trà chưa nộp giấy phép kinh doanh
                          </p>
                        )}
                      </div>

                      {/* The comparison target. Grouped and monospaced so an Admin can read the
                          account number off against the licence without transcription errors. */}
                      <div className="bg-surface-container-low border border-outline-variant/40 rounded-xl p-4">
                        <p className="flex items-center gap-2 font-label-md text-xs uppercase tracking-wider text-on-surface-variant mb-2">
                          <Bank weight="light" size={15} />
                          Thông tin tài khoản
                        </p>
                        <p className="text-on-surface font-body-md">{a.bankName}</p>
                        <p className="text-on-surface font-mono text-body-lg tracking-wider my-1">{a.accountNumber}</p>
                        <p className="text-on-surface-variant">{a.accountHolder}</p>
                      </div>
                    </div>
                  </div>

                  <div className="shrink-0">
                    <button
                      onClick={() => setConfirmingId((cur) => (cur === a.id ? null : a.id))}
                      disabled={busyId === a.id}
                      className="px-6 py-2.5 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50"
                    >
                      {confirmingId === a.id ? "Đang xác minh..." : "Xác minh"}
                    </button>
                  </div>
                </div>

                {/* Confirm step: verifying is irreversible (there is no un-verify endpoint) and
                    releases blocked payouts, so it gets an explicit second look rather than firing
                    on the first click. */}
                <div
                  className={`grid transition-[grid-template-rows] duration-300 ease-out ${
                    confirmingId === a.id ? "grid-rows-[1fr]" : "grid-rows-[0fr]"
                  }`}
                >
                  <div className="overflow-hidden">
                    <div className="bg-surface-container p-md border-t border-outline-variant/20">
                      <p className="font-body-md text-body-md text-on-surface mb-1">
                        Xác nhận tài khoản <strong>{a.accountHolder}</strong> đúng là của phòng trà{" "}
                        <strong>{a.loungeName}</strong>?
                      </p>
                      <p className="text-[12px] text-on-surface-variant">
                        {a.isDefault
                          ? "Đây là tài khoản mặc định — xác minh sẽ tự động chạy lại các khoản thanh toán đang bị treo của phòng trà này."
                          : "Không phải tài khoản mặc định nên sẽ không giải phóng khoản thanh toán nào."}{" "}
                        Thao tác này không thể hoàn tác.
                      </p>
                      <div className="flex justify-end gap-3 mt-4">
                        <button
                          onClick={() => setConfirmingId(null)}
                          className="px-4 py-2 rounded-lg text-on-surface-variant hover:bg-surface-variant transition-colors font-label-md text-label-md"
                        >
                          Hủy
                        </button>
                        <button
                          onClick={() => handleVerify(a)}
                          disabled={busyId === a.id}
                          className="px-6 py-2 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50"
                        >
                          Xác nhận đã đối chiếu
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
