import { FormEvent, useMemo, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Eye, EyeSlash } from "@phosphor-icons/react";
import { ApiError } from "../api/client";
import { AccountRole, registerAccount } from "../api/auth";
import { estimatePasswordStrength, PasswordScore } from "../lib/passwordStrength";
import { suggestEmailDomain } from "../lib/emailDomainSuggest";

// Ported from fontend/Fontend_Final/stitch_musiclounge_warm_signup_ui/code.html ("Warm
// Luxury Lounge" design). Visual structure kept 1:1; vanilla-JS behaviour (role toggle,
// password strength meter, show/hide password, parallax) reimplemented as real React state
// instead of direct DOM manipulation. Polaroid photos still point at the placeholder image
// URLs Stitch generated -- swap for real venue photography before this ships for real.

const POLAROIDS = [
  {
    id: "top-left",
    wrapClass: "absolute -top-4 -left-12 z-10",
    imgClass: "w-48 h-48 object-cover sepia-[.2] contrast-125",
    rotateClass: "rotate-[-6deg] hover:rotate-[-4deg]",
    delay: "0.1s",
    factor: 0.4,
    caption: "đêm mưa nhạc jazz",
    src: "https://lh3.googleusercontent.com/aida-public/AB6AXuAdcE0dj3Kxe7MoPfEsGrxAW--94Hp8ipbovnGFLjIJozhzBx8UXlUbQB9L0A2r_wxerZiNiprhOZVK3RR5NqiWJGgV_5bKHE24jr2n5b_RjdJI4wU7f6hwIdmzIAlwgFQdB6h8_6rlDwh6A3YfBoYElLefokR3dVvugCvXVa7gTSkXK4K1mFU7iF1gQMWoRegHFHGrCvFuqAW_gZkU_njZqrqVC4uM9VYV35VoM810oIt5Gp8Ziu6DIQ",
    alt: "Kèn saxophone vàng óng trong ánh sáng ấm, gợi không khí jazz đêm khuya.",
  },
  {
    id: "center-right",
    wrapClass: "absolute top-1/4 right-0 z-30",
    imgClass: "w-64 h-64 object-cover sepia-[.1] contrast-110",
    rotateClass: "rotate-[4deg] hover:rotate-[2deg]",
    delay: "0.3s",
    factor: 0.6,
    caption: "âm mộc — chiều tà",
    src: "https://lh3.googleusercontent.com/aida-public/AB6AXuA7r1HuES7LnQZhOp_o4IClvLoCSGMT-rHDHdu9vE99Uyzoo-if2EOsZr1cc0HOfxloCg93_5p2JD3UzSO844cB8MEDXhyte7YQvJ5eTMFLLnK4Op42fpZ7lyhXXpJ2d70XUVe7yZluZukkT7dN8WM_imJTnOV5llG8SlHrdLq0-ixaBXQ5QVNyGiNn0WRjLV3x_wWf1x5RxbcjX1hqdts_lNITvip5YX9h8wNewoYwo7Sd0JEAB9VpQw",
    alt: "Guitar acoustic tựa vào ghế bành nhung, ánh sáng chiều tà ấm áp.",
  },
  {
    id: "bottom-left",
    wrapClass: "absolute bottom-4 left-8 z-20",
    imgClass: "w-40 h-40 object-cover sepia-[.3] contrast-120",
    rotateClass: "rotate-[7deg] hover:rotate-[9deg]",
    delay: "0.5s",
    factor: 0.3,
    caption: "phố nhạc — 21:30",
    src: "https://lh3.googleusercontent.com/aida-public/AB6AXuCCGSxTnBLdjHlofyZRCXnCKYUaMqsYzBQz7CUXghFxtvOl-EqsmPjIASgIBJW-bw_u19nEp9774M0mzDYJEc3dYYUi8-jutC4sMeCbhEYPeT3eb6xX3O-jUlZgSwRwHaMUQUkkEV04tscLbacwYg9c6xk0aLasUHRrR7d1i1lg1MipZ3NlXDhmfrd_gntQXaGX9zcXUybjr9KvDqNXXmLyI3vhvZuGFWrtcR8wKZoP0PO05Y1EDFilHA",
    alt: "Ly rượu vang cạnh nến tan chảy trên bàn gỗ, không khí lounge ấm cúng.",
  },
];

