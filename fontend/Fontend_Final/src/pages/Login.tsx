import { FormEvent, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Eye, EyeSlash } from "@phosphor-icons/react";
import { ApiError, setStoredToken, setStoredUser } from "../api/client";
import { login } from "../api/auth";

// Ported from fontend/Fontend_Final/stitch/stitch_musiclounge_signup_u/code.html (folder name is
// a Stitch-internal leftover -- the actual export is the Login screen, confirmed via screen.png).
// Same "Warm Luxury Lounge" system as Sign Up / Verify Email.

const POLAROIDS = [
  {
    id: "sax",
    wrapClass: "absolute top-[10%] left-[15%] w-64",
    rotate: "-6deg",
    imgClass: "h-56",
    src: "https://lh3.googleusercontent.com/aida-public/AB6AXuBJopeTQpictIZerCSDihtmnOyUD63XsNwNBoYLNpydulYC7hT2ICMnoUhgGDErP9BHL1Qf1eKrGKAejHmcRafLnjN69tVr1afMZlHLgFIIKk5__PSFaZGufINYfOCu8uUh7_fSRKSHXmiuDDXsVAKOKIObQImtVCdCTewjGC-C_9yYkpU1A-XmDc7MsCRg1jEKrrUBaLVhMgjtiB5QSgLEaN1fM-_IsCcR5lA2J2EzpeXlK8ORzarn8Q",
    alt: "Kèn saxophone vàng óng trong ánh khói mờ ảo, gợi không khí jazz lounge cổ điển.",
  },
  {
    id: "wine",
    wrapClass: "absolute top-[15%] right-[15%] w-56",
    rotate: "8deg",
    imgClass: "h-48",
    src: "https://lh3.googleusercontent.com/aida-public/AB6AXuCC7t12OumiIBG2cJFVbl-2vnv6u0K9htGgKwK3H86f3hJ5spxH5ZY5FZ-x2ccsggtkxjwgGlyNXwSR5-XPG3WqklrxcWjfvhbs6cYF-99NyQezl4_6D85wkM3wEyiD3M8Axj1D19cqVY-4aWTSyvtu3AkDZ02LxwIOBCnw642qV8e879kvKTUYoRZxg9XyC3v9JOy6PprgxeEY1pSLBJDTo7zpKAn5lB5nPONREyO-iDzP9oinz5uITw",
    alt: "Ly rượu vang đỏ cạnh nến tan chảy trên bàn gỗ mộc, ánh sáng ấm áp lãng mạn.",
  },
  {
    id: "guitar",
    wrapClass: "absolute bottom-[15%] left-[20%] w-60",
    rotate: "-3deg",
    imgClass: "h-52",
    src: "https://lh3.googleusercontent.com/aida-public/AB6AXuDcAs98iULrPnXCqik_hUEE_kiuxjqq5IvyWYB2E3_2KFtRvGZrm9wzWlGx1mzDvLGobWw7FxsFZ4d7w5goxv8mFsQoAuWyzaiXPYhI4wBP0qlm4odRqAWVdUrSmNbTL91q0S0IiOyEGllQOZArejtZ9qDw4RjjMehat7f0SIDhRlb30zvarlm8Ggip3B31DrAouOcTwhLrqEXZeFRmJ423ab54NLpTlwz4AxRJb7smp22qaiWbq5NWag",
    alt: "Guitar acoustic tựa vào ghế bành nhung, ánh sáng ban ngày dịu nhẹ qua cửa sổ.",
  },
  {
    id: "lounge",
    wrapClass: "absolute bottom-[10%] right-[20%] w-64",
    rotate: "5deg",
    imgClass: "h-56",
    src: "https://lh3.googleusercontent.com/aida-public/AB6AXuA0SED4niwfkftabMQ2UyAj83P19l5E9vCpglRTk50LqZMoZd0N3Mf1m0Qc6DmMJpYcOgodRGRj7ERVVDHAHrRKASb7XZtyZv9pe6FFZPxqQJYRawWoHxz6DdKEcMW25B-R_8l_btH_nzw6CPrJyPACDAG4usey5N14IQa8pBseRPXEqxUPoS9Opa0xermzG5egC2KjTFJSIlGvOIcr4UyX6tGVUvqsSj281tYYZza-eUUTfRk7mv1Zyg",
    alt: "Góc phòng nghe nhạc ấm cúng ốp gỗ, ánh đèn bàn vàng dịu tạo không khí thư giãn.",
  },
];

