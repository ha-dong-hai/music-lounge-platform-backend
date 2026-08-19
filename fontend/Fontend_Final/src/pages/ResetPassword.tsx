import { FormEvent, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Eye, EyeSlash } from "@phosphor-icons/react";
import { ApiError } from "../api/client";
import { resetPassword } from "../api/auth";
import { estimatePasswordStrength, PasswordScore } from "../lib/passwordStrength";

// Ported from fontend/Fontend_Final/stitch/stitch_ai_reset_password/auralounge_reset_password_desktop/
// -- same "AuraLounge" export as ForgotPassword.tsx, see that file's header comment for the
// theme/brand-name decision (kept, renamed to MusicLounge, copy translated to Vietnamese).
//
// IMPORTANT deviation from the Stitch export, done deliberately: the original file ships all 3
// states (form/error/success) as sibling <div>s toggled by 3 demo preview buttons ("Form State" /
// "Error State" / "Success State") a human clicks to preview each one in isolation -- that's a
// Stitch-authoring affordance, not real product UI. Those buttons are NOT ported. Real state is
// derived only from actual events: no `?token=` in the URL renders "invalid" immediately, a 401
// from the API (expired/already-used/malformed token) transitions form -> "invalid", and a real
// 204 response transitions form -> "success". There is no user-operable control that can select
// "success" without a genuine successful reset, and no way back into "form" from either terminal
// state on this page (the buttons those states offer both navigate away: request a new link, or
// go log in) -- exactly the "invalid must not be able to show success" requirement this was built
// to satisfy.

type ViewState = "form" | "invalid" | "success";

const STRENGTH_COLOR: Record<PasswordScore, string> = {
  0: "#ba1a1a",
  1: "#ba1a1a",
  2: "#e9c176",
  3: "#e9c176",
  4: "#181512",
};
const STRENGTH_LABEL: Record<PasswordScore, string> = {
  0: "Rất yếu",
  1: "Yếu",
  2: "Trung bình",
  3: "Khá mạnh",
  4: "Mạnh",
};

const MIN_LENGTH = 15;

