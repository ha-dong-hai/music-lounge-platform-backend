import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Check, Lock } from "@phosphor-icons/react";
import { ApiError, getStoredToken } from "../api/client";
import {
  cancelSubscription,
  getMySubscription,
  getSubscriptionPackages,
  MySubscription,
  renewSubscription,
  SubscriptionPackage,
  subscribeToPackage,
} from "../api/subscriptions";
import OwnerHeader from "../components/OwnerHeader";

// Ported from a Stitch MCP generation (docs/stitch/Stitch-Master-Brief.md "Subscription Plans" + "My
// Subscription"), wired to the real GET /subscriptions/packages, GET /subscriptions/my,
// POST /subscriptions/subscribe|renew|cancel. Two real gaps closed before this could even be built:
// (1) subscription_packages was empty in the dev DB -- Admin-only creation, nothing for an Owner to
// actually subscribe to -- seeded 3 real tiers via a proper EF migration (SeedSubscriptionPackages),
// not a raw DB hack. (2) Subscribe/Renew return a real VNPay paymentUrl the browser must navigate to,
// and VNPay redirects back to a literal /payment/success or /payment/failed route on this frontend
// (BusinessSettings:PaymentSuccessUrl/PaymentFailedUrl) -- that page didn't exist yet either, see
// PaymentResult.tsx.
//
// Dropped from the Stitch mock: the floating "Phổ biến nhất" pill badge (asked for a restrained
// thin top-border accent instead, per Design Execution Principles -- Stitch added the badge anyway)
// and a page footer (not an established pattern on any other ported Owner screen, and its "©2024"
// was simply wrong). "Recommended" tier is computed as the median-priced active package, not
// hardcoded by name, so this stays correct if Admin adds/removes/reprices packages later.

function formatVnd(amount: number): string {
  return `${Math.round(amount).toLocaleString("vi-VN")}đ`;
}

function formatDateVi(iso: string): string {
  return new Date(iso).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
}

const STATUS_LABEL: Record<string, string> = {
  Active: "Đang hoạt động",
  Expired: "Đã hết hạn",
  Cancelled: "Đã huỷ",
};

function PlanCard({
  pkg,
  recommended,
  busy,
  onChoose,
}: {
  pkg: SubscriptionPackage;
  recommended: boolean;
  busy: boolean;
  onChoose: () => void;
}) {
  return (
    <div
      className={`bg-surface-container-lowest rounded-xl p-8 flex flex-col shadow-[0_4px_8px_rgba(43,42,39,0.08)] ${
        recommended ? "border-t-4 border-primary md:scale-105 z-10" : ""
      }`}
    >
      <h3 className="font-headline-sm text-headline-sm text-on-surface mb-2">{pkg.name}</h3>
      <div className="mb-6">
        <span className={`font-title-lg text-title-lg ${recommended ? "text-primary" : "text-on-surface"}`}>
          {formatVnd(pkg.price)}
        </span>
        <span className="font-body-md text-body-md text-on-surface-variant"> /{pkg.billingCycle === "Monthly" ? "tháng" : pkg.billingCycle === "Quarterly" ? "quý" : "năm"}</span>
      </div>
      <ul className="flex flex-col gap-4 mb-8 flex-grow">
        <li className="flex items-start gap-3">
          <Check weight="bold" size={18} className={recommended ? "text-primary" : "text-primary"} />
          <span className="font-body-md text-body-md text-on-surface">
            Tối đa {pkg.maxTicketsPerEvent.toLocaleString("vi-VN")} vé mỗi show
          </span>
        </li>
        <li className={`flex items-start gap-3 ${!pkg.hasAiPoster ? "text-on-surface-variant/60" : ""}`}>
          {pkg.hasAiPoster ? (
            <Check weight="bold" size={18} className="text-primary" />
          ) : (
            <Lock weight="light" size={18} />
          )}
          <span className="font-body-md text-body-md">
            {pkg.hasAiPoster ? `${pkg.maxAiPostersPerMonth} poster AI mỗi tháng` : "Poster AI"}
          </span>
        </li>
        <li className={`flex items-start gap-3 ${pkg.maxTourScenes === 0 ? "text-on-surface-variant/60" : ""}`}>
          {pkg.maxTourScenes > 0 ? (
            <Check weight="bold" size={18} className="text-primary" />
          ) : (
            <Lock weight="light" size={18} />
          )}
          <span className="font-body-md text-body-md">
            {pkg.maxTourScenes > 0 ? `${pkg.maxTourScenes} cảnh tour ảo 360°` : "Tour ảo 360°"}
          </span>
        </li>
      </ul>
      <button
        onClick={onChoose}
        disabled={busy}
        className={
          recommended
            ? "w-full py-3 rounded-xl bg-primary text-on-primary hover:bg-primary-container transition-colors font-label-md text-label-md shadow-sm disabled:opacity-50"
            : "w-full py-3 rounded-xl border border-outline text-on-surface hover:bg-surface-container-low transition-colors font-label-md text-label-md disabled:opacity-50"
        }
      >
        {busy ? "Đang chuyển đến VNPay..." : "Chọn gói"}
      </button>
    </div>
  );
}

