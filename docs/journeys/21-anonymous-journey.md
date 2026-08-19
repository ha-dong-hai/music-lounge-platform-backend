# 21 — Journey của Khách chưa đăng nhập (Anonymous)

← [12-actors-and-authorization.md](12-actors-and-authorization.md) · [16-api-endpoint-catalog.md](16-api-endpoint-catalog.md) · [17-audience-journey.md](17-audience-journey.md) · [22-performer-presence.md](22-performer-presence.md) · → [23-view-catalog.md](23-view-catalog.md)

> **Actor**: "Khách chưa đăng nhập" — pseudo-actor cho endpoint `[AllowAnonymous]`, không có `User`/token nào. Actor thứ 5 (cuối) trong chuỗi chạy lần lượt Audience → Owner → Staff → Admin → Khách chưa đăng nhập.
>
> **Vì sao tài liệu này viết kỹ hơn các journey trước**: actor này dễ bị hiểu sai nhất khi thiết kế màn hình — vì ranh giới "công khai" không nằm gọn theo từng **trang**, mà nằm theo từng **API gọi bên trong 1 trang**. Có trang mà 90% nội dung xem được không cần đăng nhập nhưng 1 nút bấm trong đó lại chặn cứng. Mọi bước dưới đây ghi rõ 3 việc: (1) endpoint có **thật sự không cần token** hay không, (2) response có **thay đổi tuỳ theo có token hay không** dù endpoint không bắt buộc, (3) nếu bị chặn, chặn ở đâu và Audience đăng nhập rồi có thấy khác gì không.
>
> **Ký hiệu**: `[XEM]` = chỉ đọc · `[NHẬP]` = actor phải nhập dữ liệu · `[BẤM]` = trigger hành động · 🔀 = rẽ nhánh · ↔ = ảnh hưởng actor khác · 🚧 = **điểm chặn cứng, phải đăng nhập mới đi tiếp được** (không phải rẽ nhánh trong journey này — là lối thoát SANG journey Audience, xem [17](17-audience-journey.md) Journey 1).
>
> **Cập nhật**: 2026-08-13.

---

## Mục lục journey

1. Duyệt catalog công khai (venue, show, menu, hạng vé, tour 360°, gói subscription)
2. Xem minh bạch donate công khai
3. Gửi khiếu nại không cần tài khoản
4. Bản đồ đầy đủ các điểm chặn (🚧) — nơi journey này kết thúc và phải sang Audience

---

## Journey 1 — Duyệt catalog công khai

