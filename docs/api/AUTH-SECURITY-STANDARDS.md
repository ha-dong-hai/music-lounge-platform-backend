# Auth & Registration Form: Validation & UX Standards

Tài liệu này là **nguồn chân lý duy nhất** cho các business rule liên quan đến validate/UX của form
đăng ký, đăng nhập, đổi/quên mật khẩu — kèm **nguồn tham khảo chính thống** cho từng rule. Dùng
tài liệu này khi viết SRS/SDD (Report3/Report4) hoặc khi thêm field/validate mới cho các form Auth.
Không tự đặt ra rule mới ở đây mà không có nguồn — nếu chưa có nguồn, ghi rõ "chưa có nguồn chính
thống, đây là quyết định UX nội bộ" thay vì mạo nhận là standard.

Ngày research: 2026-08-14. Nguồn chính:
- **OWASP Authentication Cheat Sheet** — cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html
- **OWASP Input Validation Cheat Sheet** — cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html
- **NIST SP 800-63B Revision 4** (final, 07/2025) — pages.nist.gov/800-63-4/sp800-63b.html
- **WCAG 2.2, SC 3.3.2 Labels or Instructions** — w3.org/WAI/WCAG22/Understanding/labels-or-instructions.html
- **OWASP Automated Threats to Web Applications, OAT-019 Account Creation** — owasp.org/www-project-automated-threats-to-web-applications
- **Nielsen Norman Group**, "Disclosing Password Constraints in the UI" — UX authority, not a formal standard, cited explicitly as such

---

## 1. Password

| Rule | Giá trị | Nguồn | Áp dụng tại |
|---|---|---|---|
| Độ dài tối thiểu | 15 ký tự | NIST 800-63B-4 §5.1.1.2 (SHALL, vì hệ thống chưa có MFA) + OWASP Auth Cheat Sheet | `RegisterCommandValidator`, `ResetPasswordCommandValidator`, `ChangePasswordCommandValidator` |
| Độ dài tối đa | 64 ký tự | OWASP Auth Cheat Sheet ("at least 64 to allow passphrases") | 3 validator trên |
| Composition rule (bắt buộc hoa/số/ký tự đặc biệt) | **KHÔNG áp dụng** | NIST 800-63B-4: "Other composition requirements for passwords SHALL NOT be imposed" | Đã cố tình không có |
| Không được truncate password | Bắt buộc verify full password | NIST 800-63B-4 §5.1.1.2 (SHALL) | Backend hash toàn bộ chuỗi, không cắt |
| Cho phép paste vào ô password | Bắt buộc | NIST 800-63B-4 (SHOULD) + OWASP Auth Cheat Sheet | Frontend không có `onPaste` chặn |
| Cho phép hiện/ẩn mật khẩu (toggle) | Nên có | NIST 800-63B-4 (SHOULD) | `SignUp.tsx` — nút toggle `visibility`/`visibility_off` |
| Đo độ mạnh mật khẩu bằng entropy thật, không phải composition check | zxcvbn hoặc tương đương | OWASP Auth Cheat Sheet gọi tên trực tiếp zxcvbn | `passwordStrength.ts` (`@zxcvbn-ts/core`) |
| Đưa gợi ý cải thiện SAU khi từ chối | Bắt buộc | NIST 800-63B-4 (SHALL offer guidance, đặc biệt sau khi mật khẩu bị từ chối) | zxcvbn `feedback.suggestions` hiển thị realtime |
| **Hiển thị yêu cầu mật khẩu TRƯỚC khi user submit, không đợi lỗi** | Bắt buộc | WCAG 2.2 SC 3.3.2 (labels/instructions phải xuất hiện trước khi nhập, không chỉ khi lỗi) + NN/g: "State password requirements upfront and make them visible when the field is selected" | `SignUp.tsx` — checklist hiển thị ngay khi field được focus |
| Rate-limit số lần thử sai | Bắt buộc | NIST 800-63B-4 §3.2.2 (throttle) | `Program.cs` policy `"auth"` — 10 req/phút/IP trên toàn bộ `/auth/*` |