export default function Login() {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [loggedInAs, setLoggedInAs] = useState<{ fullName: string; role: string } | null>(null);
  const [googleHint, setGoogleHint] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrorMessage(null);
    setSubmitting(true);
    try {
      const result = await login(email, password);
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
      setLoggedInAs({ fullName: result.fullName, role: result.role });
    } catch (err) {
      // Deliberately ONE generic message for wrong-email/wrong-password/unknown-email (backend
      // itself returns the identical "Email hoặc mật khẩu không đúng." for all three -- anti-
      // enumeration). Other real messages (unverified email, lockout, deactivated account) DO come
      // through distinctly here too, but that's fine/intentional: by that point the password was
      // already correct, so it's no longer an enumeration signal. Never invent separate UI states
      // per case -- just render whatever the backend actually said.
      setErrorMessage(
        err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ. Vui lòng thử lại."
      );
    } finally {
      setSubmitting(false);
    }
  }

  if (loggedInAs) {
    return (
      <div className="bg-surface min-h-screen flex items-center justify-center px-margin-mobile text-on-surface">
        <div className="max-w-[420px] w-full text-center">
          <span className="font-display-lg text-headline-sm text-primary tracking-tight">MusicLounge</span>
          <h1 className="font-headline-md text-headline-md text-on-surface mt-lg mb-2">
            Chào mừng trở lại, {loggedInAs.fullName}!
          </h1>
          <p className="font-body-md text-body-md text-on-surface-variant">
            Đăng nhập thành công ({loggedInAs.role}). Các màn hình tiếp theo chưa được thiết kế.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-background text-on-surface h-screen w-screen overflow-hidden flex flex-col md:flex-row">
      {/* Left Panel: Visuals */}
      <div className="hidden md:flex w-[55%] bg-surface-container-low relative items-center justify-center overflow-hidden">
        {POLAROIDS.map((p) => (
          <div
            key={p.id}
            className={`${p.wrapClass} bg-white p-3 pb-12 rounded-sm shadow-[0_4px_24px_rgba(43,42,39,0.08)]`}
            style={{ transform: `rotate(${p.rotate})` }}
          >
            <img className={`w-full ${p.imgClass} object-cover rounded-sm`} src={p.src} alt={p.alt} />
          </div>
        ))}
        <div className="absolute inset-0 bg-gradient-to-br from-surface-variant/20 to-surface-container-highest/40 mix-blend-multiply pointer-events-none" />
      </div>

      {/* Right Panel: Login Form */}
      <div className="w-full md:w-[45%] h-full flex flex-col justify-center items-center bg-surface px-margin-mobile md:px-lg relative z-40 overflow-y-auto py-12">
        <div className="w-full max-w-md flex flex-col gap-8">
          <div className="text-center md:text-left">
            <p className="font-display-lg text-headline-sm text-primary tracking-tight mb-6">MusicLounge</p>
            <h1 className="font-headline-md text-headline-md text-on-surface">Đăng nhập</h1>
          </div>

          {errorMessage && (
            <div
              role="alert"
              aria-live="assertive"
              className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3"
            >
              {errorMessage}
            </div>
          )}

          <form className="flex flex-col gap-5" onSubmit={handleSubmit} noValidate>
            <div className="relative group">
              <input
                className="peer w-full bg-surface-container-low border border-outline-variant/60 rounded-xl px-4 pt-6 pb-2 text-on-surface font-body-md text-body-md focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
                id="email"
                placeholder=" "
                required
                type="email"
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
              <label
                className="absolute left-4 top-4 text-on-surface-variant font-body-md text-body-md transition-all peer-focus:-translate-y-2.5 peer-focus:text-xs peer-focus:text-primary peer-[:not(:placeholder-shown)]:-translate-y-2.5 peer-[:not(:placeholder-shown)]:text-xs cursor-text"
                htmlFor="email"
              >
                Email
              </label>
            </div>

            <div>
              <div className="relative group">
                <input
                  className="peer w-full bg-surface-container-low border border-outline-variant/60 rounded-xl px-4 pt-6 pb-2 pr-12 text-on-surface font-body-md text-body-md focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
                  id="password"
                  placeholder=" "
                  required
                  type={showPassword ? "text" : "password"}
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
                <label
                  className="absolute left-4 top-4 text-on-surface-variant font-body-md text-body-md transition-all peer-focus:-translate-y-2.5 peer-focus:text-xs peer-focus:text-primary peer-[:not(:placeholder-shown)]:-translate-y-2.5 peer-[:not(:placeholder-shown)]:text-xs cursor-text"
                  htmlFor="password"
                >
                  Mật khẩu
                </label>
                <button
                  type="button"
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-on-surface-variant hover:text-primary transition-colors p-1"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                >
                  {showPassword ? <EyeSlash weight="light" size={20} /> : <Eye weight="light" size={20} />}
                </button>
              </div>
              <div className="flex justify-end mt-2">
                <Link
                  to="/forgot-password"
                  className="font-body-md text-body-md text-secondary hover:text-primary transition-colors text-sm"
                >
                  Quên mật khẩu?
                </Link>
              </div>
            </div>

            <button
              type="submit"
              disabled={submitting}
              className="w-full py-4 bg-primary text-on-primary font-label-md text-label-md rounded-xl hover:opacity-90 transition-opacity shadow-md disabled:opacity-60 disabled:pointer-events-none"
            >
              {submitting ? "Đang đăng nhập..." : "Đăng nhập"}
            </button>
          </form>

          <div className="relative flex items-center">
            <div className="flex-grow border-t border-outline-variant/50" />
            <span className="flex-shrink-0 mx-4 text-on-surface-variant font-body-md text-sm">hoặc</span>
            <div className="flex-grow border-t border-outline-variant/50" />
          </div>

          <div>
            <button
              type="button"
              onClick={() => setGoogleHint(true)}
              className="w-full py-4 bg-surface-container-lowest text-on-surface font-label-md text-label-md rounded-xl border border-outline-variant hover:bg-surface-variant/50 transition-colors flex justify-center items-center gap-3"
            >
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
                <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4" />
                <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853" />
                <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05" />
                <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335" />
              </svg>
              Đăng nhập bằng Google
            </button>
            {/* Honest placeholder, not a silent no-op -- Google Sign-In needs a real OAuth Client
                ID configured before GoogleLoginCommand's IdToken flow can be wired for real. */}
            {googleHint && (
              <p className="mt-2 text-xs text-on-surface-variant text-center">
                Google Sign-In chưa được cấu hình (cần Google OAuth Client ID).
              </p>
            )}
          </div>

          <p className="text-center font-body-md text-body-md text-on-surface-variant">
            Chưa có tài khoản?{" "}
            <Link to="/" className="text-primary font-semibold hover:underline decoration-primary/30 underline-offset-4">
              Đăng ký
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}