| # | Bước | Endpoint | Cần token? | I/O | Ghi chú thiết kế màn hình |
|---|---|---|---|---|---|
| 1 | Xem danh sách show đang mở / tìm kiếm / lọc / xem trending | `GET /lounge-shows`, `GET /lounge-shows/search`, `GET /lounge-shows/trending`, `GET /lounge-shows/filter-options`, `GET /lounge-shows/suggestions` | **Không cần** — hoạt động đầy đủ, không rút gọn dữ liệu | `[XEM]` | Tham số `mine=true` trên `GET /lounge-shows` **chỉ dùng được khi có token Owner** — **xác nhận lại 2026-08-17 bằng cách đọc trực tiếp `GetPublishedLoungeShowsQueryHandler`**: gọi `mine=true` mà không đăng nhập ném `UnauthorizedException` (401), **không** phải im lặng trả về như `mine=false` như tài liệu này từng ghi sai — với khách chưa đăng nhập, đừng hiện toggle/tab "Show của tôi" trên màn hình này, nếu không sẽ ra lỗi 401 khi bấm |
| 2 | Xem chi tiết 1 show | `GET /lounge-shows/{id}` | **Không cần** — nhưng đánh dấu "Optional Auth" (`SwaggerOptionalAuth`) trong code | `[XEM]` | ⚠️ **Chưa xác minh được** trong lượt phân tích này liệu response có field nào chỉ hiện khi có token hay không (vd nút quản trị ẩn/hiện). Không giả định — nếu team FE build 2 layout khác nhau (khách vs chủ show) cho cùng trang này, cần xác nhận lại với BE field-by-field trước khi code, đừng suy đoán từ tên "Optional Auth" |
| 3 | Xem sơ đồ chỗ ngồi của show | `GET /lounge-shows/{id}/seating-map` | Không cần | `[XEM]` | |
| 4 | Xem show theo nghệ sĩ / theo venue | `GET /lounge-shows/by-performer/{id}`, `GET /lounge-shows/by-lounge/{id}` | Không cần | `[XEM]` | |
| 5 | Xem danh sách/chi tiết venue | `GET /lounges`, `GET /lounges/{id}` | Không cần (cũng Optional Auth — cùng lưu ý ⚠️ như bước 2) | `[XEM]` | |
| 6 | Xem khu vực chỗ ngồi của venue | `GET /lounges/{id}/zones` | Không cần | `[XEM]` | |
| 7 | Xem tour ảo 360° của venue | `GET /lounges/{id}/tour` | Không cần | `[XEM]` | Toàn bộ trải nghiệm tour 360° (kéo thả xem panorama, click hotspot) là công khai hoàn toàn, không có phần nào trong đó cần đăng nhập |
| 8 | Xem hạng vé + giá của 1 show | `GET /ticket-tiers?showId=`, `GET /ticket-tiers/{id}` | Không cần | `[XEM]` | Xem được **giá và số lượng còn lại** mà không cần đăng nhập — màn hình chi tiết show/vé có thể hiện đầy đủ bảng giá cho khách vãng lai, chỉ nút "Mua ngay" mới cần chặn (xem Journey 4) |
| 9 | Xem menu F&B của venue | `GET /fnb-menus?loungeId=`, `GET /fnb-menu-items?menuId=` | Không cần | `[XEM]` | |
| 10 | Xem bảng giá gói subscription (thường dành cho Owner tương lai) | `GET /subscriptions/packages` | Không cần | `[XEM]` | Trang "bảng giá dành cho chủ phòng trà" hoàn toàn xem được trước khi đăng ký tài khoản Owner |

**🚧 Điểm chặn ngay trong journey này**: mọi hành động **ghi** liên quan tới các trang trên (follow venue, thêm wishlist, giữ chỗ/mua vé, đặt F&B) đều đòi hỏi đăng nhập — xem Journey 4 để có danh sách đầy đủ, tránh liệt kê rời rạc ở từng bước.

---

## Journey 2 — Xem minh bạch donate công khai

Đây là journey **dễ nhầm lẫn nhất khi thiết kế màn hình** vì có **2 cơ chế khác nhau, độc lập nhau**, cùng gắn nhãn "công khai" nhưng phục vụ 2 loại màn hình khác nhau.

