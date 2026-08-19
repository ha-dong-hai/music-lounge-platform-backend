import { Link } from "react-router-dom";
import { CheckCircle, XCircle } from "@phosphor-icons/react";

// Shared landing page for BusinessSettings:PaymentSuccessUrl / PaymentFailedUrl -- every VNPay
// flow (subscriptions, tickets, donations) redirects the browser here after the backend's own
// vnpay-return handler has already confirmed or rejected the payment server-side. No query params
// are forwarded (every *Controller.VnPayReturn does a plain Redirect(...), nothing appended), so
// this page genuinely has no transaction details -- don't invent any.
//
// It also has no idea WHICH flow just finished for the same reason. TicketPurchase.tsx works
// around that by writing sessionStorage right before the redirect to VNPay (survives the full-page
// round trip, unlike router state) -- read once here, then cleared, so a stale value can't leak
// into a later unrelated payment. Absence of the key means "not a ticket" -- today that's always
// the subscription flow, the original (and until now, only) caller of this page.
const CONTEXT_KEY = "musiclounge_payment_context";

const COPY: Record<
  "subscription" | "ticket",
  { successMessage: string; failedMessage: string; ctaLabel: string; ctaTo: string; retryTo: string }
> = {
  subscription: {
    successMessage: "Gói dịch vụ của bạn đã được kích hoạt.",
    failedMessage: "Giao dịch không thành công hoặc đã bị huỷ. Bạn chưa bị trừ tiền cho gói dịch vụ này.",
    ctaLabel: "Xem gói dịch vụ",
    ctaTo: "/owner/subscription",
    retryTo: "/owner/subscription",
  },
  ticket: {
    successMessage: "Vé của bạn đã được xác nhận.",
    failedMessage: "Giao dịch không thành công hoặc đã bị huỷ. Bạn chưa bị trừ tiền cho vé này.",
    ctaLabel: "Xem vé của tôi",
    ctaTo: "/me/tickets",
    retryTo: "/shows",
  },
};

export default function PaymentResult({ status }: { status: "success" | "failed" }) {
  const isSuccess = status === "success";

  const context = sessionStorage.getItem(CONTEXT_KEY) === "ticket" ? "ticket" : "subscription";
  sessionStorage.removeItem(CONTEXT_KEY);
  const copy = COPY[context];

  return (
    <div className="bg-surface min-h-screen flex items-center justify-center px-margin-mobile text-on-surface">
      <div className="max-w-[420px] w-full text-center">
        <span className="font-display-lg text-headline-sm text-primary tracking-tight">MusicLounge</span>
        <div className="flex justify-center my-8">
          {isSuccess ? (
            <CheckCircle weight="light" size={64} className="text-tertiary" />
          ) : (
            <XCircle weight="light" size={64} className="text-error" />
          )}
        </div>
        <h1 className="font-headline-md text-headline-md text-on-surface mb-2">
          {isSuccess ? "Thanh toán thành công" : "Thanh toán thất bại"}
        </h1>
        <p className="font-body-md text-body-md text-on-surface-variant mb-8">
          {isSuccess ? copy.successMessage : copy.failedMessage}
        </p>
        <Link
          to={isSuccess ? copy.ctaTo : copy.retryTo}
          className="inline-block px-6 py-3 bg-primary text-on-primary font-label-md text-label-md rounded-xl hover:opacity-90 transition-opacity"
        >
          {isSuccess ? copy.ctaLabel : "Thử lại"}
        </Link>
      </div>
    </div>
  );
}