export default function ResetPassword() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");

  // Synchronous initial state -- a missing token never renders the form even for one frame.
  const [view, setView] = useState<ViewState>(token ? "form" : "invalid");

  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const strength = estimatePasswordStrength(newPassword);
  const mismatch = confirmPassword.length > 0 && confirmPassword !== newPassword;
  const canSubmit = newPassword.length >= MIN_LENGTH && confirmPassword === newPassword;

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!token) {
      // Shouldn't be reachable (form isn't rendered without a token), guards against a stale
      // closure if searchParams somehow changes underneath this render.
      setView("invalid");
      return;
    }
    if (newPassword.length < MIN_LENGTH) {
      setFormError(`Mật khẩu phải có ít nhất ${MIN_LENGTH} ký tự.`);
      return;
    }
    if (confirmPassword !== newPassword) {
      setFormError("Mật khẩu xác nhận không khớp.");
      return;
    }

    setFormError(null);
    setSubmitting(true);
    try {
      await resetPassword(token, newPassword);
      setSubmitting(false);
      setView("success");
    } catch (err) {
      setSubmitting(false);
      if (err instanceof ApiError && err.status === 401) {
        // Token rejected server-side (expired / already used / doesn't exist) -- same terminal
        // "invalid" state as a missing token, not a form-field error.
        setView("invalid");
        return;
      }
      setFormError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    }
  }

  return (
    <div
      className="bg-[#fbf9f4] text-[#1b1c19] antialiased min-h-screen flex flex-col items-center justify-center p-6"
      style={{ fontFamily: "'Manrope', sans-serif" }}
    >
      <header className="mb-10">
        <h1
          className="text-4xl md:text-5xl tracking-tight text-[#181512]"
          style={{ fontFamily: "'Libre Caslon Text', serif" }}
        >
          MusicLounge
        </h1>
      </header>

      <div className="w-full max-w-md bg-white p-8 md:p-10 rounded-lg border border-[#1b1c19]/10 shadow-[0_20px_60px_-15px_rgba(24,21,18,0.08)]">
        {view === "form" && (
          <div>
            <div className="text-center mb-8">
              <h2
                className="text-[28px] leading-9 text-[#181512] mb-2"
                style={{ fontFamily: "'Libre Caslon Text', serif" }}
              >
                Đặt lại mật khẩu
              </h2>
              <p className="text-base text-[#4d4540]">Tạo mật khẩu mới cho tài khoản của bạn.</p>
            </div>

            <form className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
              <div>
                <div className="relative">
                  <input
                    id="new-password"
                    type={showNew ? "text" : "password"}
                    placeholder="Mật khẩu mới"
                    value={newPassword}
                    onChange={(e) => {
                      setNewPassword(e.target.value);
                      if (formError) setFormError(null);
                    }}
                    className="w-full bg-transparent border-0 border-b border-[#7e756f]/30 focus:border-[#181512] focus:ring-0 px-0 py-3 text-[#181512] placeholder:text-[#cfc4bd] outline-none transition-colors"
                  />
                  <button
                    type="button"
                    onClick={() => setShowNew((v) => !v)}
                    className="absolute right-0 top-1/2 -translate-y-1/2 text-[#7e756f] hover:text-[#181512] transition-colors"
                    aria-label={showNew ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                  >
                    {showNew ? <EyeSlash size={20} /> : <Eye size={20} />}
                  </button>
                </div>

                {newPassword.length > 0 && (
                  <div className="mt-3">
                    <div className="flex gap-1 h-1 w-full rounded-full overflow-hidden bg-[#e4e2dd]">
                      {[0, 1, 2, 3].map((i) => (
                        <div
                          key={i}
                          className="h-full flex-1 transition-all duration-300"
                          style={{
                            backgroundColor: i <= strength.score ? STRENGTH_COLOR[strength.score] : "transparent",
                          }}
                        />
                      ))}
                    </div>
                    <p className="text-xs text-[#7e756f] mt-1.5">
                      {STRENGTH_LABEL[strength.score]}
                      {strength.suggestion ? ` — ${strength.suggestion}` : ""}
                    </p>
                  </div>
                )}
                <p className="text-xs text-[#7e756f] mt-2 leading-tight">
                  Tối thiểu {MIN_LENGTH} ký tự. Ưu tiên độ dài hơn độ phức tạp.
                </p>
              </div>

              <div>
                <div className="relative">
                  <input
                    id="confirm-password"
                    type={showConfirm ? "text" : "password"}
                    placeholder="Xác nhận mật khẩu mới"
                    value={confirmPassword}
                    onChange={(e) => {
                      setConfirmPassword(e.target.value);
                      if (formError) setFormError(null);
                    }}
                    className={`w-full bg-transparent border-0 border-b focus:ring-0 px-0 py-3 text-[#181512] placeholder:text-[#cfc4bd] outline-none transition-colors ${
                      mismatch ? "border-[#ba1a1a]" : "border-[#7e756f]/30 focus:border-[#181512]"
                    }`}
                  />
                  <button
                    type="button"
                    onClick={() => setShowConfirm((v) => !v)}
                    className="absolute right-0 top-1/2 -translate-y-1/2 text-[#7e756f] hover:text-[#181512] transition-colors"
                    aria-label={showConfirm ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                  >
                    {showConfirm ? <EyeSlash size={20} /> : <Eye size={20} />}
                  </button>
                </div>
                <p className={`text-xs mt-1.5 leading-tight ${mismatch ? "text-[#ba1a1a]" : "text-[#7e756f]"}`}>
                  {mismatch ? "Mật khẩu xác nhận không khớp." : "Mật khẩu phải khớp nhau."}
                </p>
              </div>

              {formError && <p className="text-sm text-[#ba1a1a]">{formError}</p>}

              <button
                type="submit"
                disabled={!canSubmit || submitting}
                className="w-full bg-[#181512] text-white text-sm font-medium tracking-wide py-4 px-6 rounded-lg hover:bg-[#181512]/90 transition-colors mt-2 disabled:opacity-30 disabled:cursor-not-allowed"
              >
                {submitting ? "Đang xử lý..." : "Đặt lại mật khẩu"}
              </button>

              <div className="text-center mt-2">
                <Link
                  to="/login"
                  className="text-sm text-[#4d4540] hover:text-[#181512] transition-colors inline-flex items-center gap-2"
                >
                  ← Quay lại Đăng nhập
                </Link>
              </div>
            </form>
          </div>
        )}

        {view === "invalid" && (
          <div className="text-center flex flex-col items-center gap-4">
            <div className="w-16 h-16 rounded-full bg-[#ffdad6] flex items-center justify-center text-[#ba1a1a] text-3xl">
              !
            </div>
            <div>
              <h2
                className="text-2xl leading-8 text-[#181512] mb-2"
                style={{ fontFamily: "'Libre Caslon Text', serif" }}
              >
                Link đã hết hạn
              </h2>
              <p className="text-base text-[#4d4540] max-w-[280px] mx-auto">
                Link đặt lại mật khẩu này đã hết hạn hoặc không hợp lệ. Vì lý do bảo mật, link chỉ
                có hiệu lực trong 30 phút và chỉ dùng được 1 lần.
              </p>
            </div>
            <button
              type="button"
              onClick={() => navigate("/forgot-password")}
              className="w-full bg-[#e4e2dd] text-[#181512] text-sm font-medium tracking-wide py-4 px-6 rounded-lg border border-[#1b1c19]/10 hover:bg-[#dbdad5] transition-colors mt-2"
            >
              Gửi link đặt lại mới
            </button>
            <Link to="/login" className="text-sm text-[#4d4540] hover:text-[#181512] transition-colors">
              Quay lại Đăng nhập
            </Link>
          </div>
        )}

        {view === "success" && (
          <div className="text-center flex flex-col items-center gap-4">
            <div className="w-16 h-16 rounded-full bg-[#e4e2dd] border border-[#1b1c19]/10 flex items-center justify-center text-[#181512] text-3xl">
              ✓
            </div>
            <div>
              <h2
                className="text-2xl leading-8 text-[#181512] mb-2"
                style={{ fontFamily: "'Libre Caslon Text', serif" }}
              >
                Đổi mật khẩu thành công
              </h2>
              <p className="text-base text-[#4d4540] max-w-[280px] mx-auto">
                Mật khẩu của bạn đã được thay đổi. Vì lý do bảo mật, mọi phiên đăng nhập trước đó đã
                bị đăng xuất — hãy đăng nhập lại bằng mật khẩu mới.
              </p>
            </div>
            <button
              type="button"
              onClick={() => navigate("/login")}
              className="w-full bg-[#181512] text-white text-sm font-medium tracking-wide py-4 px-6 rounded-lg hover:bg-[#181512]/90 transition-colors mt-2"
            >
              Đăng nhập
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