### Quyết định KHÔNG làm (đã research, không phải thiếu sót)

- **KHÔNG có ô "Nhập lại mật khẩu" (confirm password).** OWASP Auth Cheat Sheet không đề cập field
  này (im lặng = không yêu cầu). NN/g khuyến nghị bỏ hẳn: field này tạo thêm ma sát mà không thực
  sự giảm lỗi nhập sai, thay vào đó dùng toggle hiện/ẩn mật khẩu (đã có) là đủ.
- **KHÔNG check email đã tồn tại real-time khi user đang gõ.** Đây là chính bug enumeration mà
  anti-enumeration rewrite của `RegisterCommandHandler` vừa đóng lại — làm live-check ở FE sẽ mở
  lại đúng lỗ hổng đó qua đường khác. Xem [[musiclounge_password_standards_hardening]].
- **CAPTCHA cho form đăng ký — chưa triển khai, có nguồn đề xuất, hoãn có chủ đích.** OWASP OAT-019
  (Account Creation abuse) khuyến nghị CAPTCHA + rate-limiting làm 2 lớp phòng thủ độc lập. Hiện
  tại chỉ có rate-limiting (10 req/phút/IP, đã đủ chặn abuse thông thường cho quy mô đồ án). Thêm
  CAPTCHA cần tài khoản bên thứ 3 (reCAPTCHA/hCaptcha/Turnstile) — ngoài phạm vi tự động hoá được,
  cần quyết định của user khi triển khai thật.

---

## 2. Họ và tên (FullName)

| Rule | Giá trị | Nguồn |
|---|---|---|
| Bắt buộc, không rỗng | — | Nghiệp vụ |
| Tối đa 255 ký tự | — | Giới hạn cột DB |
| **Chỉ chấp nhận chữ cái Unicode + dấu + khoảng trắng + `. ' -`** | Regex `^[\p{L}\p{M} .'\-]+$` | OWASP Input Validation Cheat Sheet — "character category allowlisting" cho name field, giữ apostrophe cho tên kiểu "O'Brian", không giới hạn ASCII-only nên "Nguyễn Văn A" vẫn hợp lệ |

Rule allowlist này **chỉ áp dụng cho `RegisterCommand.FullName`** (tên thật của một con người), KHÔNG
áp dụng cho các field "Name" khác trong hệ thống (venue, bank, sản phẩm F&B...) — những field đó hợp
lệ chứa số/ký hiệu ("T&T Lounge", "5 Seconds of Summer") nên giữ nguyên không giới hạn ký tự.

## 3. Email

| Rule | Giá trị | Nguồn |
|---|---|---|
| Format tối thiểu: có đúng 1 ký tự `@`, không nằm ở đầu/cuối chuỗi | FluentValidation `EmailAddress()` mặc định dùng mode `AspNetCoreCompatible` | Đã verify trực tiếp qua source code FluentValidation 11.11.0 (bản đang cài) — **KHÔNG** kiểm tra cấu trúc domain hay TLD, đây là hành vi cố ý (mirror `[EmailAddress]` của ASP.NET Core), không phải thiếu sót |
| Tối đa 255 ký tự | — | OWASP Input Validation Cheat Sheet (local ≤ 64, domain ≤ 255, tổng ≤ 254 — cột DB 255 đã đủ dư) |
| Dùng email làm username, xác thực qua OTP trước khi cấp token | Đã có (`VerifyEmail` flow) | OWASP Auth Cheat Sheet |
| Không tiết lộ email đã tồn tại hay chưa (anti-enumeration) | Đã có | OWASP Auth Cheat Sheet |
| **Chữ hiển thị ở màn Verify Email phải đúng trong CẢ 2 trường hợp** (email mới / email đã có tài khoản) | "Nếu {email} chưa có tài khoản, mã xác thực đã được gửi... Nếu đã có tài khoản, chúng tôi cũng đã gửi email tới đó..." | OWASP Auth Cheat Sheet — ví dụ chữ mẫu chính xác: registration dùng *"A link to activate your account has been emailed..."*, forgot-password dùng *"If that email address is in our database, we will send you an email..."*. Bản cũ khẳng định chắc "Mã xác thực đã được gửi tới..." — **sai** ở trường hợp email đã tồn tại (nhánh đó không hề tạo mã, chỉ gửi email cảnh báo khác) |
| Gợi ý sửa lỗi gõ domain phổ biến ("Did you mean gmail.com?") — không chặn submit, không gọi mạng | `src/lib/emailDomainSuggest.ts` — so khớp Levenshtein distance ≤ 2 với danh sách domain phổ biến | Kỹ thuật của thư viện `mailcheck.js` (đã ngừng bảo trì 11 năm, KHÔNG dùng làm dependency — chỉ dùng lại thuật toán) |

