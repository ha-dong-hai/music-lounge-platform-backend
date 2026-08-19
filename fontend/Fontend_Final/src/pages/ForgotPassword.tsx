import { FormEvent, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../api/client";
import { requestPasswordReset } from "../api/auth";

// Ported from fontend/Fontend_Final/stitch/stitch_ai_reset_password/auralounge_forgot_password_desktop/
// -- a *different* Stitch export than the rest of the app ("AuraLounge" black/gold Material-3
// theme, Libre Caslon Text + Manrope) instead of the locked-in "Warm Luxury Lounge" system
// (tailwind.config.ts, terracotta + Playfair/Inter). User explicitly chose to keep this export's
// visual system for this screen + ResetPassword.tsx rather than re-skin it -- brand text corrected
// AuraLounge -> MusicLounge, copy translated to Vietnamese to match every other screen. Colors are
// literal Tailwind arbitrary values (not added to tailwind.config.ts) so this doesn't leak into or
// collide with the shared token set the rest of the app uses.
//
// Real image URL is Stitch's own placeholder (lh3.googleusercontent.com) -- same "swap for real
// venue photography before shipping" caveat as SignUp.tsx's polaroids.

const isValidEmail = (email: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);

export default function ForgotPassword() {
  const [email, setEmail] = useState("");
  const [emailError, setEmailError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmed = email.trim();

    if (!trimmed || !isValidEmail(trimmed)) {
      setEmailError("Vui lòng nhập email hợp lệ.");
      return;
    }
    setEmailError(null);
    setSubmitting(true);

    try {
      await requestPasswordReset(trimmed);
    } catch (err) {
      // Anti-enumeration means this only throws for real problems (malformed input past the
      // client check, network/server errors) -- never "email not found". Show the success state
      // anyway is wrong for a genuine network failure, so surface it as a field error instead.
      setSubmitting(false);
      setEmailError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      return;
    }

    setSubmitting(false);
    setSubmitted(true);
  }

  return (
    <div
      className="bg-[#fbf9f4] text-[#1b1c19] antialiased min-h-screen flex flex-col md:flex-row"
      style={{ fontFamily: "'Manrope', sans-serif" }}
    >
      <div className="flex-1 flex flex-col items-center justify-center p-6 md:p-16 w-full md:w-1/2 min-h-screen">
        <div className="w-full max-w-[480px] flex flex-col items-center">
          <header className="w-full flex justify-center mb-12">
            <h1
              className="text-[48px] leading-[56px] tracking-tight text-[#181512]"
              style={{ fontFamily: "'Libre Caslon Text', serif" }}
            >
              MusicLounge
            </h1>
          </header>

          {!submitted ? (
            <div className="flex flex-col gap-8 w-full">
              <div className="flex flex-col gap-2 text-center">
                <h2
                  className="text-[28px] md:text-[32px] leading-[36px] md:leading-[40px] text-[#181512]"
                  style={{ fontFamily: "'Libre Caslon Text', serif" }}
                >
                  Quên mật khẩu?
                </h2>
                <p className="text-base leading-6 text-[#4d4540] max-w-[380px] mx-auto">
                  Nhập email của bạn, chúng tôi sẽ gửi link đặt lại mật khẩu.
                </p>
              </div>

              <form className="flex flex-col gap-6" onSubmit={handleSubmit} noValidate>
                <div className="flex flex-col gap-2">
                  <label
                    htmlFor="email"
                    className="text-xs tracking-[0.1em] font-semibold text-[#4d4540]"
                  >
                    ĐỊA CHỈ EMAIL
                  </label>
                  <div className="relative flex items-center">
                    <span className="material-symbols-outlined absolute left-4 text-[#4d4540]" aria-hidden>
                      ✉
                    </span>
                    <input
                      id="email"
                      name="email"
                      type="email"
                      placeholder="vd. julian@musiclounge.vn"
                      required
                      value={email}
                      onChange={(e) => {
                        setEmail(e.target.value);
                        if (emailError) setEmailError(null);
                      }}
                      className={`w-full text-base text-[#181512] placeholder:text-[#cfc4bd] py-3 pl-12 pr-4 bg-[#f5f3ee] border rounded-lg focus:ring-1 focus:ring-[#181512] focus:border-[#181512] transition-all outline-none ${
                        emailError ? "border-[#ba1a1a] ring-1 ring-[#ba1a1a]" : "border-[#cfc4bd]"
                      }`}
                    />
                  </div>
                  {emailError && (
                    <span className="text-xs text-[#ba1a1a] flex items-center gap-1">
                      {emailError}
                    </span>
                  )}
                </div>

                <button
                  type="submit"
                  disabled={submitting}
                  className="w-full bg-[#181512] hover:bg-[#181512]/90 text-white text-sm font-medium tracking-wide py-4 px-6 rounded-lg transition-all active:scale-[0.98] disabled:opacity-70 disabled:cursor-not-allowed"
                >
                  {submitting ? "Đang gửi..." : "Gửi link đặt lại"}
                </button>
              </form>

              <div className="flex justify-center">
                <Link
                  to="/login"
                  className="flex items-center gap-2 text-sm text-[#4d4540] hover:text-[#181512] transition-colors"
                >
                  ← Quay lại Đăng nhập
                </Link>
              </div>
            </div>
          ) : (
            <div className="flex flex-col gap-4 w-full items-center text-center py-8">
              <div className="w-20 h-20 rounded-full bg-[#f5f3ee] flex items-center justify-center mb-2 text-[32px] text-[#181512]">
                ✉
              </div>
              <h2
                className="text-2xl leading-8 text-[#181512]"
                style={{ fontFamily: "'Libre Caslon Text', serif" }}
              >
                Kiểm tra hộp thư
              </h2>
              <p className="text-base leading-6 text-[#4d4540] max-w-[400px]">
                Nếu email này gắn với một tài khoản, bạn sẽ nhận được link đặt lại mật khẩu trong ít
                phút tới.
              </p>
              <Link
                to="/login"
                className="mt-6 flex items-center gap-2 text-sm text-[#181512] border border-[#cfc4bd] px-8 py-3 rounded-lg hover:bg-[#f5f3ee] transition-colors w-full md:w-auto justify-center"
              >
                ← Quay lại Đăng nhập
              </Link>
            </div>
          )}
        </div>
      </div>

      <div className="hidden md:flex flex-1 bg-[#181512] relative overflow-hidden items-center justify-center">
        <img
          alt="Không khí phòng trà ấm áp với mâm đĩa than cao cấp"
          className="absolute inset-0 w-full h-full object-cover opacity-90 mix-blend-overlay"
          src="https://lh3.googleusercontent.com/aida-public/AB6AXuCF-6l8nLCjVAPisV7oQ3dLmWhO0y4WzuFoV_YstdRg0b_fC8ePXwp2eALlj2nJjWiDPqYiTjvvgpUOPguI41tsyvWy2DVP9EEsGajLBfK3Fe9nAgVEjReEzlApBStXDDMkkTJXajBeqhWtIRXZO0ITwIHflhd4Ac3ddoRAQfvJ4sLvMIybDrg5LR_l0hCz6skzGnY48jl0JQMIpq2ODI4gG2Dgm1k0nu_TgLtBmDeFt2KEvgyyvmqrDQ"
        />
        <div className="absolute inset-0 bg-gradient-to-r from-[#fbf9f4] via-[#fbf9f4]/40 to-transparent opacity-80" />
        <div className="absolute inset-0 bg-gradient-to-t from-[#181512]/60 to-transparent" />
      </div>
    </div>
  );
}
