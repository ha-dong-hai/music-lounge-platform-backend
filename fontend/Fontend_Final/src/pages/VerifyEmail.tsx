import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { ArrowLeft, Timer } from "@phosphor-icons/react";
import { ApiError, setStoredToken, setStoredUser } from "../api/client";
import { resendVerificationCode, verifyEmail } from "../api/auth";

// Ported from fontend/Fontend_Final/stitch/verify_email/code.html ("Warm Luxury Lounge" design,
// same system as SignUp.tsx). Visual structure kept 1:1; vanilla-JS OTP-box behaviour (auto-advance,
// backspace-to-previous, paste-split, shake-on-error) reimplemented as real React state.

const CODE_LENGTH = 6;

const POLAROIDS = [
  {
    id: "envelope",
    wrapClass: "absolute w-56 left-[5%] top-[15%]",
    rotate: "-8deg",
    delay: "0.1s",
    factor: 0.4,
    src: "https://lh3.googleusercontent.com/aida-public/AB6AXuCToTjDC6GaymTZVYORYMHA2nI7RkUc-Gz53zkioCtaEY3NyThrjg7JueXoRVj3ZL-OXdsojIwwwClRPVopZdsbHgXOJ5Kfq65CZ7kbodV-GarU6YU-3jzIw1k86iZCf78lrjXa20FsabYVqHChoBYc_Vo_b2epnoPhdqx6UlUfAh3mRVHXRJKXcZdhgRssXOc1tX5o0uLhfaYRUPndPbz97SCyMJ_69QYkxyr-REQmPacQvjR8F9yJnA",
    alt: "Phong bì thư giấy da cổ với dấu niêm phong sáp đỏ khắc hình nốt nhạc, ánh sáng ấm kiểu jazz lounge.",
    imgClass: "object-cover grayscale-[20%] sepia-[30%]",
  },
  {
    id: "sax",
    wrapClass: "absolute w-48 right-[8%] top-[35%]",
    rotate: "12deg",
    delay: "0.3s",
    factor: 0.6,
    src: "https://lh3.googleusercontent.com/aida-public/AB6AXuCokkYdr2ivgQ5VKmzVexHeJo2bpJg_VNd5E2EYmia1WnU7NwxSa-dWDCLskxc_rDIrlNx-GGMi_IISHC7E0HoUaOYynKjazYdKFki9HVuBvVCEx9IeQPLU2igzAeNv2oNr365GJkMhWjOUPdP0J4yFEhwaIYxo2MPo5fbgifGaml946Fw14mFzqG-K1aiPJvY52X-3FqB7BJoStcBgfiFsKHjZic1oiFuJ1IKp7-tCQelArhVVTTFnnw",
    alt: "Cận cảnh cơ chế phím kèn saxophone đồng thau dưới ánh sáng vàng ấm.",
    imgClass: "object-cover grayscale-[10%] sepia-[20%]",
  },
  {
    id: "lounge",
    wrapClass: "absolute w-64 left-[25%] top-[45%]",
    rotate: "-2deg",
    delay: "0.5s",
    factor: 0.3,
    src: "https://lh3.googleusercontent.com/aida-public/AB6AXuD29bxnpWRQmQi4vvd6s6T1PY8-CJN3TZJtLQhhXmfamKYh_DscA0r9KqvHGIsr8kVVpGMfGT3W1DmusoIkRaZNJ3gBdilWZgbUIAvM7lh8Ut_zPCWzHU7hvHMD3E22Hv2ag4f4hnxbSiN9jpRBn-LiRBvH9LTVNIKSs9yQBnV_pBcZ8Xenfi6TbUFCYt-vkazX03J_71FiGwwqJN1lTMyz0Kh-cSw9A4XOzBLF_zrhtWf1mnCv5AlzxA",
    alt: "Góc phòng nghe nhạc cao cấp ấm cúng với ghế bành nhung màu đất nung và ampli đèn ống.",
    imgClass: "object-cover",
    caption: "The entry pass.",
  },
];