**Vì sao không thêm MX-record lookup hay regex TLD chặt hơn**: đã research 3 lựa chọn (soft-suggestion / MX lookup lúc Register / không thêm gì) và
user chọn soft-suggestion. Lý do: `System.Net.Mail.MailAddress` được kiểm tra kỹ — chấp nhận cả
`user@example` (không có TLD) nên KHÔNG phải bản nâng cấp thật; MX-record lookup (DnsClient.NET) là
kỹ thuật thật duy nhất kiểm tra "đuôi domain" có tồn tại hay không, nhưng thêm 1 network call + độ trễ
vào luồng đăng ký và cần xử lý fail-open khi DNS timeout — không đáng đánh đổi vì bước xác thực OTP
qua email (đã có) mới là bằng chứng thật sự đáng tin (OWASP: semantic validation qua token xác thực,
không phải qua syntax check).

## 4. Số điện thoại (Phone) — optional, KHÔNG còn trên form Đăng ký

**Đã bỏ field Phone khỏi form Sign Up (2026-08-14), không phải bỏ khỏi hệ thống.** Backend
(`RegisterCommand.Phone`) vẫn nhận optional — chỉ frontend không hỏi field này ở bước đăng ký nữa,
luôn gửi `phone: null`. Lý do: field này hiển thị trên form nhưng KHÔNG được validate gì (xem 2 mục
bên dưới) — user test thử gõ `8412345678` và nhận ra form vẫn "bình thường" chấp nhận dù đó không
phải số hợp lệ. Research 2 hướng sửa: (a) thêm validate thật bằng `libphonenumber-js`/
`libphonenumber-csharp` (đã dùng ở SMS, xem [[musiclounge_sms_provider_switch_speedsms]]) — vẫn giữ
được "định dạng lỏng, nhiều cách gõ", chỉ từ chối chuỗi không phải số điện thoại thật; (b) bỏ field
khỏi form này, thu thập lại đúng lúc thực sự cần dùng (bước xác thực SĐT sau khi đăng nhập). User
chọn (b) — field không có tác dụng gì lúc đăng ký (không gửi SMS, không dùng để gì) nên tốt hơn là
không hứa hẹn 1 validate mà nó không có. Khi UI cho bước "Xác thực số điện thoại" (`/me/phone/
verification-code`) được thiết kế sau này, đó mới là nơi hỏi Phone thật, và nên áp dụng validate thật
(hướng a) ngay tại đó, vì lúc đó Phone mới thực sự được dùng để gửi SMS.

**Cố tình KHÔNG thêm regex format (VD E.164 hay format VN cụ thể).** Lý do: codebase đã có quyết
định kiến trúc từ trước (`PhoneNumberComparer.LooselyEquals`, dùng trong luồng tra cứu khiếu nại
khách vãng lai) — so khớp số điện thoại theo **9 số cuối** thay vì yêu cầu định dạng chính xác, vì hệ
thống chấp nhận cả `"0912345678"` lẫn `"+84912345678"`. Thêm regex cứng ở bước đăng ký sẽ mâu thuẫn
với triết lý "định dạng lỏng, so khớp theo hậu tố" đã áp dụng nhất quán ở các luồng khác. Chỉ giữ
`MaximumLength(20)` (đã có). OWASP Input Validation Cheat Sheet không đưa ra khuyến nghị cụ thể cho
phone number nên không có nguồn nào bị bỏ qua ở đây.