// zxcvbn score is 0-4 (see src/lib/passwordStrength.ts for why a real entropy/crack-time
// estimator replaces a naive composition check here).
const STRENGTH_COLOR: Record<PasswordScore, string> = {
  0: "bg-error",
  1: "bg-error",
  2: "bg-primary-container",
  3: "bg-primary-container",
  4: "bg-tertiary",
};
const STRENGTH_LABEL: Record<PasswordScore, string> = {
  0: "Rất yếu",
  1: "Yếu",
  2: "Trung bình",
  3: "Khá mạnh",
  4: "Mạnh",
};

export default function SignUp() {
  const navigate = useNavigate();
  const containerRef = useRef<HTMLDivElement>(null);

  const [role, setRole] = useState<AccountRole>("Audience");
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  // Never default this to true -- Luật 91/2025/QH15 requires an explicit, affirmative choice,
  // not a pre-checked box. Backend rejects the request outright if this isn't true.
  const [acceptTerms, setAcceptTerms] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [generalError, setGeneralError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [parallax, setParallax] = useState<Record<string, { x: number; y: number }>>({});
  // touched: field has been blurred at least once -> safe to show a validation error for it.
  // engaged: field has been focused at least once -> safe to show its upfront requirement hint.
  // Kept separate on purpose: WCAG 2.2 SC 3.3.2 requires format requirements to be visible when a
  // user enters a field, well before any error would fire on blur/submit.
  const [touched, setTouched] = useState<Record<string, boolean>>({});
  const [engaged, setEngaged] = useState<Record<string, boolean>>({});

  const strength = useMemo(
    () => estimatePasswordStrength(password, [fullName, email]),
    [password, fullName, email]
  );
  const emailSuggestion = useMemo(() => suggestEmailDomain(email.trim()), [email]);

  function acceptEmailSuggestion(suggestion: string) {
    setEmail(suggestion);
    if (touched.email) setFieldError("email", validateField("email", suggestion));
  }

  function fieldError(name: string): string | null {
    const key = Object.keys(fieldErrors).find((k) => k.toLowerCase() === name.toLowerCase());
    return key ? fieldErrors[key][0] : null;
  }

  // Mirrors backend RegisterCommandValidator.cs 1:1 so the user gets the same verdict instantly
  // instead of waiting on a round-trip -- see docs/api/AUTH-SECURITY-STANDARDS.md for the sourced
  // rationale behind each rule. Backend remains the real enforcement point regardless.
  function validateField(name: string, value: string | boolean): string | null {
    switch (name) {
      case "fullName": {
        const v = (value as string).trim();
        if (!v) return "Họ tên không được để trống.";
        if (!/^[\p{L}\p{M} .'-]+$/u.test(v)) {
          return "Họ tên chỉ được chứa chữ cái và các ký tự . ' -.";
        }
        return null;
      }
      case "email": {
        const v = (value as string).trim();
        if (!v) return "Email không được để trống.";
        // Mirrors the backend's ACTUAL rule, not an invented pattern -- verified against
        // FluentValidation 11.11.0's real EmailAddress() source (default AspNetCoreCompatible
        // mode): exactly one '@', not the first or last character. Nothing about domain/TLD
        // structure is checked here on purpose -- see docs/api/AUTH-SECURITY-STANDARDS.md §3.
        const at = v.indexOf("@");
        if (at === -1 || at !== v.lastIndexOf("@") || at === 0 || at === v.length - 1) {
          return "Email không hợp lệ.";
        }
        return null;
      }
      case "password":
        if ((value as string).length < 15) return "Mật khẩu phải có ít nhất 15 ký tự.";
        return null;
      case "acceptTerms":
        if (!value) return "Bạn cần đồng ý với Điều khoản dịch vụ và Chính sách bảo mật để đăng ký.";
        return null;
      default:
        return null;
    }
  }

  function setFieldError(name: string, error: string | null) {
    setFieldErrors((prev) => {
      const next = { ...prev };
      if (error) next[name] = [error];
      else delete next[name];
      return next;
    });
  }

  function handleFocusField(name: string) {
    setEngaged((e) => (e[name] ? e : { ...e, [name]: true }));
  }

  function handleBlurField(name: string, value: string | boolean) {
    setTouched((t) => ({ ...t, [name]: true }));
    setFieldError(name, validateField(name, value));
  }

  function makeChangeHandler<T extends string | boolean>(
    name: string,
    setter: (v: T) => void
  ) {
    return (value: T) => {
      setter(value);
      if (touched[name]) setFieldError(name, validateField(name, value));
    };
  }

  const onFullNameChange = makeChangeHandler<string>("fullName", setFullName);
  const onEmailChange = makeChangeHandler<string>("email", setEmail);
  const onPasswordChange = makeChangeHandler<string>("password", setPassword);

  function handleMouseMove(e: React.MouseEvent<HTMLDivElement>) {
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect) return;
    const normX = ((e.clientX - rect.left) / rect.width) * 2 - 1;
    const normY = ((e.clientY - rect.top) / rect.height) * 2 - 1;
    const next: Record<string, { x: number; y: number }> = {};
    for (const p of POLAROIDS) {
      next[p.id] = { x: normX * -20 * p.factor, y: normY * -20 * p.factor };
    }
    setParallax(next);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setGeneralError(null);

    const values: Record<string, string | boolean> = { fullName, email, password, acceptTerms };
    const errors: Record<string, string[]> = {};
    for (const name of Object.keys(values)) {
      const error = validateField(name, values[name]);
      if (error) errors[name] = [error];
    }
    setTouched({ fullName: true, email: true, password: true, acceptTerms: true });
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSubmitting(true);
    try {
      const result = await registerAccount({
        email,
        password,
        fullName,
        // Phone intentionally isn't collected on this form -- see docs/api/AUTH-SECURITY-STANDARDS.md
        // §4: showing a field the form doesn't validate is worse than not showing it at all.
        // Collected later, at the point it's actually used (phone verification), once that view exists.
        phone: null,
        acceptTerms,
        role,
      });
      navigate("/verify-email", {
        state: { email, verificationCodeExpiresAt: result.verificationCodeExpiresAt },
      });
    } catch (err) {
      if (err instanceof ApiError) {
        setGeneralError(err.fieldErrors ? null : err.message);
        setFieldErrors(err.fieldErrors ?? {});
      } else {
        setGeneralError("Không thể kết nối tới máy chủ. Vui lòng thử lại.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="bg-surface text-on-surface antialiased min-h-screen flex selection:bg-primary-container/30 selection:text-primary">
      {/* Left Pane: Visuals (55%) */}
      <div
        ref={containerRef}
        onMouseMove={handleMouseMove}
        className="hidden lg:flex w-[55%] bg-surface-container relative overflow-hidden items-center justify-center"
      >
        <div
          className="absolute inset-0 pointer-events-none opacity-[0.04]"
          style={{
            backgroundImage:
              "url(\"data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noiseFilter'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.8' numOctaves='3' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noiseFilter)'/%3E%3C/svg%3E\")",
          }}
        />
        <div className="absolute w-[600px] h-[600px] bg-primary/10 rounded-full blur-[100px] mix-blend-multiply opacity-60 pointer-events-none" />

        <div className="relative w-full max-w-[600px] aspect-square flex items-center justify-center">
          {POLAROIDS.map((p) => {
            const offset = parallax[p.id] ?? { x: 0, y: 0 };
            return (
              <div
                key={p.id}
                className={`${p.wrapClass} transition-transform duration-300 ease-out`}
                style={{
                  transform: `translate(${offset.x}px, ${offset.y}px)`,
                  animation: `polaroidFadeIn 0.8s cubic-bezier(0.16,1,0.3,1) ${p.delay} both`,
                }}
              >
                <div
                  className={`bg-surface-container-lowest p-4 pb-6 rounded-sm shadow-[0_12px_32px_rgba(43,42,39,0.08)] border border-outline-variant/20 ${p.rotateClass} transition-transform duration-500 ease-out hover:scale-105`}
                >
                  <img className={p.imgClass} src={p.src} alt={p.alt} />
                  <p className="font-caption-handwriting text-caption-handwriting italic text-on-surface-variant mt-4 text-center">
                    {p.caption}
                  </p>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Right Pane: Form (45%) */}
      <div className="w-full lg:w-[45%] bg-surface-container-lowest flex flex-col justify-center px-margin-mobile md:px-lg py-xl relative shadow-[-10px_0_30px_rgba(43,42,39,0.03)] z-10 overflow-y-auto">
        <div className="max-w-[420px] w-full mx-auto">
          <div className="mb-xl">
            <span className="font-display-lg text-headline-sm text-primary tracking-tight">
              MusicLounge
            </span>
          </div>

          <h1 className="font-display-lg text-headline-md text-on-surface mb-2">Tạo tài khoản</h1>
          <p className="font-body-md text-body-md text-on-surface-variant mb-lg">
            Bắt đầu hành trình thưởng thức âm nhạc đích thực.
          </p>

          {/* Role Toggle */}
          <div
            className="bg-surface-container p-1 rounded-xl flex items-center mb-lg relative shadow-[inset_0_2px_4px_rgba(43,42,39,0.04)]"
            role="tablist"
            aria-label="Loại tài khoản"
          >
            <div
              className="absolute left-1 top-1 bottom-1 w-[calc(50%-4px)] bg-primary/15 rounded-lg transition-transform duration-300 ease-out"
              style={{ transform: role === "Owner" ? "translateX(100%)" : "translateX(0)" }}
            />
            <button
              type="button"
              role="tab"
              aria-selected={role === "Audience"}
              onClick={() => setRole("Audience")}
              className={`relative z-10 flex-1 py-2.5 font-label-md text-label-md rounded-lg transition-colors ${
                role === "Audience" ? "text-primary" : "text-on-surface-variant hover:text-on-surface"
              }`}
            >
              Khán giả
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={role === "Owner"}
              onClick={() => setRole("Owner")}
              className={`relative z-10 flex-1 py-2.5 font-label-md text-label-md rounded-lg transition-colors ${
                role === "Owner" ? "text-primary" : "text-on-surface-variant hover:text-on-surface"
              }`}
            >
              Chủ phòng trà
            </button>
          </div>

          {generalError && (
            <div
              role="alert"
              aria-live="assertive"
              className="mb-md rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3"
            >
              {generalError}
            </div>
          )}

          <form className="flex flex-col gap-sm" onSubmit={handleSubmit} noValidate>
            {/* Full Name */}
            <div>
              <div className="relative group">
                <input
                  className={`peer w-full bg-surface-container-low border rounded-xl px-4 pt-6 pb-2 text-on-surface font-body-md text-body-md focus:outline-none focus:ring-1 transition-all ${
                    fieldError("fullName")
                      ? "border-error focus:border-error focus:ring-error"
                      : "border-outline-variant/60 focus:border-primary focus:ring-primary focus:bg-surface-container-lowest"
                  }`}
                  id="fullname"
                  placeholder=" "
                  required
                  type="text"
                  maxLength={255}
                  autoComplete="name"
                  aria-invalid={!!fieldError("fullName")}
                  aria-describedby="fullname-hint fullname-error"
                  value={fullName}
                  onChange={(e) => onFullNameChange(e.target.value)}
                  onFocus={() => handleFocusField("fullName")}
                  onBlur={(e) => handleBlurField("fullName", e.target.value)}
                />
                <label
                  className="absolute left-4 top-4 text-on-surface-variant font-body-md text-body-md transition-all peer-focus:-translate-y-2.5 peer-focus:text-xs peer-focus:text-primary peer-[:not(:placeholder-shown)]:-translate-y-2.5 peer-[:not(:placeholder-shown)]:text-xs cursor-text"
                  htmlFor="fullname"
                >
                  Họ và tên
                </label>
              </div>
              {engaged.fullName && !fieldError("fullName") && (
                <p id="fullname-hint" className="mt-1 px-1 text-xs font-body-md text-on-surface-variant">
                  Chỉ dùng chữ cái (có dấu), không dùng số hoặc ký tự đặc biệt.
                </p>
              )}
              <p id="fullname-error" className="mt-1 text-xs text-error font-body-md" aria-live="polite">
                {fieldError("fullName") ?? ""}
              </p>
            </div>

            {/* Email */}
            <div>
              <div className="relative group">
                <input
                  className={`peer w-full bg-surface-container-low border rounded-xl px-4 pt-6 pb-2 text-on-surface font-body-md text-body-md focus:outline-none focus:ring-1 transition-all ${
                    fieldError("email")
                      ? "border-error focus:border-error focus:ring-error"
                      : "border-outline-variant/60 focus:border-primary focus:ring-primary focus:bg-surface-container-lowest"
                  }`}
                  id="email"
                  placeholder=" "
                  required
                  type="email"
                  maxLength={255}
                  autoComplete="email"
                  aria-invalid={!!fieldError("email")}
                  aria-describedby="email-suggestion email-error"
                  value={email}
                  onChange={(e) => onEmailChange(e.target.value)}
                  onFocus={() => handleFocusField("email")}
                  onBlur={(e) => handleBlurField("email", e.target.value)}
                />
                <label
                  className="absolute left-4 top-4 text-on-surface-variant font-body-md text-body-md transition-all peer-focus:-translate-y-2.5 peer-focus:text-xs peer-focus:text-primary peer-[:not(:placeholder-shown)]:-translate-y-2.5 peer-[:not(:placeholder-shown)]:text-xs cursor-text"
                  htmlFor="email"
                >
                  Địa chỉ Email
                </label>
              </div>
              {/* Soft, non-blocking hint only -- never rejects a domain that isn't in the common
                  list, since plenty of real domains legitimately aren't. See
                  docs/api/AUTH-SECURITY-STANDARDS.md §3 for why this replaced a stronger check. */}
              {emailSuggestion && (
                <p id="email-suggestion" className="mt-1 px-1 text-xs font-body-md text-on-surface-variant" aria-live="polite">
                  Có phải bạn muốn nhập{" "}
                  <button
                    type="button"
                    className="text-primary hover:underline decoration-primary/30 underline-offset-2 font-medium"
                    onClick={() => acceptEmailSuggestion(emailSuggestion)}
                  >
                    {emailSuggestion}
                  </button>
                  ?
                </p>
              )}
              <p id="email-error" className="mt-1 text-xs text-error font-body-md" aria-live="polite">
                {fieldError("email") ?? ""}
              </p>
            </div>

            {/* Password */}
            <div>
              <div className="relative group">
                <input
                  className={`peer w-full bg-surface-container-low border rounded-xl px-4 pt-6 pb-2 pr-12 text-on-surface font-body-md text-body-md focus:outline-none focus:ring-1 transition-all ${
                    fieldError("password")
                      ? "border-error focus:border-error focus:ring-error"
                      : "border-outline-variant/60 focus:border-primary focus:ring-primary focus:bg-surface-container-lowest"
                  }`}
                  id="password"
                  placeholder=" "
                  required
                  minLength={15}
                  maxLength={64}
                  type={showPassword ? "text" : "password"}
                  autoComplete="new-password"
                  aria-invalid={!!fieldError("password")}
                  aria-describedby="password-requirements password-error"
                  value={password}
                  onChange={(e) => onPasswordChange(e.target.value)}
                  onFocus={() => handleFocusField("password")}
                  onBlur={(e) => handleBlurField("password", e.target.value)}
                />
                <label
                  className="absolute left-4 top-4 text-on-surface-variant font-body-md text-body-md transition-all peer-focus:-translate-y-2.5 peer-focus:text-xs peer-focus:text-primary peer-[:not(:placeholder-shown)]:-translate-y-2.5 peer-[:not(:placeholder-shown)]:text-xs cursor-text"
                  htmlFor="password"
                >
                  Mật khẩu
                </label>
                <button
                  type="button"
                  className="absolute right-3 top-3.5 text-on-surface-variant hover:text-primary transition-colors p-1"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                >
                  {showPassword ? <EyeSlash weight="light" size={20} /> : <Eye weight="light" size={20} />}
                </button>
                {/* Requirement shown as soon as the field is focused, not only after a failed
                    submit -- WCAG 2.2 SC 3.3.2 + NN/g "state password requirements upfront".
                    Deliberately doesn't list composition rules (uppercase/digit/symbol): NIST
                    SP 800-63B-4 explicitly forbids imposing them, so the real requirement here
                    is length, and the second line nudges toward a passphrase instead. */}
                {(engaged.password || password.length > 0) && (
                  <div id="password-requirements" className="mt-2 px-1" aria-live="polite">
                    <p
                      className={`text-xs font-body-md ${
                        password.length >= 15 ? "text-tertiary" : "text-on-surface-variant"
                      }`}
                    >
                      {password.length >= 15 ? "✓" : "•"} Tối thiểu 15 ký tự ({password.length}/15)
                    </p>
                    <p className="mt-0.5 text-xs font-body-md text-on-surface-variant opacity-80">
                      Không bắt buộc chữ hoa/số/ký tự đặc biệt — một cụm từ dài, dễ nhớ sẽ an toàn hơn.
                    </p>
                  </div>
                )}
                <div className="flex gap-1 mt-2 px-1">
                  {[1, 2, 3, 4].map((step) => (
                    <div
                      key={step}
                      className={`h-1 flex-1 rounded-full transition-colors duration-300 ${
                        password.length > 0 && strength.score >= step
                          ? STRENGTH_COLOR[strength.score]
                          : "bg-surface-variant"
                      }`}
                    />
                  ))}
                </div>
                {password.length > 0 && (
                  <p className="mt-1 px-1 text-xs font-body-md text-on-surface-variant" aria-live="polite">
                    {STRENGTH_LABEL[strength.score]}
                    {strength.suggestion ? ` — ${strength.suggestion}` : ""}
                  </p>
                )}
              </div>
              <p id="password-error" className="mt-1 text-xs text-error font-body-md" aria-live="polite">
                {fieldError("password") ?? ""}
              </p>
            </div>


            {/* Terms acceptance -- required by backend (Luật 91/2025/QH15), must start unchecked */}
            <div className="mt-1">
              <label className="flex items-start gap-2.5 cursor-pointer select-none">
                <input
                  type="checkbox"
                  className={`mt-0.5 h-4 w-4 shrink-0 rounded border accent-primary focus:outline-none focus:ring-1 focus:ring-primary ${
                    fieldError("acceptTerms") ? "border-error" : "border-outline-variant"
                  }`}
                  checked={acceptTerms}
                  aria-invalid={!!fieldError("acceptTerms")}
                  aria-describedby="acceptterms-error"
                  onChange={(e) => {
                    setAcceptTerms(e.target.checked);
                    setTouched((t) => ({ ...t, acceptTerms: true }));
                    setFieldError("acceptTerms", validateField("acceptTerms", e.target.checked));
                  }}
                />
                <span className="font-body-md text-body-md text-on-surface-variant leading-snug">
                  Tôi đã đọc và đồng ý với{" "}
                  <a href="/dieu-khoan" className="text-primary hover:underline decoration-primary/30 underline-offset-2" target="_blank" rel="noreferrer">
                    Điều khoản dịch vụ
                  </a>{" "}
                  và{" "}
                  <a href="/chinh-sach-bao-mat" className="text-primary hover:underline decoration-primary/30 underline-offset-2" target="_blank" rel="noreferrer">
                    Chính sách bảo mật
                  </a>
                  .
                </span>
              </label>
              <p id="acceptterms-error" className="mt-1 text-xs text-error font-body-md" aria-live="polite">
                {fieldError("acceptTerms") ?? ""}
              </p>
            </div>

            <button
              type="submit"
              disabled={submitting}
              className="w-full bg-primary text-on-primary font-label-md text-label-md py-3.5 rounded-xl mt-md shadow-[0_4px_12px_rgba(140,74,30,0.2)] hover:shadow-[0_6px_16px_rgba(140,74,30,0.3)] hover:-translate-y-0.5 transition-all duration-300 active:scale-[0.98] disabled:opacity-60 disabled:pointer-events-none"
            >
              {submitting ? "Đang tạo tài khoản..." : "Đăng ký"}
            </button>
          </form>

          <div className="mt-lg text-center font-body-md text-body-md text-on-surface-variant">
            Đã có tài khoản?{" "}
            <Link
              to="/login"
              className="text-primary font-medium hover:underline decoration-primary/30 underline-offset-4 transition-all"
            >
              Đăng nhập
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