// Privacy-masked display only -- never sent anywhere, purely cosmetic (matches the Stitch design's
// "ng**n@gmail.com" treatment). The real email (full, unmasked) is what's actually submitted.
function maskEmail(email: string): string {
  const at = email.indexOf("@");
  if (at <= 1) return email;
  const local = email.slice(0, at);
  const domain = email.slice(at);
  const visible = local.length <= 2 ? local[0] : local.slice(0, 2);
  return `${visible}${"*".repeat(Math.max(local.length - visible.length, 2))}${domain}`;
}

function formatCountdown(seconds: number): string {
  const m = Math.floor(seconds / 60)
    .toString()
    .padStart(2, "0");
  const s = Math.floor(seconds % 60)
    .toString()
    .padStart(2, "0");
  return `${m}:${s}`;
}

export default function VerifyEmail() {
  const navigate = useNavigate();
  const location = useLocation();
  const state = location.state as { email?: string; verificationCodeExpiresAt?: string } | null;
  const email = state?.email;

  // State, not a derived const: resending issues a fresh code with a fresh expiry, and the
  // countdown must restart from that real value -- not stay frozen on the original nav state.
  const [expiresAt, setExpiresAt] = useState<Date | null>(
    state?.verificationCodeExpiresAt ? new Date(state.verificationCodeExpiresAt) : null
  );
  const [digits, setDigits] = useState<string[]>(Array(CODE_LENGTH).fill(""));
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [shake, setShake] = useState(false);
  const [success, setSuccess] = useState(false);
  const [resendStatus, setResendStatus] = useState<"idle" | "sending" | "sent">("idle");
  const [now, setNow] = useState(() => Date.now());
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

  useEffect(() => {
    const interval = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(interval);
  }, []);

  const remainingSeconds = expiresAt ? Math.max(0, Math.round((expiresAt.getTime() - now) / 1000)) : null;
  const code = digits.join("");
  const allFilled = digits.every((d) => d !== "");

  function clearErrorState() {
    if (errorMessage) setErrorMessage(null);
    if (shake) setShake(false);
  }

  function handleDigitChange(index: number, raw: string) {
    const digit = raw.replace(/[^0-9]/g, "").slice(-1);
    setDigits((prev) => {
      const next = [...prev];
      next[index] = digit;
      return next;
    });
    clearErrorState();
    if (digit && index < CODE_LENGTH - 1) inputRefs.current[index + 1]?.focus();
  }

  function handleKeyDown(index: number, e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Backspace" && !digits[index] && index > 0) {
      inputRefs.current[index - 1]?.focus();
    }
  }

  function handlePaste(e: React.ClipboardEvent<HTMLInputElement>) {
    e.preventDefault();
    const pasted = e.clipboardData.getData("text/plain").replace(/[^0-9]/g, "").slice(0, CODE_LENGTH);
    if (!pasted) return;
    setDigits((prev) => {
      const next = [...prev];
      for (let i = 0; i < pasted.length; i++) next[i] = pasted[i];
      return next;
    });
    clearErrorState();
    inputRefs.current[Math.min(pasted.length, CODE_LENGTH - 1)]?.focus();
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!email || !allFilled || submitting) return;

    setSubmitting(true);
    setErrorMessage(null);
    try {
      const result = await verifyEmail(email, code);
      // First time this account gets a token. Same role-based landing as Login.tsx (verifying
      // logs the account in for the first time, exactly like a login does) -- Owner/Admin have a
      // real screen to land on; Audience doesn't yet, so it falls through to the inline "Thành
      // công!" state below instead of navigating nowhere.
      setStoredToken(result.token);
      setStoredUser({ fullName: result.fullName, role: result.role, loungeId: result.loungeId });
      if (result.role === "Owner") {
        navigate("/owner/venues");
        return;
      }
      if (result.role === "Admin") {
        navigate("/admin/venues/pending");
        return;
      }
      if (result.role === "Audience") {
        navigate("/discover");
        return;
      }
      if (result.role === "Staff") {
        navigate("/staff/box-office");
        return;
      }
      setSuccess(true);
    } catch (err) {
      if (err instanceof ApiError) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage("Không thể kết nối tới máy chủ. Vui lòng thử lại.");
      }
      setShake(true);
      setDigits(Array(CODE_LENGTH).fill(""));
      inputRefs.current[0]?.focus();
    } finally {
      setSubmitting(false);
    }
  }

  async function handleResend() {
    if (!email || resendStatus === "sending") return;
    setResendStatus("sending");
    try {
      const result = await resendVerificationCode(email);
      setExpiresAt(new Date(result.verificationCodeExpiresAt));
      // A resend means whatever was typed against the OLD code is void -- clear it so the user
      // isn't left staring at a stale "expired" error next to a countdown that just restarted.
      clearErrorState();
      setDigits(Array(CODE_LENGTH).fill(""));
      inputRefs.current[0]?.focus();
    } catch {
      // Endpoint always resolves 200 on the backend regardless of account state (anti-enumeration) --
      // a thrown error here means a real connectivity failure, not "email not found". Still show the
      // same confirmation text either way so nothing about account existence leaks through timing/UI.
    } finally {
      setResendStatus("sent");
    }
  }

  const maskedEmail = useMemo(() => (email ? maskEmail(email) : ""), [email]);

  if (!email) {
    return (
      <div className="bg-surface min-h-screen flex items-center justify-center px-margin-mobile text-on-surface">
        <div className="max-w-[420px] w-full text-center">
          <span className="font-display-lg text-headline-sm text-primary tracking-tight">MusicLounge</span>
          <h1 className="font-display-lg text-headline-md text-on-surface mt-lg mb-2">Xác thực email</h1>
          <p className="font-body-md text-body-md text-on-surface-variant">
            Không tìm thấy thông tin đăng ký.{" "}
            <Link to="/" className="text-primary hover:underline">
              Quay lại Đăng ký
            </Link>
            .
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-background min-h-screen flex text-on-surface font-body-md selection:bg-primary-container selection:text-on-primary-container">
      {/* Left Panel: Visual Motif */}
      <div className="hidden lg:flex w-1/2 relative overflow-hidden bg-surface-container-low items-center justify-center p-12 border-r border-outline-variant/20">
        <div className="relative w-full max-w-lg aspect-square flex items-center justify-center">
          {POLAROIDS.map((p) => (
            <div
              key={p.id}
              className={`${p.wrapClass} bg-white p-4 pb-14 rounded-sm shadow-[0_12px_32px_rgba(43,42,39,0.12),0_4px_8px_rgba(43,42,39,0.04)] transition-transform duration-700 ease-out`}
              style={{ transform: `rotate(${p.rotate})`, animation: `polaroidFadeIn 0.8s cubic-bezier(0.16,1,0.3,1) ${p.delay} both` }}
            >
              <div className="w-full aspect-square bg-surface-variant overflow-hidden mb-3">
                <img className={`w-full h-full ${p.imgClass}`} src={p.src} alt={p.alt} />
              </div>
              {p.caption && (
                <p className="font-caption-handwriting text-caption-handwriting text-on-surface-variant opacity-80 text-center">
                  {p.caption}
                </p>
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Right Panel: OTP Form */}
      <div className="flex flex-col w-full lg:w-1/2 justify-center items-center px-margin-mobile sm:px-12 md:px-24 py-12 relative z-10">
        <div className="absolute top-8 left-8 hidden sm:block">
          <Link
            to="/"
            className="flex items-center gap-2 text-on-surface-variant hover:text-primary transition-colors font-label-md text-label-md group"
          >
            <ArrowLeft weight="light" size={20} className="group-hover:-translate-x-1 transition-transform" />
            Trở lại
          </Link>
        </div>

        <div className="w-full max-w-md">
          <div className="mb-10 flex flex-col items-center lg:items-start">
            <span className="font-display-lg text-headline-sm text-primary tracking-tight">MusicLounge</span>
          </div>

          <div className="text-center lg:text-left mb-8">
            <h1 className="font-headline-md text-headline-md text-on-surface mb-3">Xác thực email của bạn</h1>
            {/* Deliberately worded to be true in BOTH cases (email chưa có tài khoản / đã có tài
                khoản) -- mirrors OWASP Authentication Cheat Sheet's own example wording for this
                exact anti-enumeration scenario ("If that email address is in our database, we
                will send you an email..."). The old copy flatly claimed a code was sent, which was
                simply false in the duplicate-account case (no code is ever generated there). */}
            <p className="font-body-md text-body-md text-on-surface-variant">
              Nếu <span className="font-medium text-on-surface">{maskedEmail}</span> chưa có tài
              khoản, mã xác thực đã được gửi tới email này. Nếu email này đã có tài khoản, chúng
              tôi cũng đã gửi một email tới đó — bạn có thể đăng nhập trực tiếp thay vì nhập mã.
            </p>
          </div>

          <form className="w-full" onSubmit={handleSubmit}>
            <div className="flex gap-2 sm:gap-3 mb-6 justify-center lg:justify-start">
              {digits.map((digit, i) => (
                <input
                  key={i}
                  ref={(el) => (inputRefs.current[i] = el)}
                  aria-label={`Chữ số ${i + 1}`}
                  autoFocus={i === 0}
                  disabled={submitting || success}
                  className={`w-12 h-14 sm:w-14 sm:h-16 bg-surface border rounded-xl text-center font-body-lg text-[24px] font-semibold text-on-surface focus:ring-1 outline-none transition-all shadow-sm ${
                    shake
                      ? "border-error focus:border-error focus:ring-error"
                      : "border-outline-variant focus:border-primary focus:ring-primary"
                  } ${shake ? "animate-[shake_0.5s_cubic-bezier(.36,.07,.19,.97)_both]" : ""}`}
                  maxLength={1}
                  inputMode="numeric"
                  type="text"
                  value={digit}
                  onChange={(e) => handleDigitChange(i, e.target.value)}
                  onKeyDown={(e) => handleKeyDown(i, e)}
                  onPaste={handlePaste}
                  onFocus={(e) => e.target.select()}
                />
              ))}
            </div>

            {remainingSeconds !== null && (
              <div className="mb-10 text-center lg:text-left">
                <p className="font-label-md text-label-md text-on-surface-variant flex items-center justify-center lg:justify-start gap-1">
                  <Timer weight="light" size={18} />
                  {remainingSeconds > 0 ? (
                    <>
                      Mã có hiệu lực trong{" "}
                      <span className="font-medium text-primary">{formatCountdown(remainingSeconds)}</span>
                    </>
                  ) : (
                    <span className="text-error">Mã đã hết hạn — vui lòng gửi lại mã.</span>
                  )}
                </p>
              </div>
            )}

            <button
              type="submit"
              disabled={!allFilled || submitting || success}
              className={`w-full h-[56px] rounded-xl font-title-lg text-title-lg transition-all duration-300 flex justify-center items-center ${
                success
                  ? "bg-tertiary text-on-tertiary"
                  : "bg-primary text-on-primary disabled:opacity-50 disabled:bg-surface-variant disabled:text-on-surface-variant"
              }`}
            >
              {success ? "Thành công!" : submitting ? "Đang xác thực..." : "Xác thực"}
            </button>
            <p
              className={`text-error font-label-md text-label-md mt-2 text-center h-5 transition-opacity ${
                errorMessage ? "opacity-100" : "opacity-0"
              }`}
            >
              {errorMessage ?? " "}
            </p>

            <div className="flex justify-between items-center mt-8 px-2">
              <button
                type="button"
                onClick={handleResend}
                disabled={resendStatus === "sending" || success}
                className="text-secondary font-label-md text-label-md hover:text-primary hover:underline decoration-primary/30 underline-offset-4 transition-all focus:outline-none focus:ring-2 focus:ring-primary/20 rounded px-1 -mx-1 disabled:opacity-60"
              >
                {resendStatus === "sent" ? "Đã gửi lại mã" : resendStatus === "sending" ? "Đang gửi..." : "Gửi lại mã"}
              </button>
              <Link to="/" className="text-on-surface-variant font-label-md text-label-md hover:text-primary transition-colors sm:hidden">
                Quay lại
              </Link>
              <Link
                to="/"
                className="hidden sm:inline-block text-on-surface-variant font-label-md text-label-md hover:text-primary transition-colors"
              >
                Quay lại đăng ký
              </Link>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