export default function OwnerSubscription() {
  const navigate = useNavigate();
  const [packages, setPackages] = useState<SubscriptionPackage[] | null>(null);
  const [mySubscription, setMySubscription] = useState<MySubscription | null | undefined>(undefined);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [confirmingCancel, setConfirmingCancel] = useState(false);

  useEffect(() => {
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }
    let cancelled = false;

    Promise.all([getSubscriptionPackages(), getMySubscription()])
      .then(([pkgs, mine]) => {
        if (cancelled) return;
        setPackages(pkgs);
        setMySubscription(mine);
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });

    return () => {
      cancelled = true;
    };
  }, [navigate]);

  async function handleChoose(pkg: SubscriptionPackage) {
    setBusyAction(`subscribe-${pkg.id}`);
    setErrorMessage(null);
    try {
      const result = await subscribeToPackage(pkg.id);
      window.location.href = result.paymentUrl;
    } catch (err) {
      setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      setBusyAction(null);
    }
  }

  async function handleRenew() {
    setBusyAction("renew");
    setErrorMessage(null);
    try {
      const result = await renewSubscription();
      window.location.href = result.paymentUrl;
    } catch (err) {
      setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      setBusyAction(null);
    }
  }

  async function handleCancel() {
    setBusyAction("cancel");
    setErrorMessage(null);
    try {
      await cancelSubscription();
      setMySubscription(null);
      setConfirmingCancel(false);
    } catch (err) {
      setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyAction(null);
    }
  }

  const recommendedId = (() => {
    if (!packages || packages.length === 0) return null;
    const sorted = [...packages].sort((a, b) => a.price - b.price);
    return sorted[Math.floor((sorted.length - 1) / 2)].id;
  })();

  return (
    <div className="bg-surface text-on-surface font-body-lg min-h-screen flex flex-col">
      <OwnerHeader active="Settings" />

      <main className="flex-grow w-full max-w-container-max mx-auto px-margin-mobile md:px-lg py-xl flex flex-col items-center">
        {errorMessage && (
          <div
            role="alert"
            aria-live="assertive"
            className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 mb-8 w-full max-w-2xl text-center"
          >
            {errorMessage}
          </div>
        )}

        {mySubscription === undefined && packages === null && (
          <p className="font-body-md text-body-md text-on-surface-variant py-24">Đang tải...</p>
        )}

        {mySubscription && (
          <div className="w-full max-w-2xl">
            <div className="text-center mb-12">
              <span className="text-label-md font-label-md text-secondary tracking-widest uppercase">
                Gói dịch vụ
              </span>
              <h1 className="font-display-lg text-display-lg-mobile md:text-display-lg text-on-surface mt-3">
                {mySubscription.packageName}
              </h1>
            </div>
            <div className="bg-surface-container-lowest rounded-xl p-8 shadow-[0_4px_8px_rgba(43,42,39,0.08)]">
              <div className="flex items-center justify-between pb-6 mb-6 border-b border-outline-variant/20">
                <span className="font-label-md text-label-md text-on-surface-variant uppercase tracking-wide">
                  Trạng thái
                </span>
                <span
                  className={`font-label-md text-label-md font-semibold ${
                    mySubscription.status === "Active" ? "text-tertiary" : "text-error"
                  }`}
                >
                  {STATUS_LABEL[mySubscription.status] ?? mySubscription.status}
                </span>
              </div>
              <div className="grid grid-cols-2 gap-6 mb-8">
                <div>
                  <p className="font-label-md text-[10px] text-on-surface-variant uppercase tracking-[0.2em] mb-1">
                    Bắt đầu
                  </p>
                  <p className="font-body-md text-on-surface">{formatDateVi(mySubscription.startedAt)}</p>
                </div>
                <div>
                  <p className="font-label-md text-[10px] text-on-surface-variant uppercase tracking-[0.2em] mb-1">
                    Hết hạn
                  </p>
                  <p className="font-body-md text-on-surface">{formatDateVi(mySubscription.expiresAt)}</p>
                </div>
              </div>
              <ul className="flex flex-col gap-3 mb-8">
                <li className="flex items-center gap-3">
                  <Check weight="bold" size={18} className="text-primary" />
                  <span className="font-body-md text-body-md text-on-surface">
                    Tối đa {mySubscription.maxTicketsPerEventSnapshot.toLocaleString("vi-VN")} vé mỗi show
                  </span>
                </li>
                <li className={`flex items-center gap-3 ${!mySubscription.hasAiPosterSnapshot ? "text-on-surface-variant/60" : ""}`}>
                  {mySubscription.hasAiPosterSnapshot ? (
                    <Check weight="bold" size={18} className="text-primary" />
                  ) : (
                    <Lock weight="light" size={18} />
                  )}
                  <span className="font-body-md text-body-md">
                    {mySubscription.hasAiPosterSnapshot
                      ? `${mySubscription.maxAiPostersPerMonthSnapshot} poster AI mỗi tháng`
                      : "Poster AI"}
                  </span>
                </li>
                <li className={`flex items-center gap-3 ${mySubscription.maxTourScenesSnapshot === 0 ? "text-on-surface-variant/60" : ""}`}>
                  {mySubscription.maxTourScenesSnapshot > 0 ? (
                    <Check weight="bold" size={18} className="text-primary" />
                  ) : (
                    <Lock weight="light" size={18} />
                  )}
                  <span className="font-body-md text-body-md">
                    {mySubscription.maxTourScenesSnapshot > 0
                      ? `${mySubscription.maxTourScenesSnapshot} cảnh tour ảo 360°`
                      : "Tour ảo 360°"}
                  </span>
                </li>
              </ul>
              <div className="flex flex-col-reverse md:flex-row justify-end gap-3">
                {confirmingCancel ? (
                  <>
                    <button
                      onClick={() => setConfirmingCancel(false)}
                      className="px-6 py-2.5 rounded-lg text-on-surface-variant hover:bg-surface-variant transition-colors font-label-md text-label-md"
                    >
                      Không huỷ
                    </button>
                    <button
                      onClick={handleCancel}
                      disabled={busyAction === "cancel"}
                      className="px-6 py-2.5 rounded-lg bg-error text-on-error hover:opacity-90 transition-opacity font-label-md text-label-md disabled:opacity-50"
                    >
                      {busyAction === "cancel" ? "Đang huỷ..." : "Xác nhận huỷ gói"}
                    </button>
                  </>
                ) : (
                  <>
                    <button
                      onClick={() => setConfirmingCancel(true)}
                      className="px-6 py-2.5 rounded-lg border border-outline text-on-surface hover:bg-surface-container-low transition-colors font-label-md text-label-md"
                    >
                      Huỷ gói
                    </button>
                    <button
                      onClick={handleRenew}
                      disabled={busyAction === "renew"}
                      className="px-6 py-2.5 rounded-lg bg-primary text-on-primary hover:bg-primary-container transition-colors font-label-md text-label-md shadow-sm disabled:opacity-50"
                    >
                      {busyAction === "renew" ? "Đang chuyển đến VNPay..." : "Gia hạn"}
                    </button>
                  </>
                )}
              </div>
            </div>
          </div>
        )}

        {mySubscription === null && packages && (
          <>
            <div className="text-center mb-16 max-w-2xl">
              <span className="text-label-md font-label-md text-secondary tracking-widest uppercase">
                Gói dịch vụ
              </span>
              <h1 className="font-display-lg text-display-lg text-on-surface mt-3 mb-4">
                Chọn gói phù hợp với phòng trà của bạn
              </h1>
              <p className="font-body-lg text-body-lg text-on-surface-variant">
                Nâng tầm không gian âm nhạc của bạn với các tính năng độc quyền.
              </p>
            </div>
            {packages.length === 0 ? (
              <p className="font-body-md text-body-md text-on-surface-variant py-24">
                Hiện chưa có gói dịch vụ nào được mở.
              </p>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-3 gap-8 w-full max-w-6xl">
                {packages.map((pkg) => (
                  <PlanCard
                    key={pkg.id}
                    pkg={pkg}
                    recommended={pkg.id === recommendedId}
                    busy={busyAction === `subscribe-${pkg.id}`}
                    onChoose={() => handleChoose(pkg)}
                  />
                ))}
              </div>
            )}
          </>
        )}
      </main>
    </div>
  );
}