**Cố tình KHÔNG bắt buộc 1 số điện thoại = 1 tài khoản (uniqueness).** Hiện tại `Users.Phone` không
có unique index, và không có chỗ nào — kể cả `RequestPhoneVerificationCommandHandler`/
`VerifyPhoneCommandHandler` — kiểm tra số đã được tài khoản khác dùng/xác thực chưa. Đây là quyết
định có chủ đích, không phải thiếu sót, dựa trên 2 nguồn:
- **Nghị định 147/2024/NĐ-CP** (lý do `PhoneVerified` tồn tại) chỉ yêu cầu tài khoản phải có số điện
  thoại **đã xác thực** thì mới được đăng bài/bình luận/livestream — không yêu cầu số đó phải độc
  quyền cho 1 tài khoản.
- **Auth0** (nền tảng định danh cùng mô hình email-là-chính với MusicLounge, khác mô hình
  phone-là-chính của WhatsApp) cho phép nhiều tài khoản dùng chung 1 số điện thoại cho SMS/MFA
  **theo thiết kế** — lý do chính thức của Auth0: bắt buộc unique lại tạo ra rủi ro dò email/số
  (enumeration) kiểu mới, và phá vỡ các trường hợp hợp lệ như nhiều tài khoản trong 1 gia đình dùng
  chung 1 số.

Khác với Email (xem §3) — nơi 9 handler khác nhau (Login, VerifyEmail, ForgotPassword...) đều
`query Email == ...` và giả định trả về đúng 1 dòng, nên Email unique là yêu cầu **kỹ thuật bắt
buộc** để đăng nhập hoạt động được — không có handler nào tra cứu tài khoản bằng Phone, nên Phone
unique chỉ là 1 lựa chọn chính sách, không phải yêu cầu kỹ thuật.

## 5. Điều khoản dịch vụ (AcceptTerms)

| Rule | Giá trị | Nguồn |
|---|---|---|
| Phải là lựa chọn chủ động, KHÔNG được mặc định `true` | Checkbox mặc định unchecked | Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 — yêu cầu sự đồng ý rõ ràng (explicit affirmative consent) |

## 6. Accessibility (áp dụng cho toàn bộ form, không riêng field nào)

| Rule | Nguồn |
|---|---|
| Label/instruction cho format đặc biệt phải hiển thị TRƯỚC khi user nhập sai, không chỉ hiện ra sau lỗi | WCAG 2.2 SC 3.3.2 Labels or Instructions |
| Lỗi phải được xác định rõ bằng văn bản (không chỉ đổi màu viền) | WCAG 2.2 SC 3.3.1 Error Identification |
| `autocomplete` đúng chuẩn (`name`/`email`/`new-password`/`tel`) để trình quản lý mật khẩu hoạt động đúng | WHATWG HTML spec, autocomplete attribute; WCAG 2.2 SC 1.3.5 Identify Input Purpose |
| Input lỗi phải có `aria-invalid` + `aria-describedby` trỏ tới text lỗi; vùng lỗi/gợi ý động nên có `aria-live` | WCAG 2.2 (4.1.2 Name/Role/Value, best practice ARIA Authoring Practices) |

---

## 7. Rate limiting & chống automation (đăng ký hàng loạt)

| Rule | Giá trị hiện tại | Nguồn |
|---|---|---|
| Rate-limit theo IP trên toàn bộ `/auth/*` | 10 request/phút/IP | OWASP OAT-019 (rate limiting là 1 trong 2 lớp phòng thủ khuyến nghị) + NIST 800-63B-4 §3.2.2 |
| CAPTCHA / challenge-response | Chưa có — xem mục "Quyết định KHÔNG làm" ở §1 | OWASP OAT-019 (lớp phòng thủ thứ 2, đề xuất, chưa triển khai) |

---

← Liên quan: [06-compliance.md](06-compliance.md) (pháp lý/nghị định), [API-STANDARDS.md](API-STANDARDS.md) (response shape)