| # | Bước | Endpoint / Kênh | Cần token? | I/O | Ghi chú thiết kế màn hình |
|---|---|---|---|---|---|
| 1 | Xem **sổ lịch sử** donate toàn hệ thống (trang tra cứu, có phân trang) | `GET /donations/public` (REST) | Không cần | `[XEM]` | Đây là **trang tĩnh, tải theo trang** — không tự cập nhật realtime. Chỉ chứa donate đã "chốt" (`OwnerReceived`/`PerformerPaid`) — donate vừa thanh toán xong (`PendingOwnerAck`) **chưa xuất hiện ở đây**, dù đã xuất hiện ở bước 3 bên dưới. Đừng thiết kế 2 nơi này dùng chung 1 component/1 nguồn dữ liệu rồi thắc mắc sao "thấy ở chỗ này mà không thấy ở chỗ kia" — đó là hành vi cố ý (xem [15-risk-audit.md](15-risk-audit.md)) |
| 2 | Xem lịch sử donate của **1 nghệ sĩ cụ thể** | `GET /performers/{performerId}/donations` (REST) | Không cần | `[XEM]` | Khác bước 1 ở chỗ: **không có breakdown phí** (chỉ `Gross`, không có `Net`/`PlatformFee`/`PerformerAmount`...) — nếu team FE định dùng chung 1 component hiển thị cho cả trang "sổ toàn hệ thống" và trang "trang cá nhân nghệ sĩ", phải tự ẩn cột breakdown phí khi render từ endpoint này, component không tự biết |
| 3 | Xem **ticker/overlay realtime** donate của 1 show cụ thể đang diễn ra | SignalR `PublicDonationHub` (không phải REST) | **Không cần** — nhưng phải truyền `loungeShowId` lúc kết nối, thiếu thì bị `Context.Abort()` | `[XEM]`, cập nhật liên tục | 🔑 **Điểm quan trọng nhất của journey này**: kết nối được vào ticker này **không đòi hỏi xem được video livestream** — đây là 2 hệ thống hoàn toàn tách biệt (`PublicDonationHub` vs `LivestreamHub`, xem 🚧 Journey 4). Có thể thiết kế 1 khối "donate gần đây" hiện ngay trên trang chi tiết show công khai ([Journey 1 bước 2](#journey-1--duyệt-catalog-công-khai)) cho khách vãng lai xem, **dù họ không xem được video** — đừng mặc định phải có quyền xem live mới cho thấy ticker donate |

**So sánh nhanh 3 nguồn trên (đừng nhầm khi giao việc cho FE)**:

| | `GET /donations/public` | `GET /performers/{id}/donations` | `PublicDonationHub` |
|---|---|---|---|
| Loại màn hình | Trang tra cứu, phân trang | Trang cá nhân nghệ sĩ | Widget/overlay realtime trên 1 show |
| Phạm vi | Toàn hệ thống, mọi nghệ sĩ | 1 nghệ sĩ | 1 show cụ thể |
| Breakdown phí | Có đầy đủ | Không | Không (chỉ tên/số tiền/tin nhắn, đã lọc theo cờ riêng tư) |
| Trạng thái donate hiện | `OwnerReceived`, `PerformerPaid` | `OwnerReceived`, `PerformerPaid` | Cả 3: `PendingOwnerAck`, `OwnerReceived`, `PerformerPaid` |
| Cần xem video live? | Không | Không | **Không** (hay nhầm) |

---

## Journey 3 — Gửi khiếu nại không cần tài khoản

> **Cập nhật 2026-08-13**: gap "gửi xong không tra lại được" đã được vá — xem bước 2-3. Research thực tế (SOP backend research, cùng ngày) xác nhận đây đúng 2 pattern chuẩn ngành: **"kéo"** (guest order tracking TMĐT — Order Number + Email/Zip, Baymard Institute 165 ví dụ nghiên cứu) và **"đẩy"** (ticketing dạng email — Zendesk/Freshdesk/Help Scout chủ động báo lại qua đúng kênh khách đã cho). MusicLounge làm cả 2 vì đã có sẵn `ContactPhone` + `ISmsService`, không phải cơ chế tự nghĩ ra. Chi tiết đầy đủ: [15-risk-audit.md finding #16](15-risk-audit.md#2-đã-tìm--đã-sửa).

| # | Bước | Endpoint | Cần token? | I/O | Ghi chú thiết kế màn hình |
|---|---|---|---|---|---|
| 1 | Điền form khiếu nại | `POST /complaints` | **Không cần** (`AllowAnonymous` + `SwaggerOptionalAuth`) | `[NHẬP]` TargetType, TargetId/TargetGuid, Category, ContactPhone... → `[BẤM]` | Response trả về `int id` — **màn hình phải lưu lại giá trị này cho khách** (hiện lên màn hình xác nhận, không chỉ log nội bộ) vì đó là nửa đầu của cặp khoá tra cứu ở bước 2 |
| 2 | Tự tra lại kết quả (khách chủ động quay lại) | `GET /complaints/lookup?id=&phone=` | **Không cần** (`AllowAnonymous`) | `[NHẬP]` id + số điện thoại đã dùng lúc gửi → `[XEM]` | So khớp `id` + `ContactPhone` (nới lỏng định dạng, "0912345678" và "+84912345678" tính là khớp) — sai 1 trong 2 hay `id` không tồn tại đều trả **404 giống hệt nhau**, không phân biệt lý do (chống dò). Có rate-limit policy "auth" (10 req/phút/IP, dùng lại từ login) — **đừng thiết kế UI cho phép bấm submit liên tục không giới hạn**, sẽ sớm bị 429. Trái với route `GET /complaints/{id}` (bước 2 dành cho Audience đã đăng nhập ở [17](17-audience-journey.md)) — 2 route hoàn toàn tách biệt, khách vãng lai **không** dùng được route kia |
| 3 | ↔ *(chủ động từ hệ thống, không cần khách quay lại)* Nhận SMS khi Admin xử lý xong | — (SMS, không phải API khách gọi) | — | `[XEM]` (ngoài app) | Khi Admin `POST /admin/complaints/{id}/resolve`, hệ thống tự gửi SMS tới đúng `ContactPhone` đã nhập ở bước 1 — khách **không cần chủ động vào lại app** để biết kết quả. 2 cơ chế (2 và 3) độc lập, bổ sung nhau: 3 là "đẩy" (không cần thao tác gì), 2 là "kéo" (chủ động xem chi tiết đầy đủ hơn tin SMS) |

---

## Journey 4 — Bản đồ đầy đủ các điểm chặn 🚧 (nơi journey này kết thúc)

Tổng hợp **mọi** hành động khách vãng lai chạm tới nhưng bị chặn cứng, kèm đúng thông điệp nên hiện lên màn hình tại từng điểm — tránh mỗi màn hình tự chế 1 câu khác nhau.

| Hành động khách vãng lai cố thực hiện | Chặn ở đâu | Có thấy trước khi bị chặn không? |
|---|---|---|
| Giữ chỗ / mua vé | `POST /tickets/holds`, `POST /tickets/purchase` — `RequireAuthenticated` | ✅ Thấy đầy đủ giá/số lượng còn lại trước đó ([J1 bước 8](#journey-1--duyệt-catalog-công-khai)) — chỉ chặn đúng lúc bấm "Giữ chỗ"/"Mua" |
| Donate cho nghệ sĩ | `POST /donations` — `RequireAuthenticated` | ✅ Thấy sổ donate công khai trước đó ([J2](#journey-2--xem-minh-bạch-donate-công-khai)) nhưng không tự donate được nếu chưa đăng nhập |
| **Xem video + chat livestream** | `GET /livestreams/{id}` (REST, lấy thông tin phát) — `RequireAuthenticated`; **và** kết nối `LivestreamHub` (SignalR) đòi hỏi phải là **chủ vé thật** (`isGenuineTicketHolder`) — chặt hơn cả "chỉ cần đăng nhập" | ❌ Không thấy được gì — kể cả đăng nhập rồi mà chưa có vé show đó vẫn bị `Context.Abort()`. **Khác hẳn** ticker donate ở J2 bước 3 — đừng lẫn 2 cái này khi thiết kế nút "Xem trực tiếp" |
| Đặt F&B | `POST /fnb-orders` — `RequireAuthenticated` | ✅ Thấy menu đầy đủ trước đó ([J1 bước 9](#journey-1--duyệt-catalog-công-khai)) |
| Follow venue / thêm wishlist | `POST /follows/lounges/{id}`, `POST /wishlist/{showId}` — `RequireAuthenticated` | ✅ Thấy venue/show trước đó |
| Đánh giá show | `POST /lounge-shows/{id}/rate` — `RequireAuthenticated` + phải có vé | ✅ Thấy show trước đó |
| Xem thông báo, hồ sơ cá nhân | Toàn bộ `/me/*`, `/notifications/*` — `RequireAuthenticated` | ❌ Không áp dụng — khách vãng lai không có gì để xem ở đây |

**🔀 Lối ra duy nhất khỏi mọi điểm chặn**: đăng nhập/đăng ký ([17 Journey 1](17-audience-journey.md#journey-1--đăng-ký--thiết-lập-tài-khoản)) — sau khi có token, hành động vừa bị chặn **thường thực hiện lại được ngay** mà không mất trạng thái đang xem (vd đang xem show A → bị chặn lúc bấm Mua vé → đăng nhập xong → quay lại đúng show A để mua tiếp), nhưng đây là hành vi kỳ vọng ở tầng FE (giữ lại context trước khi điều hướng sang màn hình đăng nhập), **không phải cơ chế nào ở backend tự động làm giúp** — API không có khái niệm "return URL" hay tự resume hành động dang dở.
