# View Design Spec — MusicLounge Backend-ForFE

> **Tài liệu tổng hợp cuối cùng**, gộp toàn bộ kết quả 4 bước phân tích trước: kiểm kê endpoint ([16-api-endpoint-catalog.md](16-api-endpoint-catalog.md)), journey theo actor ([17](17-audience-journey.md)-[22](22-performer-presence.md)), danh mục view chi tiết ([23-view-catalog.md](23-view-catalog.md)), sơ đồ luồng + giao thoa ([24-view-flow-diagrams.md](24-view-flow-diagrams.md)). Không suy đoán thêm field/endpoint nào ngoài những gì 4 tài liệu nguồn đã xác minh — mọi chỗ còn mơ hồ được gom về [§5](#5-danh-sách-giả-định-cần-xác-nhận-lại-với-đội-thiết-kếdev) thay vì tự quyết định thay đội thiết kế.
> **Cập nhật**: 2026-08-14 (bổ sung §1.5 kiến trúc nền tảng web/mobile).
> **Đưa vào Stitch để thiết kế UI**: tài liệu này viết cho dev/BA, không phù hợp dán thẳng vào công cụ thiết kế AI (tên field raw, ghi chú kỹ thuật BE-only). Bản đã dịch lại thành **[Stitch-Master-Brief.md](Stitch-Master-Brief.md)** — 1 file gộp đủ 5 design brief theo **surface** (xem [platform-architecture.md](platform-architecture.md)): Audience Web, Audience Mobile (F&B), Owner Web, Staff Mobile, Admin Web. Mỗi surface là 1 project Stitch riêng — đưa từng phần, không đưa cả file 1 lần.

## Mục lục

1. [Tổng quan](#1-tổng-quan) — gồm [§1.5 Nền tảng web/mobile](#15-nền-tảng-webmobile)
2. [Danh sách chi tiết từng view (theo domain nghiệp vụ)](#2-danh-sách-chi-tiết-từng-view-theo-domain-nghiệp-vụ) — 73 view, 27 nhóm
3. [Sơ đồ luồng điều hướng theo từng role](#3-sơ-đồ-luồng-điều-hướng-theo-từng-role)
4. [Sơ đồ các điểm giao thoa/tương tác real-time giữa các role](#4-sơ-đồ-các-điểm-giao-thoatương-tác-real-time-giữa-các-role) — 19 điểm
5. [Danh sách giả định cần xác nhận lại với đội thiết kế/dev](#5-danh-sách-giả-định-cần-xác-nhận-lại-với-đội-thiết-kếdev)

---

## 1. Tổng quan

### 1.1 Phạm vi & phương pháp

Toàn bộ 73 view dưới đây được suy luận **từ dưới lên**: đọc trực tiếp 25 class Controller (~209 endpoint) → dựng journey theo từng bước thật của 5 actor (Audience, Owner, Staff, Admin, Khách chưa đăng nhập) + 1 entity đặc biệt không có tài khoản (Performer) → từ mỗi bước journey suy ra 1 view nếu bước đó có mục đích hiển thị/tương tác riêng biệt, gộp lại nếu nhiều bước dùng chung 1 trang thật. Không có bước nào trong journey bị bỏ sót — 1 lần bỏ sót thật đã xảy ra (Owner tạo/sửa Menu F&B, journey 18 §9 bước 1-2) và được phát hiện + vá ngay khi dựng sơ đồ luồng ở bước kế tiếp (72→73 view), ghi lại ở [§2 nhóm AA](#aa-owner--quản-lý-menu-fb) làm bài học quy trình.

**Quy ước áp dụng xuyên suốt tài liệu này**:
- `RequireAuthenticated` (chính sách phân quyền) chấp nhận **bất kỳ role nào đã đăng nhập**, không riêng Audience — về mặt kỹ thuật 1 tài khoản Owner cũng gọi được `POST /tickets/holds`. Bảng "Actor truy cập" ở §2 và số liệu ở §1.3 ghi theo **actor chính/dự kiến dùng thật** (theo đúng journey đã dựng ở docs/17-22), không phải theo khả năng kỹ thuật thuần tuý — tránh nhầm "Audience-primary" thành "chỉ Audience mới vào được".
- DTO nào chưa từng được mở file đọc trực tiếp field (chỉ thấy qua `ProducesResponseType<>`) được giữ nguyên cảnh báo **"(chưa xác minh field cụ thể)"** — liệt kê đầy đủ ở [§5.1](#51-dto-response-cần-xác-minh-lại-field-với-be).

### 1.2 Số lượng view theo domain nghiệp vụ

| Nhóm | Domain | Số view |
|---|---|---|
| A | Auth & Tài khoản | 8 |
| B | Thông báo | 1 |
| C | Khám phá công khai | 6 |
| D | Follow/Wishlist | 1 |
| E | Vé (Audience) | 5 |
| F | Livestream & Donate | 6 |
| G | F&B (khách đặt) | 2 |
| H | Đánh giá | 1 |
| I | Khiếu nại | 3 |
| J | Owner — Venue | 6 |
| K | Owner — Bank Account | 1 |
| L | Owner — Subscription | 2 |
| M | Owner — Show | 6 |
| N | Owner/Staff — Livestream ops | 1 |
| O | Owner — Donate handling | 2 |
| P | Owner — Tài chính | 2 |
| Q | Owner — Xử phạt | 1 |
| R | Owner/Admin — Performer | 1 |
| S | Staff — Vận hành sàn | 3 |
| T | Admin — Duyệt/Kiểm duyệt | 2 |
| U | Admin — Xử phạt & Kháng cáo | 2 |
| V | Admin — Tài chính | 4 |
| W | Admin — Khiếu nại | 1 |
| X | Admin — Người dùng | 1 |
| Y | Admin — Taxonomy & Subscription | 2 |
| Z | Admin — Giám sát | 2 |
| AA | Owner — Quản lý Menu F&B | 1 |
| **Tổng** | | **73** |

### 1.3 Số lượng view theo actor

Đếm theo actor **chính** sử dụng view đó trong journey đã dựng (xem quy ước 1.1). 1 view có thể tính cho nhiều actor nếu nhiều journey cùng dùng thật:

| Actor | Số view truy cập được | Ghi chú |
|---|---|---|
| **Khách chưa đăng nhập (Anonymous)** | 15 | Toàn bộ view `AllowAnonymous`/`Optional Auth` không cần token — duyệt catalog, minh bạch donate, gửi/tra cứu khiếu nại, màn hình auth trước khi có tài khoản |
| **Audience** | 22 | Mua vé, donate, F&B, đánh giá, khiếu nại, quản lý tài khoản — không tính các view public mà Audience vẫn xem được y hệt Anonymous (đã tính riêng ở dòng trên) |
| **Owner** | 36 | Nhóm đông nhất — quản lý venue/show/vé/nhân viên/tài chính/donate cho nghệ sĩ, chiếm gần một nửa tổng số view (phản ánh đúng việc Owner là actor có nhiều nghiệp vụ nhất hệ thống) |
| **Staff** | 11 | Chủ yếu vận hành tại chỗ (bán vé quầy, check-in, F&B), dùng chung nhiều view quản lý show/livestream với Owner ở mức giới hạn hơn |
| **Admin** | 21 | Duyệt/kiểm duyệt/xử phạt/tài chính/quản trị người dùng — gần như toàn bộ nhóm T-Z |

*Tổng 105 lượt actor-view (>73 vì có view dùng chung) — xem 1.4.*

### 1.4 View dùng chung giữa nhiều role

**18/73 view (24.7%)** được từ 2 actor trở lên cùng truy cập, chia 2 loại:

- **10 view dùng chung CÓ khác biệt hiển thị/quyền thật sự** (nội dung hoặc nút hành động đổi theo role) — bảng chi tiết dưới đây.
- **8 view dùng chung nhưng KHÔNG có khác biệt** (mọi role thấy y hệt nhau, chỉ khác ở việc dữ liệu là "của ai"): Hồ sơ cá nhân, Xác thực định danh (KYC), Quyền riêng tư & Dữ liệu cá nhân, Khiếu nại của tôi (4 view dùng chung cả AU/OW/ST/AD); Kết quả thanh toán (AU+OW, khác nhau ở *loại giao dịch* chứ không phải *quyền*); Gửi khiếu nại (AN+AU); Bán vé tại quầy (Walk-in), Quản lý đơn F&B tại quầy (cả 2: OW+ST, cùng quyền `RequireVenueOperator`, không khác gì nhau).

**10 view có khác biệt thật (chi tiết đầy đủ)**:

| View | Role dùng chung | Khác biệt cụ thể |
|---|---|---|
| **Chi tiết Show** | Anonymous, Audience, Owner (chủ show) | Field hiển thị giống nhau; bộ **nút hành động** khác hẳn: Anonymous chỉ xem; Audience có vé + show `Ongoing` → thêm nút "Vào xem live"; Audience có vé + show `Ended` trong hạn 7 ngày, chưa đánh giá → thêm nút "Đánh giá"; Owner sở hữu show → thêm link sang **Bảng điều khiển Show (Owner)**. ⚠️ docs/16 đánh dấu `Optional Auth` có thể có thêm field riêng cho chủ sở hữu nhưng **chưa xác minh field cụ thể** — đừng giả định trước khi hỏi lại BE. |
| **Danh sách/Tìm kiếm Show** | Anonymous, Audience, Owner | Mặc định (không `mine=true`, hoặc chưa đăng nhập): chỉ thấy `Published`. Owner đăng nhập + `mine=true`: thấy thêm `Draft`/`Pending` của chính mình. Tham số `mine=true` **ném lỗi 401** (`UnauthorizedException`) nếu gọi không có token — xác nhận trực tiếp `GetPublishedLoungeShowsQueryHandler` 2026-08-17, sửa lại claim cũ sai ("im lặng bị bỏ qua") — UI không nên hiện toggle "Show của tôi" khi chưa đăng nhập Owner, nếu không sẽ ra lỗi khi bấm. |
| **Danh sách Venue** | Anonymous, Owner | Cùng logic `mine=true` như trên — Owner thấy cả venue `Pending`/`Rejected` của mình, công khai chỉ thấy venue đã duyệt. |
| **Chi tiết Venue** | Anonymous, Owner | ⚠️ Đánh dấu `Optional Auth`, docs/16 ghi "khả năng Owner xem thêm field quản trị" nhưng **chưa xác minh field cụ thể nào khác nhau** — cần xác nhận lại với BE trước khi thiết kế 2 layout khác nhau cho cùng view này. |
| **Hộp thư thông báo** | Mọi actor đã đăng nhập (Audience/Owner/Staff/Admin) | Field/layout giống nhau; khác ở **loại thông báo nhận được** — Admin nhận thêm nhóm cảnh báo vận hành/bảo mật nội bộ (`SettlementSchedulingBlocked`, cảnh báo từ 4 job giám sát) mà Audience/Owner/Staff không bao giờ thấy. |
| **Phòng xem Livestream** | Audience (chủ vé thật), Owner/Staff (vận hành venue đó), Admin (luôn xem) | Khác nhau ở **điều kiện vào được**, không phải field: Audience cần `isGenuineTicketHolder` + trong giới hạn thiết bị đồng thời; Owner/Staff cần đúng venue mình vận hành (`RequireVenueOperator`); Admin luôn qua được (`LivestreamAccessPolicy`). Riêng Admin thấy thêm nút "Buộc dừng phát" (`terminate`) mà 3 actor kia không có. |
| **Bảng điều khiển Show (Owner)** | Owner (đầy đủ), Staff (1 phần) | Owner thấy đầy đủ mọi nút (nộp duyệt/đổi lịch/đổi format/huỷ/start/end). Staff **chỉ** dùng được nút bắt đầu/kết thúc show (`RequireVenueOperator`) — các nút còn lại (`RequireOwner`) nên **ẩn hẳn**, không chỉ disable, với tài khoản Staff. |
| **Vận hành Livestream** | Owner, Staff (cùng quyền `RequireVenueOperator`) | Không khác biệt hiển thị giữa Owner/Staff ở view này — cả 2 làm được mọi hành động (tạo/credentials/start/end). Khác với **Bảng điều khiển Show (Owner)** ở trên, nơi Staff bị giới hạn nhiều hơn. |
| **Quản lý Hồ sơ Nghệ sĩ** | Mọi Owner (CREATE/READ/ASSIGN), người tạo + Admin (EDIT/DELETE) | Mọi Owner thấy toàn bộ catalog và gán được nghệ sĩ bất kỳ vào show mình — nhưng nút "Sửa"/"Xoá" chỉ hiện với đúng `CreatedByUserId` hoặc Admin (kiểm tra ở tầng handler, không phải policy — UI phải tự so `CreatedByUserId` với user hiện tại để ẩn nút, không dựa vào response 403 mới ẩn). |
| **Bảng giá gói Subscription** | Anonymous, Audience, Owner | Field/nội dung giống hệt nhau (công khai hoàn toàn, không cần login để xem giá) — chỉ khác ở nút "Chọn gói": với Anonymous/Audience dẫn sang Đăng ký/Đăng nhập trước, với Owner dẫn thẳng vào khởi tạo thanh toán ở **Gói Subscription của tôi**. |

---

### 1.5 Nền tảng (Web/Mobile)

> Bổ sung 2026-08-14, sau khi §1-4 đã dựng xong. Backend (REST + SignalR) không ép buộc client nào — mobile hay web đều gọi được y hệt API; việc chia dưới đây là **quyết định sản phẩm**, không phải giới hạn kỹ thuật. Chi tiết đầy đủ + lý do từng dòng: [platform-architecture.md](platform-architecture.md).

| Surface | Actor | Loại | Số view | Vì sao |
|---|---|---|---|---|
| **Audience Web** | Audience | Website (responsive) | 30 (nhóm A-F, H-I, **trừ** G) | Phần lớn xem hòa nhạc là đến trực tiếp venue, không phải theo dõi liên tục qua app — khám phá/mua vé/xem live/donate/quản lý vé không cần app riêng; QR vé mở được bình thường qua trình duyệt điện thoại ở cửa. |
| **Audience Mobile (F&B)** | Audience | App native (iOS/Android) | 2 (nhóm G) | Đúng 1 tình huống thật cần "điện thoại cầm tay, tại bàn, ngay lúc đó": đặt đồ ăn/thức uống khi đang ngồi xem trực tiếp tại venue. Phạm vi cố ý rất hẹp, không nhồi thêm tính năng khác. |
| **Owner Web** | Owner | Website (dashboard) | 36 (nhóm J-Q, AA + phần Owner trong N, R) | Nghiệp vụ nặng: form nhiều field, Zone Editor kéo-thả, dashboard tài chính — công việc kiểu bàn làm việc. |
| **Staff Mobile** | Staff | App native/tablet | 11 (nhóm S + phần Staff trong M, N) | Vận hành tại chỗ, di chuyển trong venue: bán vé quầy, quét QR cửa, quản lý đơn F&B bếp/quầy. |
| **Admin Web** | Admin | Website (console) | 21 (nhóm T-Z + phần Admin trong F, R) | Công cụ back-office: hàng chờ kiểm duyệt, đối soát sổ cái, quản lý người dùng — bảng dữ liệu dày, cross-reference nhiều nguồn. |

**Ảnh hưởng tới §2 bên dưới**: mọi view thuộc nhóm **G (F&B khách đặt)** là scope của app mobile riêng, tách hẳn khỏi 30 view Audience còn lại (web) — xem ghi chú ngay đầu nhóm G. Owner/Staff/Admin **không đổi** so với giả định ban đầu ở §1.1-1.4.

**Stitch design brief theo surface**: đủ cả 5 surface, gộp 1 file tại [Stitch-Master-Brief.md](Stitch-Master-Brief.md) (hoặc dùng riêng từng file: [stitch-brief-audience-web.md](stitch-brief-audience-web.md), [stitch-brief-audience-mobile-fnb.md](stitch-brief-audience-mobile-fnb.md), [stitch-brief-owner-web.md](stitch-brief-owner-web.md), [stitch-brief-staff-mobile.md](stitch-brief-staff-mobile.md), [stitch-brief-admin-web.md](stitch-brief-admin-web.md)).

---

## 2. Danh sách chi tiết từng view (theo domain nghiệp vụ)

> 73 view, nhóm A-AA theo domain nghiệp vụ. Cấu trúc mỗi view: Actor truy cập / Mục đích / Loại view / Dữ liệu hiển thị (map endpoint+DTO thật) / Hành động khả dụng (map endpoint+field+validate BE) / Điều kiện hiển thị có điều kiện (trích business rule) / Điều hướng đến-đi / Trạng thái đặc biệt cần thiết kế.
> **Nền tảng**: xem [§1.5](#15-nền-tảng-webmobile) cho map đầy đủ theo surface — riêng nhóm **G** (F&B khách đặt) là app mobile native tách biệt, mọi nhóm Audience khác (A-F, H-I) là website. Owner/Staff/Admin giữ nguyên như phân theo actor.

### A. Auth & Tài khoản

#### Đăng ký tài khoản
- **Actor truy cập:** Ai cũng vào được (chưa có tài khoản) — chọn Role Audience hoặc Owner ngay trên form, không có form Staff/Admin riêng (2 role đó không tự đăng ký được).
- **Mục đích:** Tạo tài khoản mới.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Không có dữ liệu tải sẵn — form trắng.
- **Hành động khả dụng trên view:** `POST /auth/register` — nhập Email, Password, FullName, Phone?, AcceptTerms, Role ("Audience"|"Owner"). BE trả `RegisterResultDto` (chưa xác minh field cụ thể).
- **Điều kiện hiển thị có điều kiện:** Không có nút "Đăng nhập ngay" sau khi submit — tài khoản tạo xong ở trạng thái **chưa xác thực, chưa có token** (khác nhiều app khác tự đăng nhập luôn sau đăng ký).
- **Điều hướng đến (From):** Trang chủ công khai / màn hình Đăng nhập (link "Chưa có tài khoản?").
- **Điều hướng đi (To):** Thành công → **Xác thực Email OTP**. Lỗi (email trùng, password yếu...) → ở lại form, hiện lỗi field-level.
- **Trạng thái đặc biệt cần thiết kế:** Loading khi submit; lỗi 400 hiện đúng field bị FluentValidation reject (không phải toast chung chung).

#### Xác thực Email OTP
- **Actor truy cập:** Người vừa đăng ký (chưa xác thực).
- **Mục đích:** Nhập mã OTP gửi về email để kích hoạt tài khoản và nhận token đăng nhập lần đầu.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Email đã đăng ký (điền sẵn/hiện lại), hướng dẫn "mã đã gửi tới ...".
- **Hành động khả dụng trên view:** `POST /auth/verify-email` (Email, Code) → nhận `AuthResultDto` (token). Nút phụ "Gửi lại mã": `POST /auth/resend-verification-code` (Email) — luôn 204 dù email tồn tại hay không.
- **Điều kiện hiển thị có điều kiện:** Không giới hạn số lần bấm "Gửi lại mã" ở tầng UI ghi nhận được từ BE (không có field cooldown trong response) — nếu cần chặn spam, phải tự thêm debounce ở FE.
- **Điều hướng đến (From):** Đăng ký tài khoản (ngay sau submit thành công).
- **Điều hướng đi (To):** Thành công → có token, vào thẳng trang chủ đã đăng nhập (Audience) hoặc **Danh sách Venue của tôi** (Owner, vì chưa có venue nào). Mã sai/hết hạn → ở lại, hiện lỗi + nút gửi lại.
- **Trạng thái đặc biệt cần thiết kế:** Loading; lỗi "mã sai/hết hạn" phân biệt rõ 2 trường hợp nếu BE trả message khác nhau (chưa xác minh — kiểm tra lại message thật trước khi code cứng 2 case UI khác nhau).

#### Đăng nhập
- **Actor truy cập:** Mọi actor có tài khoản (Audience/Owner/Staff/Admin) — cùng 1 form, không tách theo role.
- **Mục đích:** Xác thực và lấy token.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Form trắng + nút "Đăng nhập bằng Google".
- **Hành động khả dụng trên view:** `POST /auth/login` (Email, Password) hoặc `POST /auth/google` (IdToken từ Google SDK, AcceptTerms — chỉ bắt buộc lần đầu với tài khoản Google mới) → `AuthResultDto`.
- **Điều kiện hiển thị có điều kiện:** Field `AcceptTerms` trên nhánh Google **chỉ cần hiện checkbox** nếu BE báo đây là tài khoản Google mới (chưa có cách phân biệt trước khi gọi — FE có thể phải luôn hiện checkbox rồi bỏ qua nếu không cần, hoặc gọi trước 1 bước "kiểm tra tồn tại" — chưa xác minh có endpoint tách riêng, khả năng phải hiện sẵn checkbox mọi lần).
- **Điều hướng đến (From):** Trang chủ / bất kỳ view nào bị chặn 🚧 vì chưa đăng nhập (xem [21-anonymous-journey.md Journey 4](21-anonymous-journey.md#journey-4--bản-đồ-đầy-đủ-các-điểm-chặn--nơi-journey-này-kết-thúc)).
- **Điều hướng đi (To):** Thành công → quay lại đúng view đang định vào trước khi bị chặn (FE tự giữ return-URL, BE không có cơ chế này) hoặc trang chủ theo role. Tài khoản chưa xác thực email → **Xác thực Email OTP**. Tài khoản `LockedUntil`/`IsActive=false` → thông báo lỗi tương ứng, không cho vào.
- **Trạng thái đặc biệt cần thiết kế:** Lỗi cố ý **mơ hồ** giữa "sai email" và "sai password" (chống dò tài khoản — xem `LoginCommandHandler` timing-attack defense đã ghi ở docs/12) — **không được** thiết kế 2 message riêng biệt dù có vẻ thân thiện hơn.

#### Quên/Đặt lại mật khẩu
- **Actor truy cập:** Ai cũng vào được từ màn Đăng nhập.
- **Mục đích:** Khôi phục quyền truy cập khi quên mật khẩu.
- **Loại view:** form (2 bước trên cùng 1 flow).
- **Dữ liệu hiển thị:** Bước 1: form nhập email. Bước 2 (từ link trong email): form nhập mật khẩu mới.
- **Hành động khả dụng trên view:** Bước 1: `POST /auth/forgot-password` (Email) → luôn 204. Bước 2: `POST /auth/reset-password` (Token từ link email, NewPassword) → 204.
- **Điều kiện hiển thị có điều kiện:** Bước 1 **luôn** hiện thông báo thành công dù email có tồn tại hay không (BE cố ý luôn 204) — **không được** thiết kế thông báo "email không tồn tại", sẽ lộ thông tin tài khoản.
- **Điều hướng đến (From):** Đăng nhập (link "Quên mật khẩu?").
- **Điều hướng đi (To):** Bước 1 xong → màn hình "kiểm tra email". Bước 2 thành công → **Đăng nhập**. Token hết hạn/sai → lỗi, không cho đặt lại.
- **Trạng thái đặc biệt cần thiết kế:** Không có empty/loading đặc biệt ngoài loading khi submit.

#### Hồ sơ cá nhân
- **Actor truy cập:** Mọi actor đã đăng nhập (Audience/Owner/Staff/Admin) — cùng 1 view, field như nhau cho mọi role.
- **Mục đích:** Xem/sửa thông tin cơ bản và đổi mật khẩu.
- **Loại view:** detail + form.
- **Dữ liệu hiển thị:** `GET /me` → `UserProfileDto` (chưa xác minh field cụ thể — khả năng cao gồm FullName, Email, Phone, AvatarUrl, DateOfBirth, Role, CreatedAt dựa theo field `PUT /me/profile` chấp nhận).
- **Hành động khả dụng trên view:** `PUT /me/profile` (FullName, Phone, AvatarUrl, DateOfBirth) → 204. `PUT /me/password` (CurrentPassword, NewPassword) → 204, chỉ áp dụng tài khoản đăng nhập bằng mật khẩu. Đổi email: `POST /me/email/change-request` (NewEmail) rồi `POST /me/email/change-confirm` (Code) — 2 bước.
- **Điều kiện hiển thị có điều kiện:** Ẩn hẳn khối "Đổi mật khẩu" nếu tài khoản đăng nhập qua Google (`AuthProvider="google"`, không có `PasswordHash`) — hiện thay bằng ghi chú "Tài khoản liên kết Google".
- **Điều hướng đến (From):** Menu tài khoản (mọi nơi trong app đã đăng nhập).
- **Điều hướng đi (To):** Lưu thành công → ở lại view, toast xác nhận. Link phụ tới **Xác thực định danh (KYC)**, **Cài đặt sở thích gợi ý AI**, **Quyền riêng tư & Dữ liệu cá nhân**.
- **Trạng thái đặc biệt cần thiết kế:** Loading khi tải `GET /me`; lỗi mật khẩu hiện tại sai (400) hiện đúng field.

#### Xác thực định danh (KYC CCCD)
- **Actor truy cập:** Mọi actor đã đăng nhập, thường dùng nhiều nhất bởi Owner (đối soát bank account) — không bắt buộc với Audience.
- **Mục đích:** Nộp ảnh CCCD/CMND 2 mặt để xác minh danh tính.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Trạng thái đã nộp hay chưa (suy ra từ có/không `CitizenCardNumber` trong `GET /me` — chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** `POST /uploads/citizen-card-images` (×2, file mặt trước/sau, multipart) → nhận ref ảnh riêng tư (không phải URL công khai) → `POST /me/citizen-card` (CitizenCardNumber, FrontImageUrl, BackImageUrl) → 204. Xem lại ảnh đã nộp: `GET /me/citizen-card/{side}`.
- **Điều kiện hiển thị có điều kiện:** Ảnh xem lại được lấy qua endpoint riêng tư (không phải URL tĩnh) — **không được** cache/lưu URL ảnh này ở FE như ảnh công khai khác (avatar/poster).
- **Điều hướng đến (From):** Hồ sơ cá nhân (link).
- **Điều hướng đi (To):** Nộp xong → ở lại view, hiện trạng thái "Đã nộp, chờ đối chiếu" (Admin xem thủ công ở **Quản lý Người dùng**, không có bước "duyệt KYC" riêng — chỉ Admin tự xem khi cần).
- **Trạng thái đặc biệt cần thiết kế:** Upload progress cho 2 ảnh; lỗi định dạng file.

#### Cài đặt sở thích gợi ý AI
- **Actor truy cập:** Audience (chủ yếu) — về mặt kỹ thuật mọi actor đều gọi được (`RequireAuthenticated`) nhưng chỉ có ý nghĩa với Audience vì ảnh hưởng **Gợi ý dành cho bạn**.
- **Mục đích:** Thiết lập gu nghe nhạc + bật/tắt cá nhân hoá AI.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Danh sách Genre/Mood/Atmosphere để chọn (nguồn: `GET /lounge-shows/filter-options`, dùng chung taxonomy).
- **Hành động khả dụng trên view:** `PUT /me/preferences` (GenreIds[], MoodIds[], AtmosphereIds[], EnableAiConsent) → 204. Tiêu chí riêng theo venue: `GET /me/custom-preferences` (list) + `PUT /me/custom-preferences/{criteriaId}` (Value, Weight).
- **Điều kiện hiển thị có điều kiện:** Tắt `EnableAiConsent` → hiện cảnh báo "Gợi ý dành cho bạn sẽ chỉ còn hiện mục đang thịnh hành (Trending), không còn cá nhân hoá" (đúng hành vi đã xác nhận ở [17 Journey 2 bước 3](17-audience-journey.md#journey-2--khám-phá--theo-dõi-showvenue)).
- **Điều hướng đến (From):** Hồ sơ cá nhân (link), hoặc lần đầu đăng ký (tự chọn, không bắt buộc).
- **Điều hướng đi (To):** Lưu xong → ở lại, hoặc quay về **Gợi ý dành cho bạn** để thấy kết quả đổi ngay.
- **Trạng thái đặc biệt cần thiết kế:** Rỗng nếu Admin chưa từng tạo Genre/Mood/Atmosphere nào (taxonomy do Admin quản lý ở **Quản lý Taxonomy nền tảng**) — cần empty state rõ ràng, không phải lỗi.

#### Quyền riêng tư & Dữ liệu cá nhân
- **Actor truy cập:** Mọi actor đã đăng nhập.
- **Mục đích:** Thực hiện quyền DSAR (xuất dữ liệu, xoá tài khoản) theo Luật 91/2025/QH15.
- **Loại view:** form/dashboard (khu vực "vùng nguy hiểm").
- **Dữ liệu hiển thị:** Không tải trước — 2 nút hành động với mô tả hệ quả.
- **Hành động khả dụng trên view:** Xuất dữ liệu: `GET /me/data-export` → `MyDataExportDto` (chưa xác minh field), tải về ngay lập tức (đồng bộ). Khoá tạm: `DELETE /me` → 204, khôi phục được. Xoá vĩnh viễn: `POST /me/data-erasure` (CurrentPassword? — chỉ bắt buộc nếu tài khoản local) → 204.
- **Điều kiện hiển thị có điều kiện:** Nút "Xoá vĩnh viễn" **phải** có modal xác nhận 2 bước (gõ lại mật khẩu nếu local) vì **không hoàn tác được** — set `IsActive=false`, `PasswordHash=null`, xoay `SecurityStamp` (thu hồi mọi JWT đang hiệu lực ngay lập tức, kể cả phiên đang mở ở thiết bị khác).
- **Điều hướng đến (From):** Hồ sơ cá nhân (link).
- **Điều hướng đi (To):** Khoá tạm → về màn Đăng nhập (session bị huỷ ngay). Xoá vĩnh viễn → về màn Đăng nhập, không còn đăng nhập lại được nữa.
- **Trạng thái đặc biệt cần thiết kế:** Loading khi export (đồng bộ, có thể mất vài giây nếu nhiều dữ liệu); xác nhận rõ ràng "hành động không thể hoàn tác" trước khi gọi data-erasure.

---

### B. Thông báo

#### Hộp thư thông báo
- **Actor truy cập:** Mọi actor đã đăng nhập — Admin nhận thêm loại thông báo vận hành/bảo mật nội bộ mà actor khác không có (`SettlementSchedulingBlocked`, cảnh báo job đối soát...).
- **Mục đích:** Xem tập trung mọi sự kiện hệ thống liên quan tới mình.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /notifications` → `PaginatedResult<NotificationDto>` (chưa xác minh field cụ thể — khả năng gồm Type, Title, Body, ReferenceType, ReferenceId, IsRead, CreatedAt dựa theo tham số `NotifyAsync` dùng xuyên suốt).
- **Hành động khả dụng trên view:** Đánh dấu đã đọc 1 cái: `POST /notifications/{id}/read`. Đánh dấu tất cả: `POST /notifications/read-all`.
- **Điều kiện hiển thị có điều kiện:** Tap vào 1 thông báo nên điều hướng theo `ReferenceType`/`ReferenceId` (vd `donation`→**Lịch sử Donate của tôi**, `complaint`→**Khiếu nại của tôi**) — chưa xác minh BE có field điều hướng sẵn hay FE phải tự map theo `Type` enum.
- **Điều hướng đến (From):** Icon chuông ở mọi màn hình đã đăng nhập.
- **Điều hướng đi (To):** Tuỳ `ReferenceType` — điều hướng sang view tương ứng (xem trên).
- **Trạng thái đặc biệt cần thiết kế:** Empty state ("Chưa có thông báo nào"); badge số chưa đọc cần đồng bộ với FCM push tới song song (2 kênh độc lập theo thiết kế, có thể lệch tạm thời nếu push lỗi nhưng in-app vẫn ghi đúng).

---

### C. Khám phá công khai

#### Danh sách/Tìm kiếm Show
- **Actor truy cập:** Ai cũng xem được (Anonymous). Owner đăng nhập + `mine=true` thấy thêm show `Draft`/`Pending` của chính mình.
- **Mục đích:** Duyệt/tìm show đang mở.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /lounge-shows`, `GET /lounge-shows/search`, `GET /lounge-shows/trending` → `PaginatedResult<LoungeShowListItemDto>`. Bộ lọc: `GET /lounge-shows/filter-options` (genre/mood/atmosphere/category). Gõ tìm: `GET /lounge-shows/suggestions` (autocomplete).
- **Hành động khả dụng trên view:** Lọc theo keyword/genre/mood/atmosphere/performer/venue/địa điểm/ngày/format/giá — toàn bộ query param của `GET /lounge-shows/search`. Bấm vào 1 show → sang chi tiết.
- **Điều kiện hiển thị có điều kiện:** **Không hiện** toggle/tab "Show của tôi" nếu chưa đăng nhập Owner — gọi `mine=true` không có token **ném lỗi 401** (`UnauthorizedException`, xác nhận trực tiếp trong `GetPublishedLoungeShowsQueryHandler` 2026-08-17), không phải im lặng trả về như `mine=false` như bản trước của tài liệu này từng ghi (đã sửa lại ở [21 Journey 1 bước 1](21-anonymous-journey.md#journey-1--duyệt-catalog-công-khai)).
- **Điều hướng đến (From):** Trang chủ, kết quả tìm kiếm, link từ Venue detail.
- **Điều hướng đi (To):** Bấm 1 item → **Chi tiết Show**.
- **Trạng thái đặc biệt cần thiết kế:** Empty state khi lọc không ra kết quả (khác empty vì chưa có show nào trên hệ thống); loading khi gõ tìm (debounce cho `suggestions`).

#### Chi tiết Show
- **Actor truy cập:** Mọi actor, kể cả Anonymous (`Optional Auth`) — nội dung hiển thị **giống nhau về field** nhưng bộ nút hành động khác hẳn theo actor/trạng thái vé/trạng thái show (xem "Hành động" bên dưới). Owner sở hữu show này thấy thêm link sang **Bảng điều khiển Show (Owner)**.
- **Mục đích:** Xem đầy đủ thông tin 1 show trước khi mua vé / vào xem live / đánh giá.
- **Loại view:** detail.
- **Dữ liệu hiển thị:** `GET /lounge-shows/{id}` → `LoungeShowDetailDto` (chưa xác minh field cụ thể — chắc chắn có `Performers: PerformerSummaryDto[]` gồm Id, Name, AvatarUrl, Bio, Genres, PerformanceId, AcceptsDonation — xem [22 §3](22-performer-presence.md#3-nơi-công-khai-thấy-được-performer)). Kèm `GET /lounge-shows/{id}/seating-map`.
- **Hành động khả dụng trên view:** Tuỳ trạng thái show + vé của actor: "Mua vé" → **Chọn vé & Giữ chỗ**; "Vào xem live" → **Phòng xem Livestream**; "Đánh giá" (`POST /lounge-shows/{id}/rate`); Follow venue/Wishlist show (nút phụ, link sang **Danh sách theo dõi/yêu thích**).
- **Điều kiện hiển thị có điều kiện:** Nút "Mua vé" chỉ hiện khi `Status=Published` và còn slot (5 lớp quota kiểm tra thật ở bước giữ chỗ, UI chỉ cần disable khi hết theo số liệu hiển thị). Nút "Vào xem live" chỉ hiện khi `Status=Ongoing` **và** show có Livestream — **và** dù hiện nút, việc vào được hay không còn phụ thuộc `isGenuineTicketHolder` kiểm tra riêng ở tầng SignalR (nút có thể hiện nhưng vẫn bị từ chối kết nối). Nút "Đánh giá" chỉ hiện khi actor có vé `Confirmed`/`Used` cho show này, `Status=Ended`, còn trong 7 ngày kể từ `ActualEnd`, và **chưa từng đánh giá**.
- **Điều hướng đến (From):** Danh sách/Tìm kiếm Show, Chi tiết Venue, Trang cá nhân Nghệ sĩ, thông báo (`EventLive`/`NewEvent`).
- **Điều hướng đi (To):** Theo hành động đã chọn ở trên; Owner sở hữu → **Bảng điều khiển Show (Owner)**.
- **Trạng thái đặc biệt cần thiết kế:** 404 nếu show không tồn tại/đã xoá; khác hẳn hiển thị nếu show `Cancelled` (không còn nút mua vé, chỉ còn thông báo lý do huỷ nếu có).

#### Danh sách Venue
- **Actor truy cập:** Ai cũng xem được. Owner + `mine=true` thấy venue của mình mọi trạng thái (kể cả `Pending`/`Rejected`).
- **Mục đích:** Duyệt danh sách phòng trà.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /lounges` (city?, mine?) → `PaginatedResult<LoungeListItemDto>` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** Lọc theo thành phố; bấm vào 1 venue.
- **Điều kiện hiển thị có điều kiện:** Venue `Pending`/`Rejected` **chỉ** hiện trong danh sách khi actor là Owner của chính venue đó (`mine=true`) — công khai không thấy venue chưa duyệt (BR-01, đã xác nhận resolved).
- **Điều hướng đến (From):** Trang chủ, kết quả tìm kiếm show (link venue).
- **Điều hướng đi (To):** Bấm 1 item → **Chi tiết Venue**.
- **Trạng thái đặc biệt cần thiết kế:** Empty state theo thành phố lọc.

#### Chi tiết Venue
- **Actor truy cập:** Ai cũng xem được (`Optional Auth`).
- **Mục đích:** Xem thông tin, vị trí, show đang diễn tại venue.
- **Loại view:** detail.
- **Dữ liệu hiển thị:** `GET /lounges/{id}` → `LoungeDetailDto` (chưa xác minh field cụ thể — khả năng cao Owner xem thêm field quản trị, chưa xác minh cụ thể field nào). Show tại venue: `GET /lounge-shows/by-lounge/{loungeId}`. Khu vực chỗ ngồi: `GET /lounges/{id}/zones`.
- **Hành động khả dụng trên view:** "Theo dõi venue": `POST /follows/lounges/{loungeId}` → 204. Link sang **Tour ảo 360°** nếu venue có tour.
- **Điều kiện hiển thị có điều kiện:** Nút "Theo dõi" hiện với mọi venue kể cả `Pending`/`Suspended` — **không gate** theo trạng thái venue (khác Wishlist show, có gate) — cố ý, đã xác nhận ở [17 Journey 2 bước 4](17-audience-journey.md#journey-2--khám-phá--theo-dõi-showvenue).
- **Điều hướng đến (From):** Danh sách Venue, Chi tiết Show (link venue tổ chức).
- **Điều hướng đi (To):** **Tour ảo 360°** (nếu có), **Danh sách/Tìm kiếm Show** (lọc theo venue này).
- **Trạng thái đặc biệt cần thiết kế:** 404 nếu venue không tồn tại; không có tour 360° → ẩn hẳn link, không hiện disabled.

#### Tour ảo 360°
- **Actor truy cập:** Ai cũng xem được — **không cần vé, không cần theo dõi live** để xem tour.
- **Mục đích:** Trải nghiệm không gian venue qua panorama 360° kiểu Louvre trước khi quyết định mua vé.
- **Loại view:** real-time/interactive (kéo-thả xem panorama, click hotspot chuyển scene).
- **Dữ liệu hiển thị:** `GET /lounges/{id}/tour` → `VenueTourDto` (chưa xác minh field cụ thể — chắc chắn có danh sách scene + hotspot theo cấu trúc `POST .../tour/scenes` và `.../hotspots` chấp nhận: ImageUrl, Name, X/Y position, Yaw/Pitch/Label/TargetSceneId/InfoText mỗi hotspot).
- **Hành động khả dụng trên view:** Click hotspot loại "chuyển scene" → nhảy sang scene khác trong cùng view; hotspot loại "info" → hiện `InfoText`.
- **Điều kiện hiển thị có điều kiện:** Không có — toàn bộ trải nghiệm công khai hoàn toàn, đã xác nhận không có phần nào cần đăng nhập.
- **Điều hướng đến (From):** Chi tiết Venue (link).
- **Điều hướng đi (To):** Không điều hướng đi tiếp tự động — actor tự thoát về Chi tiết Venue.
- **Trạng thái đặc biệt cần thiết kế:** Venue chưa có scene nào → empty state ("Venue chưa thiết lập tour ảo"), không phải lỗi.

#### Gợi ý dành cho bạn
- **Actor truy cập:** Audience đã đăng nhập (`RequireAuthenticated`).
- **Mục đích:** Khám phá show phù hợp gu cá nhân.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /recommendations` (limit) → `RecommendedLoungeShowDto[]` (chưa xác minh field cụ thể — khả năng có field điểm số/lý do gợi ý dựa theo công thức `FinalScore` đã biết).
- **Hành động khả dụng trên view:** Bấm vào 1 item → sang chi tiết.
- **Điều kiện hiển thị có điều kiện:** `EnableAiConsent=false` → chỉ trả tầng Trending (không cá nhân hoá) — UI nên hiện nhãn "Đang thịnh hành" thay vì "Dành riêng cho bạn" khi phát hiện trường hợp này (nếu response có field phân biệt tầng — chưa xác minh, cần hỏi lại BE nếu muốn phân biệt rõ trên UI).
- **Điều hướng đến (From):** Trang chủ (tab/section riêng).
- **Điều hướng đi (To):** Bấm 1 item → **Chi tiết Show**.
- **Trạng thái đặc biệt cần thiết kế:** Empty state khi chưa đủ dữ liệu hành vi (mới đăng ký) — hệ thống tự rơi về Trending, không phải màn hình trắng.

---

### D. Follow/Wishlist

#### Danh sách đã theo dõi/yêu thích
- **Actor truy cập:** Audience đã đăng nhập.
- **Mục đích:** Quản lý danh sách venue đã follow + show đã wishlist.
- **Loại view:** list (2 tab: Venue đã follow / Show đã wishlist).
- **Dữ liệu hiển thị:** `GET /follows/lounges` → `PaginatedResult<FollowedLoungeDto>` (chưa xác minh field). `GET /wishlist` → `PaginatedResult<LoungeShowListItemDto>`.
- **Hành động khả dụng trên view:** Bỏ theo dõi: `DELETE /follows/lounges/{loungeId}`. Bỏ khỏi wishlist: `DELETE /wishlist/{showId}`.
- **Điều kiện hiển thị có điều kiện:** Show wishlist đã `Cancelled` sau khi thêm — vẫn hiện trong danh sách (không tự động gỡ), chỉ khác ở UI hiện nhãn trạng thái để actor tự gỡ nếu muốn.
- **Điều hướng đến (From):** Menu tài khoản.
- **Điều hướng đi (To):** Bấm 1 item → **Chi tiết Venue** hoặc **Chi tiết Show**.
- **Trạng thái đặc biệt cần thiết kế:** Empty state riêng cho từng tab.

---

### E. Vé (Audience)

#### Chọn vé & Giữ chỗ
- **Actor truy cập:** Audience đã đăng nhập.
- **Mục đích:** Chọn hạng vé, số lượng và giữ chỗ tạm thời trước khi thanh toán.
- **Loại view:** form/detail (kết hợp bảng giá + trạng thái giữ chỗ đang đếm ngược).
- **Dữ liệu hiển thị:** `GET /ticket-tiers?showId=` → `TicketTierSummaryDto[]` (chưa xác minh field cụ thể — chắc chắn có giá + số lượng còn lại, đã xác nhận công khai xem được không cần login). `GET /lounge-shows/{id}/seating-map`.
- **Hành động khả dụng trên view:** Giữ chỗ: `POST /tickets/holds` (PriceId, Quantity) → `HoldTicketResultDto` (giữ 15 phút). Sau khi có hold: "Huỷ giữ chỗ" `DELETE /tickets/holds/{holdId}`, hoặc "Tiếp tục thanh toán" `POST /tickets/purchase` (HoldId).
- **Điều kiện hiển thị có điều kiện:** Nút "Giữ chỗ" disable khi hết vé ở **bất kỳ lớp nào trong 5 lớp quota** (đợt giá/tier/zone/show/gói subscription Owner) — UI chỉ hiện số liệu còn lại đã lọc, không tự tính. Đồng hồ đếm ngược 15 phút bắt buộc phải hiện rõ — hết giờ mà chưa `purchase` thì hold tự huỷ ở BE, UI cần tự phát hiện hết giờ (polling hoặc tính local) và quay lại trạng thái chưa giữ chỗ.
- **Điều hướng đến (From):** Chi tiết Show (nút "Mua vé").
- **Điều hướng đi (To):** Huỷ giữ chỗ → ở lại view, reset. Tiếp tục thanh toán → chuyển sang trang VNPay (ngoài hệ thống) → **Kết quả thanh toán**.
- **Trạng thái đặc biệt cần thiết kế:** Lỗi 400 khi hết vé cần hiện đúng thông điệp (không generic "có lỗi xảy ra") vì đây là tình huống thường gặp (concurrency); loading khi giữ chỗ (nhiều actor tranh chấp cùng lúc).

#### Kết quả thanh toán
- **Actor truy cập:** Audience (vé/donate) hoặc Owner (subscription) — 1 view mẫu dùng chung cho 3 luồng thanh toán VNPay khác nhau, tham số hoá theo loại giao dịch.
- **Mục đích:** Thông báo kết quả sau khi actor quay lại từ VNPay.
- **Loại view:** detail (trạng thái kết quả).
- **Dữ liệu hiển thị:** Redirect từ `GET /payments/vnpay/callback` (vé) / `GET /donations/vnpay-return` (donate) / `GET /subscriptions/vnpay-return` (subscription) — đều trả **redirect 302**, không phải JSON cho FE đọc trực tiếp; FE cần đọc kết quả thật qua polling lại resource tương ứng (`GET /tickets/{id}`, `GET /donations/{id}`, `GET /subscriptions/my`) vì nguồn sự thật là IPN server-to-server, có thể xử lý **chậm hơn** redirect trình duyệt.
- **Hành động khả dụng trên view:** Không có hành động nghiệp vụ — chỉ có nút điều hướng tiếp theo tuỳ kết quả.
- **Điều kiện hiển thị có điều kiện:** **Quan trọng cho UI**: callback redirect (trình duyệt) không phải nguồn sự thật — nếu tại thời điểm actor quay lại mà IPN chưa kịp xử lý xong, trạng thái đọc được có thể vẫn `Pending`. UI nên tự poll lại vài giây thay vì tin ngay kết quả từ URL param.
- **Điều hướng đến (From):** VNPay (redirect tự động).
- **Điều hướng đi (To):** Vé thành công → **Chi tiết vé**. Donate thành công → **Lịch sử Donate của tôi** hoặc quay lại **Phòng xem Livestream**. Subscription thành công → **Gói Subscription của tôi**. Thất bại → quay lại view khởi tạo tương ứng (Chọn vé & Giữ chỗ / Form Donate / Gói Subscription của tôi).
- **Trạng thái đặc biệt cần thiết kế:** Trạng thái "đang xử lý" (chưa chắc chắn) khác hẳn "thành công"/"thất bại" — bắt buộc phải có state thứ 3 này, không phải chỉ 2 trạng thái nhị phân.

#### Vé của tôi
- **Actor truy cập:** Audience đã đăng nhập.
- **Mục đích:** Xem toàn bộ lịch sử vé đã mua.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /tickets/my` → `PaginatedResult<TicketListItemDto>` (chưa xác minh field cụ thể). Mọi trạng thái: Pending/Confirmed/Used/Cancelled/Refunded.
- **Hành động khả dụng trên view:** Bấm vào 1 vé → chi tiết. Badge "Có lời mời chuyển nhượng" nếu có: `GET /tickets/incoming-transfers`.
- **Điều kiện hiển thị có điều kiện:** Lọc/nhóm theo trạng thái nên tách rõ "sắp diễn" khỏi "đã qua" (không phải field BE trả sẵn, FE tự suy từ `ScheduledStart` của show liên kết).
- **Điều hướng đến (From):** Menu tài khoản, thông báo "vé đã xác nhận".
- **Điều hướng đi (To):** Bấm 1 vé → **Chi tiết vé**.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Bạn chưa mua vé nào".

#### Chi tiết vé
- **Actor truy cập:** Audience — chỉ chủ vé (`BuyerId` khớp `currentUser`, check ở handler).
- **Mục đích:** Xem QR để vào cửa, quản lý chuyển nhượng/huỷ.
- **Loại view:** detail.
- **Dữ liệu hiển thị:** `GET /tickets/{id}` → `TicketDetailDto` (chưa xác minh field cụ thể). Ảnh QR: `GET /tickets/{id}/qr` (SVG).
- **Hành động khả dụng trên view:** Chuyển nhượng: `POST /tickets/{id}/transfer` (RecipientEmail). Huỷ yêu cầu chuyển nhượng đang chờ: `POST /tickets/{id}/transfer/cancel`. Huỷ vé: `POST /tickets/{id}/cancel` → trả `refundRequestId`.
- **Điều kiện hiển thị có điều kiện:** Nút "Chuyển nhượng" chỉ hiện khi: vé `Confirmed`, **chưa check-in**, **chưa từng xem livestream** (`FirstAccessedAt is null`), show chưa `Ended`/`Cancelled`. Nút "Huỷ vé": vé `Pending` → huỷ ngay không điều kiện; vé `Confirmed` → chỉ hiện nếu còn trong hạn (`ScheduledStart − CancellationDeadlineHours`), show cho phép huỷ (`CancellationAllowed=true`), chưa check-in, không đang chuyển nhượng dở dang.
- **Điều hướng đến (From):** Vé của tôi.
- **Điều hướng đi (To):** Huỷ vé thành công → **Yêu cầu hoàn tiền của tôi** (theo dõi `refundRequestId` vừa tạo).
- **Trạng thái đặc biệt cần thiết kế:** Vé `Cancelled`/`Refunded` → ẩn QR (đã dùng được nữa không có ý nghĩa vào cửa), hiện rõ nhãn trạng thái thay vào chỗ QR.

#### Yêu cầu hoàn tiền của tôi
- **Actor truy cập:** Audience đã đăng nhập.
- **Mục đích:** Theo dõi tiến độ xử lý các yêu cầu hoàn tiền vé.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /tickets/refund-requests/my` → `PaginatedResult<RefundRequestDto>` (chưa xác minh field cụ thể — chắc chắn có Status: Pending/Approved/Rejected theo enum `RefundRequestStatus`).
- **Hành động khả dụng trên view:** Chỉ xem — Admin mới xử lý được (xem **Yêu cầu hoàn tiền (Refund Requests)** phía Admin).
- **Điều kiện hiển thị có điều kiện:** Tỉ lệ hoàn hiện đúng theo lý do huỷ — 100% nếu do Owner huỷ show/Admin gỡ nội dung vi phạm; theo `show.RefundPercentage` (mặc định 100% nếu Owner không cấu hình khác) nếu Audience tự huỷ.
- **Điều hướng đến (From):** Chi tiết vé (sau khi huỷ), Hộp thư thông báo (kết quả xử lý).
- **Điều hướng đi (To):** Không điều hướng tiếp — màn hình theo dõi thuần tuý.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Chưa có yêu cầu hoàn tiền nào".

---

### F. Livestream & Donate

#### Phòng xem Livestream
- **Actor truy cập:** Audience có vé thật cho show đó (`isGenuineTicketHolder`); Owner/Staff vận hành venue đó; Admin luôn xem được (`LivestreamAccessPolicy`) — Admin thấy thêm nút "Buộc dừng phát" mà actor khác không có.
- **Mục đích:** Xem video trực tiếp, chat, donate cho nghệ sĩ đang biểu diễn.
- **Loại view:** real-time.
- **Dữ liệu hiển thị:** `GET /livestreams/{id}` → `LivestreamDetailDto` (chưa xác minh field cụ thể — nội dung khác theo actor, xem docs/16 §"Endpoint có response khác nhau theo Actor"). Video: Mux HLS (ngoài .NET backend). Chat: `GET /livestreams/{id}/chat` → `PaginatedResult<ChatMessageDto>` (chưa xác minh field).
- **Hành động khả dụng trên view:** Gửi chat: SignalR `LivestreamHub` (gửi `Message`). Mở form donate (xem view riêng). Admin: `POST /livestreams/{id}/terminate` (Reason).
- **Điều kiện hiển thị có điều kiện:** Kết nối SignalR bị `Context.Abort()` ngay nếu không phải chủ vé thật hoặc vượt giới hạn thiết bị xem đồng thời (mặc định 2/vé) — UI cần bắt sự kiện disconnect này và hiện thông báo rõ ràng, không phải màn hình trắng/treo. Nút donate chỉ hiện với `Performance.AcceptsDonation=true` cho đúng lượt diễn đang chọn.
- **Điều hướng đến (From):** Chi tiết Show (nút "Vào xem live").
- **Điều hướng đi (To):** Bấm donate → **Form Donate**. Admin terminate → mọi actor khác bị ngắt kết nối, quay về **Chi tiết Show**.
- **Trạng thái đặc biệt cần thiết kế:** Mất kết nối SignalR (mạng chập chờn) cần retry tự động, phân biệt với bị `Abort()` do không đủ quyền (2 nguyên nhân khác nhau, UX xử lý khác nhau — 1 cái retry được, 1 cái thì không).

#### Ticker Donate công khai
- **Actor truy cập:** Ai cũng xem được, kể cả Anonymous, kể cả **không có vé xem live** — widget nhúng trong **Chi tiết Show**/**Phòng xem Livestream**, không phải trang riêng.
- **Mục đích:** Hiện overlay realtime kiểu Streamlabs khi có donate mới cho show đang diễn.
- **Loại view:** real-time (widget nhúng).
- **Dữ liệu hiển thị:** SignalR `PublicDonationHub` (kết nối với `loungeShowId`, thiếu thì `Context.Abort()`) → `PublicDonationAlertDto` — tên/tin nhắn/số tiền đã lọc theo 3 cờ riêng tư donor chọn lúc donate (`IsAnonymous`/`IsAmountPublic`/`IsMessagePublic`).
- **Hành động khả dụng trên view:** Không có — thuần hiển thị.
- **Điều kiện hiển thị có điều kiện:** **Không đòi hỏi quyền xem video livestream** — đây là hệ thống hoàn toàn tách biệt khỏi `LivestreamHub` (điểm dễ nhầm nhất, đã ghi ở [21 §So sánh nhanh](21-anonymous-journey.md#journey-2--xem-minh-bạch-donate-công-khai)). Bắn 3 lần cho cùng 1 donate (chặng 1/3, 2/3, 3/3) theo tiến độ Owner xử lý — UI nên hiện dạng "cập nhật trạng thái" chứ không tạo 3 dòng ticker trùng lặp nhìn rối.
- **Điều hướng đến (From):** Nhúng sẵn trong Chi tiết Show / Phòng xem Livestream, không điều hướng riêng.
- **Điều hướng đi (To):** Không điều hướng — bấm vào 1 alert (nếu muốn) có thể mở **Sổ minh bạch Donate công khai** để xem lại lịch sử đầy đủ.
- **Trạng thái đặc biệt cần thiết kế:** Show chưa có donate nào → không hiện gì (không phải empty-state kiểu bảng, chỉ đơn giản không có gì để hiện).

#### Form Donate
- **Actor truy cập:** Audience đã đăng nhập, đang trong **Phòng xem Livestream** của show `Ongoing`.
- **Mục đích:** Gửi donate cho 1 nghệ sĩ đang biểu diễn.
- **Loại view:** form (modal trên Phòng xem Livestream).
- **Dữ liệu hiển thị:** Tên/avatar nghệ sĩ đã chọn (từ `PerformerSummaryDto` đã tải sẵn ở Chi tiết Show/Phòng xem Livestream).
- **Hành động khả dụng trên view:** `POST /donations` (PerformanceId, Amount, IsAnonymous, Message, IsMessagePublic) → `DonationInitiationDto` (DonationId, OrderId, Gross, PaymentUrl).
- **Điều kiện hiển thị có điều kiện:** Chỉ submit được khi show vẫn `Ongoing` tại thời điểm gọi (không phải lúc mở form) — nếu show chuyển `Ended` giữa lúc actor đang gõ, submit sẽ bị chặn, UI cần hiện lỗi rõ ràng chứ không phải mất dữ liệu đã nhập. 3 tuỳ chọn riêng tư (`IsAnonymous`/`IsAmountPublic`... — lưu ý `IsAmountPublic` không nằm trong Request field liệt kê ở docs/16 `POST /donations`, cần xác minh lại có đúng là field nhập ở bước tạo hay được set mặc định/sửa sau).
- **Điều hướng đến (From):** Phòng xem Livestream (nút donate trên 1 performer).
- **Điều hướng đi (To):** Chuyển sang VNPay → **Kết quả thanh toán** → quay lại **Phòng xem Livestream**.
- **Trạng thái đặc biệt cần thiết kế:** Loading khi khởi tạo thanh toán; lỗi "show đã kết thúc" là case riêng cần message rõ, không phải lỗi validate thông thường.

#### Lịch sử Donate của tôi
- **Actor truy cập:** Audience đã đăng nhập.
- **Mục đích:** Theo dõi tiến độ từng donate mình đã gửi.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /donations/my` → `PaginatedResult<MyDonationDto>` (Id, PerformerName, ShowName, Gross, Net, Status, IsAnonymous, Message, CreatedAt).
- **Hành động khả dụng trên view:** Bấm 1 dòng → `GET /donations/{id}` (đầy đủ breakdown: Gross, Net, PlatformFee, Tax, PerformerShareRate, PerformerAmount, OwnerRetained).
- **Điều kiện hiển thị có điều kiện:** Hiện tiến độ dạng stepper `PendingOwnerAck → OwnerReceived → PerformerPaid` — tốc độ hoàn toàn phụ thuộc Owner xử lý, actor không thao tác thêm được ở đây.
- **Điều hướng đến (From):** Hộp thư thông báo (`DonationConfirmed`), Phòng xem Livestream (link "Lịch sử donate").
- **Điều hướng đi (To):** Không điều hướng tiếp — màn hình theo dõi thuần tuý.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Bạn chưa donate cho ai".

#### Sổ minh bạch Donate công khai
- **Actor truy cập:** Ai cũng xem được, kể cả Anonymous — 2 biến thể trong cùng 1 nhóm mục đích: toàn hệ thống và theo 1 nghệ sĩ.
- **Mục đích:** Minh bạch dòng tiền donate cho công chúng.
- **Loại view:** list.
- **Dữ liệu hiển thị:** Toàn hệ thống: `GET /donations/public` → `PaginatedResult<PublicDonationTransactionDto>` (đầy đủ breakdown: Gross, Net, PlatformFee, Tax, PerformerAmount, OwnerRetained). Theo 1 nghệ sĩ: `GET /performers/{performerId}/donations` → `PublicDonationDto` (chỉ Gross, **không** breakdown).
- **Hành động khả dụng trên view:** Chỉ xem — không có hành động ghi.
- **Điều kiện hiển thị có điều kiện:** Chỉ hiện donate đã "chốt" (`OwnerReceived`/`PerformerPaid`) — **không** hiện `PendingOwnerAck` dù **Ticker Donate công khai** đã hiện alert cho donate đó rồi (pattern "pending vs. posted" ngành ngân hàng, cố ý — xem [15-risk-audit.md §3](15-risk-audit.md#3-đối-chứng-tích-cực)). **Không được** thiết kế 2 nguồn này dùng chung 1 component data rồi thắc mắc sao lệch nhau.
- **Điều hướng đến (From):** Chi tiết Show/Trang cá nhân Nghệ sĩ (link "Xem lịch sử donate"), Ticker Donate công khai.
- **Điều hướng đi (To):** Không điều hướng tiếp.
- **Trạng thái đặc biệt cần thiết kế:** Empty state khác nhau: "Chưa có donate nào công khai" (khác 0 kết quả vì bộ lọc).

#### Trang cá nhân Nghệ sĩ
- **Actor truy cập:** Ai cũng xem được (Anonymous).
- **Mục đích:** Xem hồ sơ công khai của 1 nghệ sĩ và toàn bộ show đã/sắp diễn.
- **Loại view:** detail.
- **Dữ liệu hiển thị:** `GET /lounge-shows/by-performer/{performerId}` → `PerformerDetailDto` (Id, Name, AvatarUrl, Bio, Genres, Shows phân trang) — **không** có `SocialLinks` (gap đã ghi nhận ở [22 §3](22-performer-presence.md#3-nơi-công-khai-thấy-được-performer), chưa sửa).
- **Hành động khả dụng trên view:** Link sang **Sổ minh bạch Donate công khai** (biến thể theo nghệ sĩ).
- **Điều kiện hiển thị có điều kiện:** **Không** hiện khối "Theo dõi mạng xã hội" dù Owner có nhập `SocialLinks` qua `PUT /performers/{id}/social-links` — dữ liệu này hiện không có đường ra public nào, đừng thiết kế UI cho field chưa tồn tại trong response.
- **Điều hướng đến (From):** Chi tiết Show (avatar/tên nghệ sĩ trong lineup).
- **Điều hướng đi (To):** Bấm 1 show → **Chi tiết Show**. Link donate history → **Sổ minh bạch Donate công khai**.
- **Trạng thái đặc biệt cần thiết kế:** Empty state nếu nghệ sĩ chưa từng có show nào (mới tạo).

---

### G. F&B (khách đặt)

> 📱 **Nền tảng: App mobile native riêng** (không phải website Audience) — xem [§1.5](#15-nền-tảng-webmobile) / [platform-architecture.md](platform-architecture.md). Brief thiết kế: [stitch-brief-audience-mobile-fnb.md](stitch-brief-audience-mobile-fnb.md).

#### Đặt món F&B
- **Actor truy cập:** Audience đã đăng nhập (đặt cho mình) — cùng endpoint Staff dùng đặt hộ (xem **Quản lý đơn F&B tại quầy**), nhưng đây là view phía khách tự đặt.
- **Mục đích:** Xem menu và đặt món tại venue.
- **Loại view:** list + form (menu kèm giỏ hàng).
- **Dữ liệu hiển thị:** `GET /fnb-menus?loungeId=`, `GET /fnb-menu-items?menuId=` → `FnbMenuDto[]`/`FnbMenuItemDto[]` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** `POST /fnb-orders` (LoungeId, ShowId?, ZoneId?, TableNote, PaymentMethod, Note, Items[]) → int id.
- **Điều kiện hiển thị có điều kiện:** Món `IsAvailable=false` → disable nút thêm vào giỏ, không ẩn hẳn (để khách biết món này tồn tại nhưng tạm hết). Chặn đặt nếu Zone/Show chọn không thuộc đúng venue đang xem.
- **Điều hướng đến (From):** Chi tiết Venue (link "Xem menu"), Chi tiết Show (nếu đang xem tại venue).
- **Điều hướng đi (To):** Đặt xong → **Đơn F&B của tôi**.
- **Trạng thái đặc biệt cần thiết kế:** Venue chưa có menu nào → empty state, không phải lỗi.

#### Đơn F&B của tôi
- **Actor truy cập:** Audience đã đăng nhập (chủ đơn).
- **Mục đích:** Theo dõi trạng thái đơn đã đặt.
- **Loại view:** list/detail.
- **Dữ liệu hiển thị:** `GET /fnb-orders/my` → `PaginatedResult<FnbOrderDto>` (chưa xác minh field cụ thể). Chi tiết 1 đơn: `GET /fnb-orders/{id}`.
- **Hành động khả dụng trên view:** Chỉ xem — trạng thái do Staff cập nhật, Audience không tự đổi được.
- **Điều kiện hiển thị có điều kiện:** Hiện stepper `Pending→Preparing→Served→Paid`, hoặc nhãn `Cancelled` nếu bị huỷ (lối thoát riêng, có thể xảy ra ở bất kỳ bước nào trước `Paid`).
- **Điều hướng đến (From):** Đặt món F&B (sau khi đặt xong), Menu tài khoản.
- **Điều hướng đi (To):** Không điều hướng tiếp.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Chưa có đơn nào"; chưa có push riêng cho cập nhật trạng thái F&B (đã ghi ở [17 §Tổng hợp real-time](17-audience-journey.md#tổng-hợp-điểm-giao-thoa-real-time-đáng-chú-ý-nhất-khi-thiết-kế-view)) — nếu muốn thấy cập nhật mới nhất, actor phải tự vào lại/tự refresh, không tự đẩy real-time.

---

### H. Đánh giá

#### Đánh giá Show
- **Actor truy cập:** Audience đã đăng nhập, có vé `Confirmed`/`Used` cho show đó.
- **Mục đích:** Chấm điểm + viết nhận xét sau khi show kết thúc.
- **Loại view:** form (thường modal trên Chi tiết Show).
- **Dữ liệu hiển thị:** Tên show + poster (đã tải sẵn từ Chi tiết Show).
- **Hành động khả dụng trên view:** `POST /lounge-shows/{id}/rate` (Score, Comment) → 204.
- **Điều kiện hiển thị có điều kiện:** Chỉ submit được nếu: show `Ended`, còn trong `RatingOpenUntil` (mặc định 7 ngày sau `ActualEnd`), actor có vé hợp lệ cho show, **chưa đánh giá lần nào** — vi phạm bất kỳ điều kiện nào → 409, form không nên hiện nút submit nếu FE đã biết trước điều kiện không đạt (ẩn hẳn nút "Đánh giá" ở Chi tiết Show thay vì hiện rồi báo lỗi).
- **Điều hướng đến (From):** Chi tiết Show (nút "Đánh giá", chỉ hiện đúng điều kiện trên).
- **Điều hướng đi (To):** Submit xong → về Chi tiết Show, ẩn nút đánh giá (đã dùng hết lượt).
- **Trạng thái đặc biệt cần thiết kế:** Quá hạn `RatingOpenUntil` → nút không hiện nữa (không phải lỗi khi bấm).

---

### I. Khiếu nại

#### Gửi khiếu nại
- **Actor truy cập:** Ai cũng gửi được, kể cả Anonymous (`AllowAnonymous`).
- **Mục đích:** Báo cáo vấn đề (vé, donate, thái độ venue...).
- **Loại view:** form.
- **Dữ liệu hiển thị:** Không tải trước — form trắng, có thể điền sẵn `TargetType`/`TargetId` nếu mở từ 1 đối tượng cụ thể (vd từ Chi tiết vé).
- **Hành động khả dụng trên view:** `POST /complaints` (TargetType, TargetId, TargetGuid?, Category, Description, EvidenceUrls?, ContactPhone?) → int id.
- **Điều kiện hiển thị có điều kiện:** Field `ContactPhone` **bắt buộc** nếu actor chưa đăng nhập (BE chặn `DomainException` nếu thiếu) — UI phải validate field này required khi phát hiện chưa có token, optional khi đã đăng nhập. `TargetGuid` chỉ dùng khi `TargetType="ticket"` (Ticket.Id là Guid, không dùng `TargetId` int cho case này).
- **Điều hướng đến (From):** Chi tiết vé/Chi tiết Show/Chi tiết Venue (link "Báo cáo vấn đề"), hoặc trang khiếu nại chung.
- **Điều hướng đi (To):** Đã đăng nhập → **Khiếu nại của tôi** (xem lại đúng khiếu nại vừa gửi). Chưa đăng nhập → **Tra cứu khiếu nại khách vãng lai** (hiện rõ `id` vừa nhận để actor tự lưu lại).
- **Trạng thái đặc biệt cần thiết kế:** Sau submit khi chưa đăng nhập, **bắt buộc hiện rõ `int id`** trên màn hình xác nhận (không chỉ log nội bộ) — đây là nửa đầu của cặp khoá tra cứu sau này.

#### Khiếu nại của tôi
- **Actor truy cập:** Audience/Owner/Staff/Admin đã đăng nhập (chủ khiếu nại).
- **Mục đích:** Theo dõi khiếu nại đã gửi.
- **Loại view:** list/detail.
- **Dữ liệu hiển thị:** `GET /complaints/my` → `PaginatedResult<ComplaintDto>`. Chi tiết: `GET /complaints/{id}` (Id, TargetType, TargetId, TargetGuid, Category, Description, EvidenceUrls, ContactPhone, Status, ComplainantName, AdminName, Resolution, ResolvedAction, ResolvedAt, CreatedAt).
- **Hành động khả dụng trên view:** Chỉ xem.
- **Điều kiện hiển thị có điều kiện:** Nếu `ResolvedAction=Refund/Compensate` và target là vé → hiện link sang **Yêu cầu hoàn tiền của tôi** (RefundRequest đã tự tạo).
- **Điều hướng đến (From):** Gửi khiếu nại (sau khi gửi, nếu đã đăng nhập), Hộp thư thông báo (`ComplaintUpdate`).
- **Điều hướng đi (To):** Có refund liên quan → **Yêu cầu hoàn tiền của tôi**.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Bạn chưa gửi khiếu nại nào".

#### Tra cứu khiếu nại khách vãng lai
- **Actor truy cập:** Anonymous.
- **Mục đích:** Tự tra lại kết quả khiếu nại đã gửi lúc chưa đăng nhập, bằng id + số điện thoại.
- **Loại view:** form + detail (nhập xong hiện luôn kết quả).
- **Dữ liệu hiển thị:** `GET /complaints/lookup?id=&phone=` → `ComplaintDto` (cùng shape với Khiếu nại của tôi).
- **Hành động khả dụng trên view:** Nhập `id` + số điện thoại đã dùng lúc gửi → xem kết quả.
- **Điều kiện hiển thị có điều kiện:** Sai `id` hoặc sai số điện thoại → **404 giống hệt nhau**, không phân biệt lý do (chống dò) — UI chỉ nên hiện 1 thông điệp chung "Không tìm thấy khiếu nại khớp thông tin đã nhập", không suy đoán lý do cụ thể. Có rate-limit 10 req/phút/IP — UI không nên cho submit liên tục không giới hạn.
- **Điều hướng đến (From):** Gửi khiếu nại (sau khi gửi lúc chưa đăng nhập), hoặc link riêng "Tra cứu khiếu nại" ở footer.
- **Điều hướng đi (To):** Không điều hướng tiếp — actor cũng nhận SMS chủ động khi Admin xử lý xong (kênh riêng, ngoài luồng điều hướng UI).
- **Trạng thái đặc biệt cần thiết kế:** 429 (quá số lần thử) cần message riêng biệt với 404 (không tìm thấy).

---

### J. Owner — Venue

#### Danh sách Venue của tôi
- **Actor truy cập:** Owner.
- **Mục đích:** Quản lý tổng quan mọi venue mình sở hữu.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /lounges?mine=true` → `PaginatedResult<LoungeListItemDto>`, mọi trạng thái kể cả `Pending`/`Rejected`.
- **Hành động khả dụng trên view:** Nút "Tạo venue mới" → **Tạo/Sửa Venue**.
- **Điều kiện hiển thị có điều kiện:** Venue `Pending` → hiện nhãn "Đang chờ Admin duyệt", ẩn mọi hành động vận hành (chưa tạo show được). Venue `Rejected` → hiện lý do (`Reason` từ `POST /admin/lounges/{id}/reject`), nút duy nhất là "Tạo venue mới" (không có "nộp lại đúng venue đó").
- **Điều hướng đến (From):** Menu Owner (sau đăng nhập).
- **Điều hướng đi (To):** Bấm 1 venue → **Tạo/Sửa Venue** (nếu cần sửa) hoặc thẳng **Bảng điều khiển Show (Owner)** lọc theo venue đó.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Bạn chưa có venue nào" (Owner mới đăng ký).

#### Tạo/Sửa Venue
- **Actor truy cập:** Owner (đúng chủ venue khi sửa).
- **Mục đích:** Nhập/cập nhật thông tin cơ bản, ảnh, giấy phép venue.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Khi sửa: `GET /lounges/{id}` → `LoungeDetailDto`.
- **Hành động khả dụng trên view:** Tạo: `POST /lounges` (Name, Description, AtmosphereId, Street/Ward/District/City, Lat/Long). Sửa: `PUT /lounges/{id}`. Ảnh đại diện: `POST /uploads/images` → `PUT /lounges/{id}/image`. Giấy phép kinh doanh: `PUT /lounges/{id}/business-license`. Model 3D (.glb, khác tour 360°): `POST /uploads/models` → `PUT /lounges/{id}/model-3d`.
- **Điều kiện hiển thị có điều kiện:** Venue vừa tạo ở trạng thái `Pending` — **chưa tạo được show** cho tới khi Admin duyệt, UI nên hiện banner nhắc rõ ngay sau khi tạo thành công.
- **Điều hướng đến (From):** Danh sách Venue của tôi.
- **Điều hướng đi (To):** Tạo xong → **Danh sách Venue của tôi** (thấy venue mới ở trạng thái Pending). Sửa xong → ở lại view.
- **Trạng thái đặc biệt cần thiết kế:** Loading khi upload ảnh/file lớn (giấy phép, model 3D).

#### Sơ đồ chỗ ngồi (Zone Editor)
- **Actor truy cập:** Owner (đúng chủ venue).
- **Mục đích:** Thiết lập khu vực chỗ ngồi + sức chứa vật lý thật — dữ liệu này là nguồn giới hạn thật dùng khi Audience giữ chỗ vé sau này.
- **Loại view:** form (visual editor — kéo thả vị trí 2D/3D).
- **Dữ liệu hiển thị:** `GET /lounges/{id}/zones` → `SeatingZoneDto[]` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** Tạo zone: `POST /lounges/{id}/zones` (Name, Description, Capacity). Sửa: `PUT .../zones/{zoneId}`. Xoá: `DELETE .../zones/{zoneId}`. Đặt vị trí trực quan: `PUT .../layout-2d` (X, Y, Width, Height, RotationDeg, Color) hoặc `.../layout-3d` (X, Y, Z — null cả 3 = gỡ marker). Ảnh nền sơ đồ: `PUT /lounges/{id}/area-layout-image`.
- **Điều kiện hiển thị có điều kiện:** `Capacity` nhập ở đây **chính là** 1 trong 5 lớp quota kiểm tra thật khi Audience giữ chỗ (lớp "sức chứa vật lý", không chỉ logic bán hàng) — cảnh báo rõ trong UI đây không phải con số trang trí.
- **Điều hướng đến (From):** Tạo/Sửa Venue (link "Thiết lập sơ đồ chỗ ngồi").
- **Điều hướng đi (To):** Lưu xong → ở lại editor, hoặc quay về **Tạo/Sửa Venue**.
- **Trạng thái đặc biệt cần thiết kế:** Chưa có ảnh nền sơ đồ → editor vẫn dùng được (toạ độ tự do), chỉ thiếu phần visual tham chiếu.

#### Tour ảo 360° (quản lý)
- **Actor truy cập:** Owner (đúng chủ venue).
- **Mục đích:** Tạo/ghép/sắp xếp các scene panorama cho tour 360°.
- **Loại view:** form + real-time (job ghép ảnh chạy nền, cần poll trạng thái).
- **Dữ liệu hiển thị:** Danh sách scene hiện có (từ `GET /lounges/{id}/tour`).
- **Hành động khả dụng trên view:** Thêm scene thủ công (đã có ảnh panorama sẵn): `POST /lounges/{id}/tour/scenes` (ImageUrl, Name). Ghép tự động từ nhiều ảnh xoay vòng: `POST .../tour/scenes/stitch` (SourceImageUrls[], Name) → trả `attemptId` (202 Accepted) → poll `GET .../stitch/{attemptId}` (Pending/Succeeded/Failed). Đặt vị trí scene trên sơ đồ: `PUT .../scenes/{sceneId}/position`. Gắn hotspot: `POST .../scenes/{sceneId}/hotspots` (Type, Yaw, Pitch, Label, TargetSceneId, InfoText). Xoá scene/hotspot.
- **Điều kiện hiển thị có điều kiện:** Số scene tối đa gate theo gói subscription (`MaxTourScenes`) — disable nút "Thêm scene" khi đã chạm hạn mức, hiện rõ "X/Y scene đã dùng". Ghép ảnh có giới hạn số lần thử riêng (chống lạm dụng CPU server) — độc lập với hạn mức scene.
- **Điều hướng đến (From):** Tạo/Sửa Venue (link "Quản lý Tour 360°").
- **Điều hướng đi (To):** Không điều hướng tiếp — actor có thể mở **Tour ảo 360°** (view công khai) để xem trước kết quả.
- **Trạng thái đặc biệt cần thiết kế:** **Bắt buộc** có polling UI cho trạng thái ghép ảnh (15–30+ giây) — không được thiết kế như thao tác đồng bộ tức thì; `Failed` cần hiện lý do nếu có, cho phép thử lại.

#### Nội dung bổ sung Venue (Gallery & Tiêu chí gợi ý)
- **Actor truy cập:** Owner (đúng chủ venue).
- **Mục đích:** Thêm ảnh showcase và định nghĩa tiêu chí gợi ý riêng của venue.
- **Loại view:** list/form (2 khối trong 1 trang cài đặt phụ).
- **Dữ liệu hiển thị:** Gallery: (không có GET liệt kê riêng trong docs/16 — khả năng nhúng trong `LoungeDetailDto`, chưa xác minh). Tiêu chí: `GET /lounges/{id}/custom-criteria` → `CustomCriteriaDto[]` (chưa xác minh field).
- **Hành động khả dụng trên view:** Gallery: `POST /lounges/{id}/gallery` (ImageUrl, Caption), `DELETE .../gallery/{imageId}`. Tiêu chí: `POST /lounges/{id}/custom-criteria` (Name, Key, DataType, Options), `PUT .../custom-criteria/{criteriaId}` (Name, Options, IsActive).
- **Điều kiện hiển thị có điều kiện:** Gallery **không giới hạn** theo gói subscription (khác Tour 360° có giới hạn `MaxTourScenes`) — UI không cần hiện đồng hồ đo hạn mức ở khối này. `Key`/`DataType` của tiêu chí **không sửa được sau khi tạo** — disable 2 field này ở form Sửa, chỉ cho sửa `Name`/`Options`/`IsActive`.
- **Điều hướng đến (From):** Tạo/Sửa Venue (link).
- **Điều hướng đi (To):** Không điều hướng tiếp.
- **Trạng thái đặc biệt cần thiết kế:** Empty state riêng cho từng khối.

#### Quản lý Nhân viên (Staff)
- **Actor truy cập:** Owner (đúng chủ venue).
- **Mục đích:** Gán/gỡ tài khoản Audience thành Staff vận hành venue.
- **Loại view:** list + form.
- **Dữ liệu hiển thị:** `GET /lounges/{id}/staff` → `LoungeStaffDto[]` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** Tra cứu trước khi gán: `GET /lounges/staff/lookup?email=` → `UserLookupDto`. Gán: `POST /lounges/{id}/staff` (UserId). Gỡ: `DELETE /lounges/{id}/staff/{staffId}`.
- **Điều kiện hiển thị có điều kiện:** Tra cứu trả về User đang là Owner/Admin → chặn gán (`ConflictException`), UI disable nút "Gán" và hiện lý do. User đang là Staff active ở venue khác → chặn tương tự, gợi ý "yêu cầu họ dùng tài khoản khác".
- **Điều hướng đến (From):** Danh sách Venue của tôi / Tạo-Sửa Venue (link).
- **Điều hướng đi (To):** Gán xong → ở lại view, danh sách cập nhật.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Venue chưa có nhân viên nào".

---

### K. Owner — Bank Account

#### Quản lý Tài khoản ngân hàng
- **Actor truy cập:** Owner.
- **Mục đích:** Đăng ký nơi nhận tiền cho venue (và cho từng Performer mình tạo, thay mặt vì Performer không tự làm được).
- **Loại view:** list + form.
- **Dữ liệu hiển thị:** `GET /bank-accounts?ownerType=&ownerId=` → `BankAccountDto[]` (chưa xác minh field cụ thể — chắc chắn có `IsVerified` theo cơ chế Admin xác minh đã biết).
- **Hành động khả dụng trên view:** Tạo: `POST /bank-accounts` (OwnerType=Lounge|Performer, OwnerId, BankName, AccountNumber, AccountHolder, IsDefault). Sửa: `PUT /bank-accounts/{id}`.
- **Điều kiện hiển thị có điều kiện:** Tài khoản chưa `IsVerified` → hiện nhãn "Chờ Admin xác minh", **settlement/donate payout sẽ bị chặn** cho tới khi xác minh xong — UI nên cảnh báo rõ đây là lý do tiền chưa về nếu Owner thắc mắc. **Bắt buộc phải làm bước này trước khi tạo show** (chặn cứng lịch settlement nếu chưa có tài khoản mặc định).
- **Điều hướng đến (From):** Đăng ký tài khoản Owner (bước bắt buộc đầu tiên), Quản lý Hồ sơ Nghệ sĩ (link đăng ký tài khoản cho 1 performer).
- **Điều hướng đi (To):** Không điều hướng tiếp.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Chưa đăng ký tài khoản ngân hàng nào" kèm cảnh báo nổi bật vì đây là điều kiện chặn nhiều luồng tài chính khác.

---

### L. Owner — Subscription

#### Bảng giá gói Subscription
- **Actor truy cập:** Ai cũng xem được (Anonymous) — trang "bảng giá dành cho chủ phòng trà tương lai".
- **Mục đích:** So sánh các gói trước khi đăng ký.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /subscriptions/packages?activeOnly=` → `SubscriptionPackageDto[]` (chưa xác minh field cụ thể — chắc chắn có Name, Price, BillingCycle, MaxTicketsPerEvent, HasAiPoster, MaxAiPostersPerMonth, MaxTourScenes theo field Admin tạo).
- **Hành động khả dụng trên view:** Bấm "Chọn gói" → nếu chưa đăng nhập, dẫn sang Đăng ký/Đăng nhập trước; nếu đã là Owner → **Gói Subscription của tôi** (khởi tạo thanh toán).
- **Điều kiện hiển thị có điều kiện:** Không hiện gói `IsActive=false` (Admin đã ngưng bán).
- **Điều hướng đến (From):** Trang chủ (link "Dành cho chủ phòng trà"), Tạo Show (khi bị chặn vì chưa có subscription).
- **Điều hướng đi (To):** **Gói Subscription của tôi**.
- **Trạng thái đặc biệt cần thiết kế:** Không có gói nào đang bán → empty state (thực tế hiếm xảy ra, nhưng cần xử lý).

#### Gói Subscription của tôi
- **Actor truy cập:** Owner.
- **Mục đích:** Xem gói hiện tại, đăng ký/gia hạn/huỷ.
- **Loại view:** detail.
- **Dữ liệu hiển thị:** `GET /subscriptions/my` → `MySubscriptionDto` (chưa xác minh field cụ thể — chắc chắn có snapshot quyền lợi tại thời điểm mua: MaxTicketsPerEvent, HasAiPoster, MaxTourScenes... không đổi ngược dù Admin sửa gói gốc sau đó).
- **Hành động khả dụng trên view:** Đăng ký mới: `POST /subscriptions/subscribe` (PackageId) → `SubscriptionPaymentInitiationDto`. Gia hạn: `POST /subscriptions/renew` (dùng lại gói lần trước, vẫn cần 1 lần OTP VNPay thật). Huỷ: `POST /subscriptions/cancel`.
- **Điều kiện hiển thị có điều kiện:** Nút "Đăng ký gói mới" disable nếu đang có subscription `Active` khác — phải huỷ trước. Sắp hết hạn → hiện nổi bật nút "Gia hạn" (không tự động trừ tiền được, luôn cần 1 lần thao tác VNPay thật).
- **Điều hướng đến (From):** Bảng giá gói Subscription, Tạo Show (bị chặn vì chưa có Active).
- **Điều hướng đi (To):** Đăng ký/gia hạn → **Kết quả thanh toán** → quay lại view này.
- **Trạng thái đặc biệt cần thiết kế:** Chưa từng có subscription nào → empty state, dẫn thẳng sang Bảng giá.

---

### M. Owner — Show

#### Danh sách Show của tôi
- **Actor truy cập:** Owner.
- **Mục đích:** Quản lý tổng quan mọi show mình tạo, mọi trạng thái.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /lounge-shows?mine=true` → `PaginatedResult<LoungeShowListItemDto>` — thấy cả `Draft`/`Pending`/`Published`, khác public chỉ thấy `Published`.
- **Hành động khả dụng trên view:** Nút "Tạo show mới" → **Tạo/Sửa Show**.
- **Điều kiện hiển thị có điều kiện:** Show `Draft` chưa nộp duyệt → hiện nhãn "Bản nháp", link vào **Tạo/Sửa Show** để tiếp tục. Show `Pending` (đã nộp, chờ Admin) → chỉ xem, không sửa được.
- **Điều hướng đến (From):** Menu Owner.
- **Điều hướng đi (To):** Bấm 1 show → **Bảng điều khiển Show (Owner)**.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Bạn chưa tạo show nào".

#### Tạo/Sửa Show
- **Actor truy cập:** Owner — chỉ sửa được khi show còn `Draft`.
- **Mục đích:** Nhập thông tin show + xếp lineup nghệ sĩ.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Khi sửa: `GET /lounge-shows/{id}`.
- **Hành động khả dụng trên view:** `POST /lounge-shows` (Name, Description, Format, ScheduledStart/End, CategoryId, Offline/OnlineQuota, GenreIds[], Performances[] (PerformerId hoặc tên mới, Role, OrderIndex, SetTime, AcceptsDonation), CustomValues[]). Sửa: `PUT /lounge-shows/{id}`.
- **Điều kiện hiển thị có điều kiện:** Chặn tạo nếu Owner **chưa có subscription Active tại đúng thời điểm** submit (không phải lúc mở form) — UI nên kiểm tra trước và banner cảnh báo nếu phát hiện chưa có gói, thay vì để actor điền hết form rồi mới báo lỗi. Nhập tên nghệ sĩ **không có** `PerformerId` (nghệ sĩ mới, chưa có trong catalog) → tự động tạo `Performer` mới ngay khi submit — UI nên có ô tìm-kiếm-hoặc-tạo-mới (autocomplete + "tạo mới nếu không có") thay vì 2 form tách biệt. Số nghệ sĩ/show **không giới hạn** (0..n), chỉ ràng buộc 1 nghệ sĩ không trùng lặp trong cùng show.
- **Điều hướng đến (From):** Danh sách Show của tôi.
- **Điều hướng đi (To):** Tạo xong (Draft) → **Bảng điều khiển Show (Owner)** (tiếp tục thêm hạng vé/poster/pháp lý trước khi nộp duyệt).
- **Trạng thái đặc biệt cần thiết kế:** Loading khi submit (tạo Performer mới đồng thời có thể chậm hơn 1 chút).

#### Quản lý Hạng vé (Ticket Tier)
- **Actor truy cập:** Owner — chỉ tạo được khi show còn `Draft`.
- **Mục đích:** Thiết lập hạng vé + đợt giá cho show.
- **Loại view:** list + form.
- **Dữ liệu hiển thị:** `GET /ticket-tiers?showId=` → `TicketTierSummaryDto[]`.
- **Hành động khả dụng trên view:** Tạo: `POST /ticket-tiers` (ShowId, Name, Description, AccessType, ZoneId nếu Physical, TotalCapacity, Prices[]). Sửa: `PUT /ticket-tiers/{id}`. Xoá: `DELETE /ticket-tiers/{id}`.
- **Điều kiện hiển thị có điều kiện:** Chỉ tạo/sửa được khi show `Draft` — ẩn nút "Thêm hạng vé" nếu show đã qua trạng thái này. Tổng `TotalCapacity` mọi tier cộng lại **không được vượt hạn mức gói subscription** (`MaxTicketsPerEvent`) — UI nên hiện thanh tiến độ "đã dùng X/Y vé theo gói" để Owner tự canh, dù kiểm tra thật nằm ở BE (và kiểm tra lại lần 2 lúc Audience giữ chỗ).
- **Điều hướng đến (From):** Bảng điều khiển Show (Owner) (link).
- **Điều hướng đi (To):** Không điều hướng tiếp — quay lại Bảng điều khiển Show (Owner) khi xong.
- **Trạng thái đặc biệt cần thiết kế:** Cần **≥1 hạng vé** mới nộp duyệt được — nếu danh sách rỗng, banner nhắc ngay tại đây thay vì để actor phát hiện muộn ở bước Nộp duyệt.

#### Poster Show
- **Actor truy cập:** Owner.
- **Mục đích:** Tạo poster bằng AI hoặc tự upload thủ công.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Poster hiện tại (nếu có), lịch sử tạo AI: `GET /lounge-shows/{id}/ai-poster/history` → `PosterGenerationAttemptDto[]` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** Tạo AI: `POST /lounge-shows/{id}/ai-poster` (StyleHint?) → `PosterGenerationResultDto`. Upload thủ công: `POST /uploads/images` → `PUT /lounge-shows/{id}/poster` (ImageUrl). Ảnh cover riêng: `PUT /lounge-shows/{id}/cover-image`.
- **Điều kiện hiển thị có điều kiện:** Nút "Tạo bằng AI" chỉ hiện/enable nếu gói subscription snapshot có `HasAiPosterSnapshot=true` (kiểm tra trong handler, không phải tầng policy — nghĩa là ẩn ở UI vẫn cần kiểm tra thật ở BE, không tin tưởng tuyệt đối UI-only) + còn hạn mức tháng (`MaxAiPostersPerMonth`) + chưa vượt giới hạn chống-lạm-dụng riêng theo show.
- **Điều hướng đến (From):** Bảng điều khiển Show (Owner) (link, thường ngay sau Tạo/Sửa Show).
- **Điều hướng đi (To):** Không điều hướng tiếp.
- **Trạng thái đặc biệt cần thiết kế:** Tạo AI đang xử lý (có thể mất thời gian) → loading rõ ràng; thất bại → hiện lý do nếu có, không mất lượt hạn mức đã trừ oan (chưa xác minh BE có hoàn lại lượt khi thất bại hay không — cần hỏi lại nếu quan trọng cho UX).

#### Khai báo pháp lý & Tác quyền
- **Actor truy cập:** Owner.
- **Mục đích:** Khai báo giấy phép biểu diễn (NĐ 144/2020) và tác quyền VCPMC — điều kiện bắt buộc trước khi phát livestream.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Giá trị hiện tại (nếu đã khai) từ `GET /lounge-shows/{id}`.
- **Hành động khả dụng trên view:** `PUT /lounge-shows/{id}/legal-approval` (LegalApprovalReference). `PUT /lounge-shows/{id}/vcpmc-royalty` (VcpmcRoyaltyReference).
- **Điều kiện hiển thị có điều kiện:** Thiếu `VcpmcRoyaltyReference` → **chặn cứng** `POST /livestreams/{id}/start` sau này (ở view **Vận hành Livestream**) — banner cảnh báo ngay tại đây nếu show có Format Online mà field này còn trống.
- **Điều hướng đến (From):** Bảng điều khiển Show (Owner) (link).
- **Điều hướng đi (To):** Không điều hướng tiếp.
- **Trạng thái đặc biệt cần thiết kế:** Không có validate định dạng đặc biệt được ghi nhận — chỉ là text reference, không phải file upload.

#### Bảng điều khiển Show (Owner)
- **Actor truy cập:** Owner (đúng chủ show), Staff/Admin xem/thao tác 1 phần (start/end qua `RequireVenueOperator`).
- **Mục đích:** Trung tâm quản lý vòng đời 1 show — từ Draft tới Ended.
- **Loại view:** dashboard.
- **Dữ liệu hiển thị:** `GET /lounge-shows/{id}` (đầy đủ field quản trị) + `GET /lounge-shows/{id}/orders` → `PaginatedResult<ShowOrderDto>` (danh sách đơn hàng vé của show).
- **Hành động khả dụng trên view:** Nộp duyệt: `POST /lounge-shows/{id}/publish`. Đổi lịch: `POST /lounge-shows/{id}/reschedule` (NewScheduledStart). Đổi format: `PUT /lounge-shows/{id}/format` (NewFormat). Đổi chế độ phát: `PUT /lounge-shows/{id}/playback-mode`. Huỷ show: `POST /lounge-shows/{id}/cancel`. Bắt đầu/kết thúc (show Offline thuần): `POST .../start`, `POST .../end`.
- **Điều kiện hiển thị có điều kiện:** Nút "Nộp duyệt" disable nếu venue chưa `Approved` hoặc chưa có ≥1 hạng vé. Nút "Đổi lịch"/"Huỷ" chỉ hiện khi show `Published` (chưa `Ongoing`) — riêng "Huỷ" cũng hiện được ở `Ongoing` nhưng chặn nếu livestream đang `Live` (phải terminate trước, ở **Vận hành Livestream**). "Đổi Offline→Online" chỉ 1 chiều, không đổi ngược — cảnh báo rõ trước khi xác nhận vì **tự động hoàn 100% mọi vé vật lý đã Confirmed**. Nút "Bắt đầu/Kết thúc" (Offline) **tự ẩn** nếu show có Livestream đi kèm — dùng cặp lệnh riêng ở Vận hành Livestream thay thế.
- **Điều hướng đến (From):** Danh sách Show của tôi.
- **Điều hướng đi (To):** Link sang **Quản lý Hạng vé**, **Poster Show**, **Khai báo pháp lý & Tác quyền**, **Vận hành Livestream** (nếu Online).
- **Trạng thái đặc biệt cần thiết kế:** Show bị Admin từ chối duyệt (`Rejected`→`Draft`) → hiện rõ `ReviewNote` lý do từ chối, không chỉ nhãn trạng thái suông.

---

### N. Owner/Staff — Livestream ops

#### Vận hành Livestream
- **Actor truy cập:** Owner/Staff của venue đó (`RequireVenueOperator`).
- **Mục đích:** Cấu hình, bắt đầu/kết thúc phát, theo dõi viewer/chat trong lúc phát.
- **Loại view:** dashboard/real-time.
- **Dữ liệu hiển thị:** `GET /livestreams/{id}/credentials` → RTMP URL + Stream Key (**không lộ ra khán giả**). `GET /livestreams/{id}/chat`. `Livestream.ViewerCount` (đếm nguyên tử tại DB, đọc lại qua polling/refresh — không tự đẩy realtime tới Owner/Staff).
- **Hành động khả dụng trên view:** Tạo Livestream cho show: `POST /livestreams` (ShowId). Bắt đầu: `POST /livestreams/{id}/start`. Kết thúc: `POST /livestreams/{id}/end`.
- **Điều kiện hiển thị có điều kiện:** Tạo Livestream **tự động** kéo theo 1 vòng kiểm duyệt riêng (`EventModeration(TargetType=Livestream)`, độc lập với duyệt Show) — nút "Bắt đầu phát" disable cho tới khi Admin duyệt xong **và** đã khai báo `VcpmcRoyaltyReference` (2 điều kiện độc lập, UI nên hiện rõ đang thiếu điều kiện nào).
- **Điều hướng đến (From):** Bảng điều khiển Show (Owner) (link, chỉ hiện nếu show Online).
- **Điều hướng đi (To):** Bắt đầu phát thành công → show đồng thời chuyển `Ongoing` (phản ánh ở Bảng điều khiển Show). Kết thúc → show `Ended`, mở cửa sổ đánh giá cho khán giả.
- **Trạng thái đặc biệt cần thiết kế:** Chưa được Admin duyệt → hiện trạng thái "Đang chờ kiểm duyệt", không phải nút disable trơ không rõ lý do; bị Admin `terminate` giữa lúc phát → cần hiện rõ khác với "Kết thúc" bình thường (do vi phạm, kèm `Reason`).

---

### O. Owner — Donate handling

#### Donate chờ xác nhận
- **Actor truy cập:** Owner.
- **Mục đích:** Xác nhận đã nhận tiền donate qua VNPay cho nghệ sĩ tại venue mình.
- **Loại view:** list.
- **Dữ liệu hiển thị:** `GET /donations/pending-ack` → `PaginatedResult<PendingDonationDto>` (Id, PerformerName, ShowName, Gross, Net, AmountToPayPerformer, DisplayName, Message, Deadline).
- **Hành động khả dụng trên view:** `POST /donations/{id}/acknowledge` → 204.
- **Điều kiện hiển thị có điều kiện:** Hiện đồng hồ đếm ngược tới `Deadline` (24h) — không thao tác kịp thì hệ thống **tự động** chuyển `OwnerReceived` giùm (`AutoConfirmed=true`), kèm cảnh báo `DonationPending` nếu quá hạn — UI nên phân biệt rõ "Owner tự xác nhận" vs "hệ thống tự động xác nhận giùm" trong lịch sử để tránh hiểu nhầm.
- **Điều hướng đến (From):** Hộp thư thông báo (`DonationReceived`), Menu Owner.
- **Điều hướng đi (To):** Acknowledge xong → item chuyển sang **Donate chờ trả nghệ sĩ**.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Không có donate nào đang chờ xác nhận".

#### Donate chờ trả nghệ sĩ
- **Actor truy cập:** Owner.
- **Mục đích:** Ghi nhận đã chuyển khoản thủ công cho nghệ sĩ.
- **Loại view:** list + form.
- **Dữ liệu hiển thị:** `GET /donations/awaiting-payout` → cùng shape `PendingDonationDto`, khác filter status.
- **Hành động khả dụng trên view:** `POST /donations/{id}/confirm-paid` (PaymentRef, PaymentEvidenceUrl — ảnh chụp chuyển khoản) → 204.
- **Điều kiện hiển thị có điều kiện:** BE **fail-closed** nếu Performer chưa có `BankAccount` mặc định đã đăng ký — nút "Xác nhận đã trả" nên disable kèm link nhanh sang **Quản lý Tài khoản ngân hàng** nếu phát hiện thiếu, thay vì để Owner bấm rồi nhận lỗi.
- **Điều hướng đến (From):** Hộp thư thông báo, Menu Owner.
- **Điều hướng đi (To):** Xác nhận xong → item biến mất khỏi danh sách (đã hoàn tất chu trình donate).
- **Trạng thái đặc biệt cần thiết kế:** Yêu cầu upload `PaymentEvidenceUrl` — cần preview ảnh trước khi submit.

---

### P. Owner — Tài chính

#### Thu nhập tổng quan
- **Actor truy cập:** Owner.
- **Mục đích:** Xem tổng thu nhập từ mọi venue mình sở hữu.
- **Loại view:** dashboard.
- **Dữ liệu hiển thị:** `GET /me/earnings` → `EarningsSummaryDto` (chưa xác minh field cụ thể — tổng hợp Settlement/Payment/Donation).
- **Hành động khả dụng trên view:** Chỉ xem.
- **Điều kiện hiển thị có điều kiện:** Settlement bị chặn vì chưa có/chưa xác minh bank account → hiện rõ trong dashboard dạng "đang tạm giữ, cần xử lý" thay vì im lặng không hiện gì (Owner dễ hiểu nhầm là mất tiền).
- **Điều hướng đến (From):** Menu Owner.
- **Điều hướng đi (To):** Không điều hướng tiếp — link phụ sang **Quản lý Tài khoản ngân hàng** nếu phát hiện vướng.
- **Trạng thái đặc biệt cần thiết kế:** Chưa có venue nào bán được vé → empty/zero state.

#### Thống kê Venue
- **Actor truy cập:** Owner.
- **Mục đích:** Xem thống kê chi tiết 1 venue cụ thể (khác tổng hợp toàn bộ ở Thu nhập tổng quan).
- **Loại view:** dashboard.
- **Dữ liệu hiển thị:** `GET /analytics/my-lounge?loungeId=` → `OwnerAnalyticsDto` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** Chọn venue (nếu Owner có nhiều hơn 1) để xem riêng.
- **Điều kiện hiển thị có điều kiện:** Không có — endpoint riêng biệt với `GET /analytics/platform` (Admin), không phải cùng 1 endpoint check quyền bên trong.
- **Điều hướng đến (From):** Danh sách Venue của tôi (link).
- **Điều hướng đi (To):** Không điều hướng tiếp.
- **Trạng thái đặc biệt cần thiết kế:** Venue mới, chưa có show nào diễn → empty state.

---

### Q. Owner — Xử phạt

#### Xử phạt & Kháng cáo của tôi
- **Actor truy cập:** Owner.
- **Mục đích:** Xem lịch sử xử phạt venue mình và gửi kháng cáo.
- **Loại view:** list/detail + form.
- **Dữ liệu hiển thị:** `GET /venue-penalties/mine` → `PaginatedResult<VenuePenaltyDto>` (mọi trạng thái, mọi lounge của Owner). Chi tiết: `GET /venue-penalties/{id}`.
- **Hành động khả dụng trên view:** `POST /venue-penalties/{id}/appeal` (AppealReason) → 204.
- **Điều kiện hiển thị có điều kiện:** Nút "Kháng cáo" chỉ hiện khi phạt đang `Active` (không hiện với phạt đã `Overturned`/`Upheld`/đã hết hạn). Không xử lý kịp SLA → hệ thống **tự động Overturn** giùm — UI cần phân biệt rõ "Admin overturn" vs "tự động overturn do quá SLA" trong lịch sử.
- **Điều hướng đến (From):** Hộp thư thông báo (quyết định xử phạt mới), Menu Owner.
- **Điều hướng đi (To):** Gửi kháng cáo → ở lại view, trạng thái chuyển `Appealed`, chờ Admin xử lý.
- **Trạng thái đặc biệt cần thiết kế:** `Overturned` nhưng venue **không tự mở lại** nếu còn phạt Active khác chồng lên — UI cần hiện rõ trạng thái tổng hợp thật của venue (còn bị khoá phần nào) chứ không chỉ dựa vào trạng thái của riêng 1 phạt vừa overturn.

---

### R. Owner/Admin — Performer

#### Quản lý Hồ sơ Nghệ sĩ
- **Actor truy cập:** Owner (bất kỳ, CREATE/READ/ASSIGN mở cho mọi Owner) — EDIT/DELETE chỉ đúng người tạo (`CreatedByUserId`) hoặc Admin (kiểm tra ở tầng handler, không phải policy).
- **Mục đích:** Tạo/tìm/sửa hồ sơ nghệ sĩ trong catalog dùng chung toàn nền tảng.
- **Loại view:** list + form.
- **Dữ liệu hiển thị:** `GET /performers?search=` → `PaginatedResult<PerformerDto>` (Id, Name, AvatarUrl, Bio, Type, CreatedByUserId, GenreIds, GenreNames, SocialLinks).
- **Hành động khả dụng trên view:** Tạo: `POST /performers` (Name, AvatarUrl, Bio, Type, GenreIds[]). Sửa: `PUT /performers/{id}` (như trên). Xoá: `DELETE /performers/{id}`. Social links: `PUT /performers/{id}/social-links` (Platform, Url, DisplayName — upsert), `DELETE .../social-links/{linkId}`.
- **Điều kiện hiển thị có điều kiện:** Nút "Sửa"/"Xoá" **ẩn** (không chỉ disable) nếu actor không phải người tạo và không phải Admin — tránh Owner khác tưởng sửa được rồi nhận 403. Nút "Xoá" disable + tooltip nếu performer **đã từng** được xếp lịch ở bất kỳ show nào (kể cả show cũ đã kết thúc — `ON DELETE RESTRICT`, giữ lịch sử).
- **Điều hướng đến (From):** Tạo/Sửa Show (khi cần tìm/tạo nghệ sĩ cho lineup), Menu Owner.
- **Điều hướng đi (To):** Link sang **Quản lý Tài khoản ngân hàng** (ownerType=Performer) để đăng ký nơi nhận donate cho nghệ sĩ này.
- **Trạng thái đặc biệt cần thiết kế:** Empty state khi tìm kiếm không ra kết quả (gợi ý "Tạo hồ sơ mới cho nghệ sĩ này" ngay tại đây).

---

### S. Staff — Vận hành sàn

#### Bán vé tại quầy (Walk-in)
- **Actor truy cập:** Staff (đúng venue được gán qua `lounge_id`), Owner.
- **Mục đích:** Bán vé trực tiếp cho khách tới quầy, không qua app.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Hạng vé + giá còn lại (`GET /ticket-tiers?showId=`, lọc `AccessType=Physical`).
- **Hành động khả dụng trên view:** `POST /tickets/walk-in` (PriceId, Quantity) → `WalkInSaleResultDto` (chưa xác minh field cụ thể — chắc chắn có QR sinh ngay).
- **Điều kiện hiển thị có điều kiện:** Chỉ hiện/bán được hạng vé `AccessType=Physical` (không bán walk-in cho vé online) và đợt giá phải cho phép kênh Offline. Không qua VNPay — `Payment.Status=Confirmed` ngay, `Method=Cash`. Mặc định **không tính hoa hồng** nền tảng (`WalkInCommissionEnabled` tắt mặc định) — không hiện thông tin hoa hồng gây hiểu nhầm trên biên lai in ra.
- **Điều hướng đến (From):** Menu Staff (vận hành quầy).
- **Điều hướng đi (To):** Bán xong → hiện QR ngay tại chỗ để đưa khách (không cần điều hướng, khách này không có tài khoản trong hệ thống — `BuyerId=null`).
- **Trạng thái đặc biệt cần thiết kế:** Hết vé Physical → disable, hiện rõ lý do (không phải lỗi mạng).

#### Check-in vé
- **Actor truy cập:** Staff (đúng venue được gán).
- **Mục đích:** Quét QR xác nhận khán giả vào cửa.
- **Loại view:** real-time/form (giao diện quét camera).
- **Dữ liệu hiển thị:** Xem trước (tách riêng khỏi check-in thật): `GET /tickets/by-qr/{qrCode}` → `TicketDetailDto`.
- **Hành động khả dụng trên view:** `POST /tickets/check-in` (QrCode) → `TicketDetailDto`.
- **Điều kiện hiển thị có điều kiện:** Chặn (kèm message rõ) nếu: show chưa/đã qua giờ diễn (không `Ongoing`), vé không phải `AccessType=Physical` (vé online không cần check-in cửa), vé chưa `Confirmed`, **đã check-in trước đó** (409 — chống quét trùng), hoặc vé đang trong quá trình chuyển nhượng ("đóng băng"). Mỗi lý do chặn cần message riêng, không gộp chung "vé không hợp lệ".
- **Điều hướng đến (From):** Menu Staff (vận hành cửa).
- **Điều hướng đi (To):** Check-in thành công → ở lại view (sẵn sàng quét vé tiếp theo), hiện brief xác nhận (tên khán giả/hạng vé) trong 1-2 giây rồi tự reset.
- **Trạng thái đặc biệt cần thiết kế:** **Không có cơ chế offline fallback** — mất mạng lúc quét là rủi ro đã biết và chấp nhận (phạm vi capstone) — UI nên hiện rõ trạng thái mất kết nối thay vì cho phép quét "có vẻ như" thành công.

#### Quản lý đơn F&B tại quầy
- **Actor truy cập:** Staff/Owner (đúng venue).
- **Mục đích:** Xem và cập nhật tiến độ chế biến/phục vụ mọi đơn F&B của venue.
- **Loại view:** list (dạng bảng/kanban theo trạng thái).
- **Dữ liệu hiển thị:** `GET /fnb-orders?loungeId=&status=` → `PaginatedResult<FnbOrderDto>` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** Đặt hộ khách tại quầy: `POST /fnb-orders` (Role=Staff/Owner, gắn `StaffId` thay vì `AudienceUserId`). Cập nhật trạng thái: `PUT /fnb-orders/{id}/status`.
- **Điều kiện hiển thị có điều kiện:** Chuỗi trạng thái **bắt buộc tuần tự** `Pending→Preparing→Served→Paid`, không nhảy cóc/lùi — UI chỉ nên hiện đúng 1 nút "bước tiếp theo" mỗi lúc, không phải dropdown chọn tự do trạng thái bất kỳ. `Cancelled` là lối thoát riêng, dùng được ở bất kỳ bước nào trước `Paid`. Đánh dấu `Paid` **không** kiểm tra Staff có thực thu tiền hay không (chỉ là field trạng thái) — rủi ro gian lận nội bộ đã ghi nhận, không phải việc Staff cần lo nhưng Owner/Admin đối soát cần biết.
- **Điều hướng đến (From):** Menu Staff/Owner.
- **Điều hướng đi (To):** Không điều hướng tiếp — khách tự theo dõi ở **Đơn F&B của tôi** (không có push riêng báo khách khi Staff đổi trạng thái).
- **Trạng thái đặc biệt cần thiết kế:** Empty state theo từng cột trạng thái (vd cột "Đang chuẩn bị" trống).

---

### T. Admin — Duyệt/Kiểm duyệt

#### Duyệt Venue mới
- **Actor truy cập:** Admin.
- **Mục đích:** Xét duyệt venue mới đăng ký trước khi cho phép hoạt động.
- **Loại view:** list + action.
- **Dữ liệu hiển thị:** `GET /admin/lounges/pending` → `PaginatedResult<PendingLoungeDto>` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** Duyệt: `POST /admin/lounges/{id}/approve`. Từ chối: `POST /admin/lounges/{id}/reject` (Reason).
- **Điều kiện hiển thị có điều kiện:** Không có điều kiện ẩn/hiện đặc biệt — mọi venue trong hàng chờ đều xử lý được ngay.
- **Điều hướng đến (From):** Menu Admin, Hộp thư thông báo (venue mới đăng ký — chưa xác minh có notify Admin hay chỉ hiện trong danh sách chờ, cần kiểm tra thêm nếu muốn có badge số lượng đang chờ).
- **Điều hướng đi (To):** Xử lý xong → item biến mất khỏi hàng chờ.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Không có venue nào đang chờ duyệt".

#### Kiểm duyệt Show & Livestream
- **Actor truy cập:** Admin.
- **Mục đích:** Xét duyệt nội dung show/livestream trước khi công khai/phát được.
- **Loại view:** list + action.
- **Dữ liệu hiển thị:** `GET /moderations/pending?targetType=` → `PaginatedResult<EventModerationDto>` (lọc `show`/`livestream`) — kèm điểm rủi ro AI đã chấm trước (enqueue tự động lúc Owner nộp duyệt/tạo livestream).
- **Hành động khả dụng trên view:** Show: `POST /moderations/shows/{id}/review` (Decision=Approved|Rejected, ReviewNote?). Livestream: `POST /moderations/livestreams/{id}/review` (tương tự).
- **Điều kiện hiển thị có điều kiện:** `ReviewNote` **bắt buộc** khi `Decision=Rejected` (form validate) — không bắt buộc khi Approved. Điểm rủi ro AI chỉ là **gợi ý**, Admin toàn quyền quyết định khác đi.
- **Điều hướng đến (From):** Menu Admin.
- **Điều hướng đi (To):** Duyệt xong → item biến mất khỏi hàng chờ; nếu duyệt show → công khai ngay cho Audience; nếu duyệt livestream → mở khoá nút "Bắt đầu phát" cho Owner/Staff.
- **Trạng thái đặc biệt cần thiết kế:** Empty state riêng theo từng tab (show/livestream).

---

### U. Admin — Xử phạt & Kháng cáo

#### Ra quyết định Xử phạt Venue
- **Actor truy cập:** Admin.
- **Mục đích:** Ra quyết định xử phạt 1 venue vi phạm.
- **Loại view:** form.
- **Dữ liệu hiển thị:** Thông tin venue đang xét (từ Chi tiết Venue hoặc danh sách venue nội bộ Admin).
- **Hành động khả dụng trên view:** `POST /venue-penalties` (LoungeId, PenaltyType=Warning|Suspension|Ban, Reason, EvidenceRef, SuspensionDays? nếu Suspension) → int id.
- **Điều kiện hiển thị có điều kiện:** Field `SuspensionDays` chỉ hiện khi `PenaltyType=Suspension`.
- **Điều hướng đến (From):** Chi tiết Venue (Admin xem), Quản lý Người dùng.
- **Điều hướng đi (To):** Tạo xong → Owner nhận thông báo ngay.
- **Trạng thái đặc biệt cần thiết kế:** Không có gì đặc biệt ngoài validate form thường.

#### Xử lý Kháng cáo
- **Actor truy cập:** Admin.
- **Mục đích:** Ra quyết định giữ nguyên hoặc huỷ 1 quyết định xử phạt đang bị kháng cáo.
- **Loại view:** detail + action.
- **Dữ liệu hiển thị:** `GET /venue-penalties/{id}` → `VenuePenaltyDto` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** `POST /venue-penalties/{id}/appeal/review` (Decision=Overturned|Upheld, ReviewNote).
- **Điều kiện hiển thị có điều kiện:** Chỉ xử lý được kháng cáo đang `Appealed`. **Gap đã ghi nhận** ở [20 Journey 3 bước 2](20-admin-journey.md#journey-3--xử-phạt-venue--xử-lý-kháng-cáo): **không có endpoint danh sách mọi kháng cáo đang mở** — Admin phải biết `id` trước (qua thông báo hoặc tra từng venue). FE cần tự dựng 1 "danh sách kháng cáo" bằng cách lọc phía client từ dữ liệu khác (vd duyệt qua `GET /venue-penalties` theo từng venue), hoặc đề xuất BE bổ sung endpoint riêng nếu view này cần dùng thường xuyên. `Overturned` **không tự mở lại venue** nếu còn phạt Active khác chồng lên, và **không tự hoàn tác** ảnh hưởng tài chính (co ngắn subscription) — Admin phải tự vào sửa `owner_subscriptions`/ledger thủ công (hiện **không có UI riêng** cho việc sửa tay này — ngoài phạm vi các endpoint đã liệt kê).
- **Điều hướng đến (From):** Ra quyết định Xử phạt Venue (nếu biết id), hoặc trực tiếp theo link thông báo.
- **Điều hướng đi (To):** Xử lý xong → Owner nhận kết quả.
- **Trạng thái đặc biệt cần thiết kế:** Do thiếu endpoint danh sách, view này **có nguy cơ không có "entry point" tự nhiên** trong điều hướng UI — cần quyết định thiết kế bù (vd luôn đính kèm `id` trong nội dung thông báo Admin nhận được để bấm thẳng vào).

---

### V. Admin — Tài chính

#### Yêu cầu hoàn tiền (Refund Requests)
- **Actor truy cập:** Admin.
- **Mục đích:** Duyệt/từ chối yêu cầu hoàn tiền vé.
- **Loại view:** list + action.
- **Dữ liệu hiển thị:** `GET /admin/refund-requests` → `PaginatedResult<RefundRequestDto>` — sinh ra từ 3 nguồn: Audience tự huỷ vé, Owner huỷ show, hoặc Admin resolve khiếu nại (take-down).
- **Hành động khả dụng trên view:** Từ chối: `POST /admin/refund-requests/{id}/process` (Decision=Rejected). Duyệt: (Decision=Approved, ApprovedAmount? — mặc định = AmountRequested nếu bỏ trống). Tạo thủ công (escape-hatch): `POST /admin/refund-requests` (tương đương field `CreateRefundRequestCommand`, chưa xác minh field cụ thể).
- **Điều kiện hiển thị có điều kiện:** Duyệt **chỉ đảo bút toán sổ cái nội bộ — không gọi API hoàn tiền thật của VNPay** (giới hạn môi trường sandbox capstone, không phải thiếu sót) — UI nên hiện rõ đây là xử lý nội bộ, không phải "tiền đã về tài khoản ngân hàng khán giả" theo nghĩa VNPay thật.
- **Điều hướng đến (From):** Menu Admin, Hộp thư thông báo (refund request mới — chưa xác minh có notify hay chỉ vào danh sách).
- **Điều hướng đi (To):** Xử lý xong → Audience liên quan nhận kết quả.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Không có yêu cầu nào đang chờ".

#### Hoàn Donate
- **Actor truy cập:** Admin.
- **Mục đích:** Hoàn 1 donate cụ thể (đảo bút toán).
- **Loại view:** form (thao tác nhanh, không có danh sách duyệt riêng).
- **Dữ liệu hiển thị:** Không có — actor cần biết `DonationId` trước (thường từ **Xử lý Khiếu nại** khi Admin quyết định hoàn donate theo khiếu nại, hoặc tra trực tiếp).
- **Hành động khả dụng trên view:** `POST /admin/donations/{id}/refund` (Reason) → 204.
- **Điều kiện hiển thị có điều kiện:** Chỉ hợp lệ **trước khi** Owner xác nhận đã trả nghệ sĩ (chặng 2, `PerformerPaid`) — nếu donate đã qua giai đoạn này, form nên disable kèm giải thích thay vì để Admin submit rồi nhận lỗi. Đảo bút toán chặng 1, **không gọi VNPay** (donate không có `Payment` row riêng để dùng lại cơ chế refund vé).
- **Điều hướng đến (From):** Xử lý Khiếu nại (khi ResolvedAction liên quan donate — lưu ý: theo docs/20 J6, `ResolvedAction=Refund/Compensate` hiện chỉ tự động tạo RefundRequest cho target **ticket**, không tự động cho donate — Admin phải vào đây thao tác tay riêng nếu khiếu nại liên quan donate).
- **Điều hướng đi (To):** Hoàn xong → donor + Owner nhận thông báo.
- **Trạng thái đặc biệt cần thiết kế:** **Không có danh sách "duyệt donate cần hoàn"** — đây là hành động chủ động đơn lẻ, không phải hàng chờ như Refund Requests vé. Cân nhắc thêm 1 ô tìm-kiếm-theo-id ở đầu form vì không có điểm vào tự nhiên khác.

#### Xác minh Tài khoản ngân hàng
- **Actor truy cập:** Admin.
- **Mục đích:** Xác minh thủ công 1 bank account Owner đã đăng ký (đối soát ngoài hệ thống, không có API ngân hàng tích hợp).
- **Loại view:** action (không có danh sách duyệt riêng — **gap tương tự Hoàn Donate**).
- **Dữ liệu hiển thị:** Không có endpoint liệt kê "tài khoản đang chờ xác minh" trong toàn bộ 209 endpoint đã đọc — Admin phải biết `id` trước qua kênh khác.
- **Hành động khả dụng trên view:** `POST /admin/bank-accounts/{id}/verify` → 204.
- **Điều kiện hiển thị có điều kiện:** Không có điều kiện chặn — verify được bất kỳ lúc nào theo id.
- **Điều hướng đến (From):** Không có điểm vào tự nhiên trong điều hướng UI hiện tại — **cần quyết định thiết kế bù** (đề xuất: hoặc Admin tra qua `GET /admin/users/{id}` rồi Owner đó liên kết sang bank account của họ nếu có endpoint phụ trợ chưa được liệt kê, hoặc đề xuất BE bổ sung `GET /admin/bank-accounts?verified=false`).
- **Điều hướng đi (To):** Verify xong → Owner thấy settlement bị chặn trước đó **tự động retry ngay**, phản ánh ở **Thu nhập tổng quan** của họ.
- **Trạng thái đặc biệt cần thiết kế:** Đây là view **thiếu hụt entry-point rõ ràng nhất** trong toàn bộ catalog — nên ưu tiên hỏi lại BE có định bổ sung endpoint liệt kê hay Admin thực tế vận hành qua kênh khác (email đối soát thủ công?) trước khi thiết kế UI cho nó.

#### Kiểm tra Toàn vẹn Sổ cái
- **Actor truy cập:** Admin.
- **Mục đích:** Công cụ vận hành — quét lệch `SUM(debit)≠SUM(credit)` theo từng `JournalId`.
- **Loại view:** dashboard.
- **Dữ liệu hiển thị:** `GET /admin/ledger/integrity-check` → `LedgerIntegrityIssueDto[]` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** Chỉ xem — không có hành động sửa trực tiếp từ đây (job nền `LedgerIntegrityCheckJob` chạy định kỳ, cảnh báo mọi Admin nếu lệch; đây là công cụ Admin tự kiểm tra thủ công thêm).
- **Điều kiện hiển thị có điều kiện:** Không có — công cụ vận hành nội bộ, không phải use case người dùng cuối.
- **Điều hướng đến (From):** Menu Admin (khu vực công cụ vận hành), Hộp thư thông báo (cảnh báo job tự động phát hiện lệch).
- **Điều hướng đi (To):** Không điều hướng tiếp — phát hiện lệch cần xử lý thủ công ngoài UI (can thiệp DB trực tiếp).
- **Trạng thái đặc biệt cần thiết kế:** Empty (không lệch gì) là kết quả **tốt** — cần thiết kế rõ ràng khác với "chưa tải xong"/"lỗi".

---

### W. Admin — Khiếu nại

#### Xử lý Khiếu nại (Admin)
- **Actor truy cập:** Admin.
- **Mục đích:** Xem và ra quyết định xử lý mọi khiếu nại, gồm cả từ khách vãng lai.
- **Loại view:** list + action.
- **Dữ liệu hiển thị:** `GET /admin/complaints` → `PaginatedResult<ComplaintDto>`.
- **Hành động khả dụng trên view:** `POST /admin/complaints/{id}/resolve` (Status, Resolution, ResolvedAction, RefundAmount?) → 204.
- **Điều kiện hiển thị có điều kiện:** 3 nhánh theo `ResolvedAction`: (a) `Refund`/`Compensate` **chỉ** tự động tạo `RefundRequest` khi target là **1 vé cụ thể** (`TargetType="ticket"`) — target khác (show/venue/donation/penalty) không tự động, Admin phải tự thao tác thêm (donation → sang **Hoàn Donate**); (b) `TakeDownContent` chỉ áp dụng `TargetType="show"` — gỡ show + hoàn 100% mọi vé `Confirmed`; (c) các action khác chỉ ghi nhận, không tự động hoá gì thêm. Field `RefundAmount` chỉ nên hiện khi `ResolvedAction` là Refund/Compensate.
- **Điều hướng đến (From):** Menu Admin.
- **Điều hướng đi (To):** Xử lý xong → người khiếu nại nhận kết quả: in-app (**Khiếu nại của tôi**) nếu có tài khoản, hoặc SMS chủ động tới `ContactPhone` nếu khách vãng lai (xem [21-anonymous-journey.md Journey 3](21-anonymous-journey.md#journey-3--gửi-khiếu-nại-không-cần-tài-khoản)). Nếu tạo RefundRequest → nối sang **Yêu cầu hoàn tiền (Refund Requests)**.
- **Trạng thái đặc biệt cần thiết kế:** Khiếu nại từ khách vãng lai (`ComplainantUserId=null`) → hiện `ContactPhone` nổi bật thay cho tên tài khoản (không có `ComplainantName`).

---

### X. Admin — Người dùng

#### Quản lý Người dùng
- **Actor truy cập:** Admin.
- **Mục đích:** Tìm kiếm, xem chi tiết, khoá/mở tài khoản người dùng bất kỳ.
- **Loại view:** list + detail + action.
- **Dữ liệu hiển thị:** `GET /admin/users?searchText=&role=&isActive=` → `PaginatedResult<UserAdminDto>` (chưa xác minh field cụ thể). Chi tiết: `GET /admin/users/{id}`. Ảnh KYC: `GET /admin/users/{id}/citizen-card/{side}`.
- **Hành động khả dụng trên view:** Khoá: `POST /admin/users/{id}/deactivate`. Mở lại: `POST /admin/users/{id}/reactivate`.
- **Điều kiện hiển thị có điều kiện:** Nút "Khoá"/"Mở lại" hiện đúng 1 trong 2 tuỳ `IsActive` hiện tại, không hiện cả 2 cùng lúc. Ảnh KYC nằm ngoài `wwwroot`, không đoán URL truy cập trực tiếp được — chỉ load qua đúng endpoint này.
- **Điều hướng đến (From):** Menu Admin.
- **Điều hướng đi (To):** Không điều hướng tiếp — có thể link sang **Ra quyết định Xử phạt Venue** nếu user đó là Owner vi phạm.
- **Trạng thái đặc biệt cần thiết kế:** Empty state khi tìm kiếm không ra kết quả.

---

### Y. Admin — Taxonomy & Subscription

#### Quản lý Taxonomy nền tảng
- **Actor truy cập:** Admin.
- **Mục đích:** Quản lý danh mục dùng chung toàn nền tảng (category/genre/mood/atmosphere).
- **Loại view:** list + form (CRUD đơn giản, 4 tab theo loại).
- **Dữ liệu hiển thị:** Không có endpoint GET liệt kê riêng cho Admin trong docs/16 (chỉ thấy POST tạo) — khả năng dùng chung `GET /lounge-shows/filter-options` để hiển thị danh sách hiện có, cần xác minh lại trước khi code.
- **Hành động khả dụng trên view:** `POST /admin/categories`, `/admin/genres`, `/admin/moods`, `/admin/atmospheres` (mỗi loại field riêng, chưa xác minh cụ thể — DTO ghi "chưa xác minh field" ở docs/16).
- **Điều kiện hiển thị có điều kiện:** Trước khi Admin làm bước này lần đầu, mọi nơi khác dùng taxonomy (vd Tạo/Sửa Show chọn Genre) sẽ **luôn thấy danh sách rỗng** — không phải lỗi, cần banner cảnh báo rõ cho Admin mới. **Xác nhận lại (đọc trực tiếp `AdminController.cs:227-257`)**: chỉ có `[HttpPost]` cho cả 4 route (`categories`/`genres`/`moods`/`atmospheres`) — **không có** `PUT`/`DELETE`, cũng không có `GET` liệt kê riêng cho Admin.
- **Điều hướng đến (From):** Menu Admin.
- **Điều hướng đi (To):** Tạo xong → Owner thấy ngay ở các form liên quan (Tạo/Sửa Show, Cài đặt sở thích AI).
- **Trạng thái đặc biệt cần thiết kế:** UI **không được** thiết kế nút "Sửa"/"Xoá" cho taxonomy — không có endpoint hậu thuẫn (chỉ tạo mới, tạo sai phải sống chung hoặc sửa tay DB). Nếu team cần sửa/xoá thật, đây là 1 gap BE cần bổ sung trước, không phải việc UI tự chế.

#### Quản lý Gói Subscription
- **Actor truy cập:** Admin.
- **Mục đích:** Tạo/sửa các gói subscription bán cho Owner.
- **Loại view:** list + form.
- **Dữ liệu hiển thị:** `GET /subscriptions/packages` (cùng nguồn dữ liệu với **Bảng giá gói Subscription** công khai, nhưng ở đây Admin thấy cả gói `IsActive=false`).
- **Hành động khả dụng trên view:** Tạo: `POST /subscriptions/packages` (Name, Description, Price, BillingCycle, MaxTicketsPerEvent, HasAiPoster, MaxAiPostersPerMonth, MaxTourScenes). Sửa: `PUT /subscriptions/packages/{id}` (như trên + IsActive).
- **Điều kiện hiển thị có điều kiện:** Sửa gói đang bán **không ảnh hưởng ngược** Owner đã mua gói cũ — họ giữ nguyên snapshot quyền lợi tại thời điểm mua. UI nên cảnh báo rõ điều này khi Admin sửa (tránh hiểu nhầm "sửa xong mọi Owner đổi quyền lợi ngay").
- **Điều hướng đến (From):** Menu Admin.
- **Điều hướng đi (To):** Tạo/sửa xong → Owner thấy ngay ở **Bảng giá gói Subscription**.
- **Trạng thái đặc biệt cần thiết kế:** Tắt `IsActive` một gói đang có Owner dùng — **không huỷ ngang** subscription đang Active của họ, chỉ chặn Owner mới chọn gói đó.

---

### Z. Admin — Giám sát

#### Thống kê toàn nền tảng
- **Actor truy cập:** Admin.
- **Mục đích:** Xem thống kê tổng hợp toàn hệ thống (khác `Thống kê Venue` của riêng 1 Owner).
- **Loại view:** dashboard.
- **Dữ liệu hiển thị:** `GET /analytics/platform` → `PlatformAnalyticsDto` (chưa xác minh field cụ thể).
- **Hành động khả dụng trên view:** Chỉ xem.
- **Điều kiện hiển thị có điều kiện:** Endpoint riêng biệt hoàn toàn với `GET /analytics/my-lounge` (không phải cùng 1 endpoint check quyền bên trong) — không có cách nào Owner truy cập nhầm vào view này qua API.
- **Điều hướng đến (From):** Menu Admin.
- **Điều hướng đi (To):** Không điều hướng tiếp.
- **Trạng thái đặc biệt cần thiết kế:** Không có gì đặc biệt ngoài loading thông thường.

#### Bảng điều khiển Job nền
- **Actor truy cập:** Admin.
- **Mục đích:** Ép chạy ngay 1 job nền để kiểm tra/khắc phục thủ công.
- **Loại view:** list + action.
- **Dữ liệu hiển thị:** Danh sách job đã biết theo tên (không có endpoint liệt kê job — Admin chọn từ danh sách cứng đã biết trước: `AdminRoleDriftDetectionJob`, `LoginSpikeDetectionJob`, `PushFailureAlertJob`, `VnPayReconciliationJob`, `LedgerIntegrityCheckJob`, `SettlementReleaseJob`, `AutoApproveOverdueAppealsJob`, `ReleaseExpiredHoldsJob`, `CancelAbandonedPaymentsJob`, `ExpireStuckDonationsJob`, `ApplyDuePenaltiesJob`, `PruneStaleDeviceTokensJob`, `RecomputeUserEventScoresJob`...).
- **Hành động khả dụng trên view:** `POST /admin/jobs/{jobId}/trigger` (jobId) → 204.
- **Điều kiện hiển thị có điều kiện:** Chỉ chạy ngay **1 lần**, không đổi lịch Cron định kỳ — UI cần ghi rõ điều này để Admin không hiểu nhầm đây là nơi cấu hình lịch chạy.
- **Điều hướng đến (From):** Menu Admin (khu vực công cụ vận hành).
- **Điều hướng đi (To):** Không điều hướng tiếp — kết quả chạy job thể hiện gián tiếp qua các view khác (vd chạy `LedgerIntegrityCheckJob` → xem lại **Kiểm tra Toàn vẹn Sổ cái**).
- **Trạng thái đặc biệt cần thiết kế:** Không có phản hồi đồng bộ về kết quả job (job chạy nền, có thể mất thời gian) — cần loading/toast "đã yêu cầu chạy" chứ không phải "đã chạy xong thành công".

---

### AA. Owner — Quản lý Menu F&B

> Bổ sung sau lượt đọc đầu (73 view) — journey [18 J9 bước 1-2](18-owner-journey.md#journey-9--vận-hành-fb) (tạo menu, thêm món) ban đầu bị bỏ sót khi dựng catalog, chỉ có 2 phía "khách đặt" (§G) và "Staff xử lý đơn" (§S), thiếu phía Owner **tạo nội dung** menu.

#### Quản lý Menu F&B
- **Actor truy cập:** Owner (đúng chủ venue).
- **Mục đích:** Tạo/sửa cấu trúc menu và món ăn/thức uống của venue.
- **Loại view:** list + form.
- **Dữ liệu hiển thị:** `GET /fnb-menus?loungeId=` → `FnbMenuDto[]` (chưa xác minh field cụ thể). Món trong 1 menu: `GET /fnb-menu-items?menuId=` → `FnbMenuItemDto[]`.
- **Hành động khả dụng trên view:** Tạo menu: `POST /fnb-menus` (LoungeId, Name, Description, DisplayOrder, IsActive). Sửa: `PUT /fnb-menus/{id}`. Xoá: `DELETE /fnb-menus/{id}`. Thêm món: `POST /fnb-menu-items` (MenuId, Category, Name, Description, Price, ImageUrl, DisplayOrder). Sửa món: `PUT /fnb-menu-items/{id}` (như trên + IsAvailable). Xoá món: `DELETE /fnb-menu-items/{id}`.
- **Điều kiện hiển thị có điều kiện:** Toggle `IsActive` (menu) / `IsAvailable` (món) là cách **ẩn tạm** khỏi **Đặt món F&B** (view công khai) mà không cần xoá hẳn — UI nên làm nút gạt riêng biệt với nút "Xoá" (2 hành động khác hệ quả: ẩn tạm vs xoá vĩnh viễn).
- **Điều hướng đến (From):** Danh sách Venue của tôi / Tạo-Sửa Venue (link "Quản lý Menu F&B").
- **Điều hướng đi (To):** Không điều hướng tiếp — venue có menu ngay lập tức xuất hiện ở **Đặt món F&B** phía khách.
- **Trạng thái đặc biệt cần thiết kế:** Empty state "Venue chưa có menu nào" — khác `DELETE`-toàn-bộ (không có endpoint xoá hàng loạt, phải xoá từng món/menu).


## 3. Sơ đồ luồng điều hướng theo từng role

> **Ký hiệu**: `[Tên View]` = đúng tên view ở §2. `→ (hành động)` = actor bấm/submit gì. `↳` = 1 nhánh rẽ (dùng khi nhánh có chuỗi bước riêng dài); `/` = 2 kết quả cùng 1 điểm rẽ, viết trên cùng dòng khi ngắn gọn được. `(thụ động)` = actor không thao tác, chỉ nhận kết quả từ hành động actor khác/hệ thống. Dựng lại 1:1 từ các bảng journey đã có ở docs/17-21, không thêm bước mới.

### 1.1 Audience

**J1. Đăng ký & thiết lập tài khoản**
```
[Đăng ký tài khoản] → (nhập OTP) → [Xác thực Email OTP]
  ↳ (mã đúng) → [Danh sách/Tìm kiếm Show] (đã đăng nhập)
  ↳ (mã sai/hết hạn) → [Xác thực Email OTP] (gửi lại)
[Đăng nhập] → (quên mật khẩu) → [Quên/Đặt lại mật khẩu] → [Đăng nhập]
[Danh sách/Tìm kiếm Show] → (tự chọn) → [Xác thực định danh (KYC CCCD)] / [Cài đặt sở thích gợi ý AI]
```

**J2. Khám phá & theo dõi show/venue**
```
[Danh sách/Tìm kiếm Show] → (chọn 1 show) → [Chi tiết Show]
[Chi tiết Show] → (xem venue tổ chức) → [Chi tiết Venue] → (xem tour) → [Tour ảo 360°]
[Chi tiết Show] → (bấm gợi ý) → [Gợi ý dành cho bạn] → (chọn item) → [Chi tiết Show]
[Chi tiết Venue] → (follow) → ở lại [Chi tiết Venue] (nút đổi trạng thái)
[Chi tiết Show] → (wishlist) → ở lại [Chi tiết Show]
[Danh sách đã theo dõi/yêu thích] ← (menu tài khoản, độc lập)
```

**J3. Mua vé xem show**
```
[Chi tiết Show] → (bấm "Mua vé") → [Chọn vé & Giữ chỗ]
[Chọn vé & Giữ chỗ] → (giữ chỗ 15 phút)
  ↳ (đổi ý, huỷ) → [Chọn vé & Giữ chỗ] (reset, nhả chỗ)
  ↳ (tiếp tục mua) → VNPay (ngoài hệ thống) → [Kết quả thanh toán]
      → (thành công) → [Chi tiết vé]
      → (thất bại) → [Chọn vé & Giữ chỗ] (giữ chỗ mới nếu còn)
```

**J4. Xem livestream & donate**
```
[Chi tiết Show] → (Ongoing + có vé, bấm "Vào xem live") → [Phòng xem Livestream]
[Phòng xem Livestream] → (chọn nghệ sĩ, bấm donate) → [Form Donate]
[Form Donate] → (submit) → VNPay → [Kết quả thanh toán]
  → (thành công) → [Phòng xem Livestream] (quay lại xem, ticker tự cập nhật) / [Lịch sử Donate của tôi]
  → (thất bại) → [Form Donate] (thử lại)
[Lịch sử Donate của tôi] ← (thụ động, thông báo DonationConfirmed)
```

**J5. Đặt F&B tại venue**
```
[Chi tiết Venue] / [Chi tiết Show] → (xem menu) → [Đặt món F&B]
[Đặt món F&B] → (đặt xong) → [Đơn F&B của tôi]
[Đơn F&B của tôi] ← (thụ động, Staff cập nhật trạng thái — không push riêng, phải tự vào xem lại)
```

**J6. Quản lý vé đã mua**
```
[Vé của tôi] → (chọn 1 vé) → [Chi tiết vé]
[Chi tiết vé]
  ↳ (chuyển nhượng) → (thụ động, chờ người nhận `transfer/accept`) → ở lại [Chi tiết vé] (đổi chủ)
  ↳ (huỷ yêu cầu chuyển nhượng đang chờ) → ở lại [Chi tiết vé]
  ↳ (huỷ vé) → [Yêu cầu hoàn tiền của tôi]
[Yêu cầu hoàn tiền của tôi] ← (thụ động, Admin duyệt/từ chối)
```

**J7. Đánh giá show sau khi kết thúc**
```
[Chi tiết Show] (Ended, có vé, trong 7 ngày, chưa đánh giá) → (bấm "Đánh giá") → [Đánh giá Show]
[Đánh giá Show] → (submit) → [Chi tiết Show] (nút ẩn đi, không đánh giá lại được)
```

**J8. Gửi khiếu nại**
```
[Chi tiết vé] / [Chi tiết Show] → (báo cáo vấn đề) → [Gửi khiếu nại]
[Gửi khiếu nại] → (submit, đã đăng nhập) → [Khiếu nại của tôi]
[Khiếu nại của tôi] ← (thụ động, thông báo ComplaintUpdate) — nếu ResolvedAction liên quan hoàn tiền vé → [Yêu cầu hoàn tiền của tôi]
```

**J9. Thông báo & hồ sơ cá nhân** *(chạy song song mọi journey khác, không tuyến tính)*
```
[Hộp thư thông báo] ← (icon chuông, mọi màn hình đã đăng nhập)
[Hồ sơ cá nhân] → [Xác thực định danh (KYC CCCD)] / [Cài đặt sở thích gợi ý AI] / [Quyền riêng tư & Dữ liệu cá nhân]
[Quyền riêng tư & Dữ liệu cá nhân] → (khoá tạm / xoá vĩnh viễn) → [Đăng nhập] (session bị huỷ ngay)
```

---

### 1.2 Owner

**J1. Đăng ký & thiết lập tài khoản Owner**
```
[Đăng ký tài khoản] (Role=Owner) → [Xác thực Email OTP] → (thành công) → [Danh sách Venue của tôi]
[Danh sách Venue của tôi] → (bắt buộc trước J2) → [Quản lý Tài khoản ngân hàng] → (thụ động, chờ Admin xác minh)
```

**J2. Tạo & đưa venue vào hoạt động**
```
[Danh sách Venue của tôi] → (tạo mới) → [Tạo/Sửa Venue] → (submit, venue Pending) → (thụ động, chờ Admin)
  ↳ (Approved) → [Tạo/Sửa Venue] (bổ sung ảnh/giấy phép) → [Sơ đồ chỗ ngồi (Zone Editor)] → [Quản lý Nhân viên (Staff)]
      → (tự chọn) → [Tour ảo 360° (quản lý)] / [Nội dung bổ sung Venue (Gallery & Tiêu chí gợi ý)] / [Quản lý Menu F&B]
  ↳ (Rejected) → [Tạo/Sửa Venue] (tạo venue MỚI — không có "nộp lại đúng venue đó")
```

**J3. Đăng ký gói Subscription**
```
[Bảng giá gói Subscription] → (chọn gói) → [Gói Subscription của tôi] → (khởi tạo thanh toán) → VNPay → [Kết quả thanh toán]
  → (thành công) → [Gói Subscription của tôi] (Active, mở khoá J4)
  → (thất bại) → [Gói Subscription của tôi] (thử lại)
[Gói Subscription của tôi] → (sắp hết hạn) → (gia hạn) → [Kết quả thanh toán] → [Gói Subscription của tôi]
```

**J4. Tạo show mới & mở bán vé**
```
[Danh sách Show của tôi] → (tạo mới, cần Subscription Active) → [Tạo/Sửa Show] → (submit, Draft) → [Bảng điều khiển Show (Owner)]
[Bảng điều khiển Show (Owner)] → [Quản lý Hạng vé (Ticket Tier)] (≥1 bắt buộc)
[Bảng điều khiển Show (Owner)] → [Poster Show] (AI hoặc upload thủ công)
[Bảng điều khiển Show (Owner)] → [Khai báo pháp lý & Tác quyền]
[Bảng điều khiển Show (Owner)] → (nộp duyệt, cần venue Approved + ≥1 hạng vé) → (thụ động, chờ Admin)
  ↳ (Approved) → show công khai ngay → nối [Chi tiết Show] (phía Audience, xem 1.1 J2/J3)
  ↳ (Rejected) → [Tạo/Sửa Show] (sửa lại, về Draft)
```

**J5-A. Vận hành show Offline thuần**
```
[Bảng điều khiển Show (Owner)] → (bắt đầu) → ở lại (Ongoing) → (kết thúc) → ở lại (Ended, mở cửa sổ đánh giá 7 ngày)
```

**J5-B. Vận hành show Online (Livestream)**
```
[Bảng điều khiển Show (Owner)] → (tạo Livestream) → [Vận hành Livestream] → (thụ động, chờ Admin duyệt livestream — độc lập duyệt Show)
[Vận hành Livestream] → (lấy credentials, cắm OBS) → (bắt đầu phát — chặn nếu chưa duyệt HOẶC chưa khai VCPMC)
  → (thành công) → Ongoing → nối [Phòng xem Livestream] (phía Audience, xem 1.1 J4)
[Vận hành Livestream] → (kết thúc phát) → [Bảng điều khiển Show (Owner)] (Ended, mở cửa sổ đánh giá)
[Vận hành Livestream] ← (ngoại lệ, thụ động — Admin terminate) → (Terminated, ngắt kết nối mọi khán giả)
```

**J6. Điều chỉnh show đã publish**
```
[Bảng điều khiển Show (Owner)]
  ↳ (đổi lịch, chỉ khi Published) → ở lại (áp lại quy tắc 7-ngày-làm-việc)
  ↳ (đổi Offline→Online, 1 chiều) → ở lại (hoàn 100% vé vật lý Confirmed tự động)
  ↳ (đổi playback 2D/3D) → ở lại
  ↳ (huỷ show, chặn nếu Livestream đang Live) → ở lại (Cancelled, hoàn 100% mọi vé Confirmed)
```

**J7. Nhận & xử lý donate cho nghệ sĩ**
```
(thụ động, Audience donate thành công) → [Donate chờ xác nhận]
[Donate chờ xác nhận] → (acknowledge trong 24h) → [Donate chờ trả nghệ sĩ]
  ↳ (không thao tác kịp 24h) → (thụ động) → [Donate chờ trả nghệ sĩ] (hệ thống tự AutoConfirmed)
[Donate chờ trả nghệ sĩ] → (chuyển khoản thủ công ngoài hệ thống, rồi confirm-paid) → hoàn tất chu trình
  ↳ (chặn nếu Performer chưa có bank account) → [Quản lý Tài khoản ngân hàng] (đăng ký hộ trước)
```

**J8. Theo dõi doanh thu & tài chính** *(phần lớn thụ động)*
```
(thụ động, vé đầu tiên bán) → (thụ động, tới hạn giải ngân) → [Thu nhập tổng quan]
  ↳ (chưa có bank account) → chặn tạo lịch → [Quản lý Tài khoản ngân hàng]
  ↳ (tranche cuối, show kết thúc sớm bất thường) → (thụ động, chờ Admin xét PendingReview)
[Thu nhập tổng quan] → (xem chi tiết 1 venue) → [Thống kê Venue]
```

**J9. Vận hành F&B**
```
[Quản lý Menu F&B] → (tạo menu, thêm món) → menu xuất hiện ngay ở [Đặt món F&B] (phía Audience)
[Quản lý đơn F&B tại quầy] → (đặt hộ khách tại quầy) / (cập nhật trạng thái Pending→Preparing→Served→Paid)
```

**J10. Đối phó xử phạt & kháng cáo**
```
(thụ động, nhận quyết định xử phạt) → [Xử phạt & Kháng cáo của tôi]
[Xử phạt & Kháng cáo của tôi] → (gửi kháng cáo, chỉ khi Active) → (thụ động, chờ Admin hoặc auto-overturn quá SLA)
  ↳ (Overturned) → ở lại (lưu ý: không tự mở lại nếu còn phạt Active khác chồng lên)
  ↳ (Upheld) → ở lại
```

---

### 1.3 Staff

**J1. Được gán vào venue & đăng nhập lần đầu**
```
(thụ động, Owner gán qua [Quản lý Nhân viên (Staff)]) → [Đăng nhập] → (thành công, nhận claim lounge_id) → Trang chủ Staff
  ↳ (ngoại lệ, thụ động — Owner gỡ khỏi venue) → mất quyền vận hành từ lần gọi API kế tiếp
```

**J2. Bán vé tại quầy (Walk-in)**
```
[Bán vé tại quầy (Walk-in)] → (chọn hạng vé Physical + số lượng, bán ngay) → ở lại (QR sinh tại chỗ, đưa khách trực tiếp)
```

**J3. Check-in khán giả tại cửa**
```
[Check-in vé] → (quét xem trước, tách riêng) → (xác nhận check-in thật) → ở lại (reset, sẵn sàng quét tiếp)
```

**J4. Vận hành livestream** *(dùng chung view với Owner — xem 1.2 J5-B)*
```
[Vận hành Livestream] → (tạo/lấy credentials/bắt đầu/kết thúc — cùng quyền RequireVenueOperator như Owner)
```

**J5. Vận hành show Offline (bắt đầu/kết thúc)**
```
[Bảng điều khiển Show (Owner)] → (Staff chỉ dùng được nút bắt đầu/kết thúc — không sửa/huỷ/đổi lịch được, các nút đó RequireOwner)
  ↳ (show có Livestream đi kèm) → nút start/end ở đây tự ẩn, dùng [Vận hành Livestream] thay thế
```

**J6. Xử lý đơn F&B**
```
[Quản lý đơn F&B tại quầy] → (đặt hộ khách tại quầy) / (cập nhật trạng thái tuần tự, hoặc Cancelled bất kỳ lúc nào trước Paid)
```

---

### 1.4 Admin

> Không có journey "đăng ký" — Admin luôn bắt đầu thẳng từ [Đăng nhập] (tài khoản tạo sẵn qua sửa DB trực tiếp).

**J1. Duyệt venue mới**
```
[Duyệt Venue mới]
  ↳ (duyệt) → Owner mở khoá J4 (Owner) ngay lập tức
  ↳ (từ chối, kèm Reason) → Owner phải tạo venue mới (không "nộp lại")
```

**J2. Kiểm duyệt show & livestream**
```
[Kiểm duyệt Show & Livestream]
  ↳ (duyệt show) → show công khai ngay cho Audience
  ↳ (từ chối show, cần ReviewNote) → show về Draft cho Owner sửa
  ↳ (duyệt livestream) → mở khoá nút "Bắt đầu phát" cho Owner/Staff
  ↳ (từ chối livestream, cần ReviewNote) → Owner/Staff không phát được
```

**J3. Xử phạt venue & xử lý kháng cáo**
```
[Ra quyết định Xử phạt Venue] → (thụ động, Owner kháng cáo) → [Xử lý Kháng cáo]
[Xử lý Kháng cáo] (chỉ xử lý được trạng thái Appealed)
  ↳ (Overturned) → Owner nhận kết quả (không tự mở lại venue nếu còn phạt Active khác)
  ↳ (Upheld) → Owner nhận kết quả
  ↳ (không xử lý kịp SLA, thụ động) → (hệ thống tự Overturn giùm, cùng khoá tránh chồng quyết định)
```

**J4. Xử lý hoàn tiền (vé) & hoàn donate**
```
[Yêu cầu hoàn tiền (Refund Requests)]
  ↳ (từ chối) → không đụng tiền
  ↳ (duyệt, toàn phần/một phần) → đảo bút toán, co settlement chưa Released
[Yêu cầu hoàn tiền (Refund Requests)] → (escape-hatch, tự tạo thủ công) → ở lại (vẫn qua bước duyệt bình thường)
[Hoàn Donate] → (chỉ hợp lệ trước PerformerPaid) → đảo bút toán chặng 1, không gọi VNPay
```

**J5. Xác minh bank account**
```
(thụ động, Owner đăng ký ở [Quản lý Tài khoản ngân hàng] phía họ) → [Xác minh Tài khoản ngân hàng]
[Xác minh Tài khoản ngân hàng] → (xác minh) → settlement từng bị chặn ở Owner tự động retry ngay
```

**J6. Xử lý khiếu nại**
```
[Xử lý Khiếu nại (Admin)] → (quyết định, theo ResolvedAction)
  ↳ (Refund/Compensate, target=ticket) → tự tạo RefundRequest → nối [Yêu cầu hoàn tiền (Refund Requests)]
  ↳ (Refund/Compensate, target=donation) → không tự động, Admin tự vào [Hoàn Donate]
  ↳ (TakeDownContent, target=show) → gỡ show + hoàn 100% mọi vé (logic riêng, không dùng chung path Huỷ show của Owner)
  ↳ (action khác) → chỉ ghi nhận, không tự động hoá gì thêm
```

**J7. Quản lý người dùng**
```
[Quản lý Người dùng] → (tìm kiếm → chọn 1 user → xem KYC nếu cần)
  ↳ (khoá tài khoản) → ở lại (IsActive=false)
  ↳ (mở lại) → ở lại (IsActive=true)
```

**J8. Quản lý taxonomy & gói subscription**
```
[Quản lý Taxonomy nền tảng] → (thêm category/genre/mood/atmosphere — không sửa/xoá được, xem [23 §Tổng hợp mục 3])
[Quản lý Gói Subscription] → (tạo/sửa gói) → Owner thấy ngay ở [Bảng giá gói Subscription] (không ảnh hưởng ngược Owner đã mua)
```

**J9. Buộc dừng livestream vi phạm**
```
[Phòng xem Livestream] (Admin luôn xem được) → (phát hiện vi phạm) → (buộc dừng, kèm Reason)
  → mọi khán giả đang xem bị ngắt kết nối ngay + [Vận hành Livestream] (phía Owner/Staff) đổi trạng thái Terminated
```

**J10. Giám sát vận hành & tài chính** *(phần lớn phản ứng, không chủ động tìm)*
```
(thụ động, nhận cảnh báo từ 4 job nền) → [Hộp thư thông báo]
[Hộp thư thông báo] → [Kiểm tra Toàn vẹn Sổ cái] / [Thống kê toàn nền tảng] / [Bảng điều khiển Job nền]
[Bảng điều khiển Job nền] → (ép chạy 1 job) → kết quả phản ánh gián tiếp qua view liên quan (vd chạy job đối soát sổ cái → xem lại [Kiểm tra Toàn vẹn Sổ cái])
```

---

### 1.5 Khách chưa đăng nhập (Anonymous)

**J1. Duyệt catalog công khai**
```
[Danh sách/Tìm kiếm Show] → [Chi tiết Show] → [Chi tiết Venue] → [Tour ảo 360°]
[Chi tiết Show] → [Trang cá nhân Nghệ sĩ] (bấm vào nghệ sĩ trong lineup)
```

**J2. Xem minh bạch donate công khai** *(2 cơ chế độc lập, không nhầm lẫn)*
```
[Sổ minh bạch Donate công khai] — trang tra cứu, tải theo trang, chỉ donate đã "chốt"
[Ticker Donate công khai] — nhúng trong [Chi tiết Show]/[Phòng xem Livestream], realtime, không cần vé xem live
[Trang cá nhân Nghệ sĩ] → (xem lịch sử donate riêng nghệ sĩ đó) → [Sổ minh bạch Donate công khai] (biến thể theo nghệ sĩ)
```

**J3. Gửi khiếu nại không cần tài khoản**
```
[Gửi khiếu nại] → (submit, chưa đăng nhập, ContactPhone bắt buộc) → [Tra cứu khiếu nại khách vãng lai] (lưu lại id hiện trên màn hình)
[Tra cứu khiếu nại khách vãng lai] → (nhập id + phone, có thể nhiều lần sau) → xem lại kết quả
(thụ động, song song) → Admin resolve → SMS chủ động tới ContactPhone (kênh ngoài UI, không phải 1 view)
```

**J4. Bản đồ điểm chặn 🚧** *(nơi journey Anonymous kết thúc, phải sang Audience)*
```
[Chọn vé & Giữ chỗ] / [Form Donate] / [Đặt món F&B] / [Danh sách đã theo dõi/yêu thích] / [Đánh giá Show]
  → 🚧 (RequireAuthenticated) → [Đăng nhập] / [Đăng ký tài khoản] → quay lại đúng view đang định vào (FE tự giữ context, BE không có "return URL")
[Phòng xem Livestream] → 🚧 (RequireAuthenticated **và** isGenuineTicketHolder — chặt hơn cả đăng nhập suông) → không thấy gì kể cả sau khi đăng nhập nếu chưa có vé
```

---

## 4. Sơ đồ các điểm giao thoa/tương tác real-time giữa các role

> Định dạng mỗi điểm: **Actor A @ [View]** → *(hành động)* → **BE**: endpoint/service xử lý → **Actor B @ [View]**: thay đổi thấy được. Gộp + khử trùng lặp từ 4 bảng "Tổng hợp điểm giao thoa real-time" ở cuối mỗi doc journey 17/18/19/20 — không thêm điểm giao thoa mới ngoài những gì đã ghi nhận ở đó.

**1. Giữ chỗ làm tồn kho tụt ≤10%**
Audience A @ **[Chọn vé & Giữ chỗ]** → *(giữ chỗ thành công)* → BE: `POST /tickets/holds`, kiểm quota nội bộ phát hiện ngưỡng ≤10% → publish `NotificationType.WishlistLowStock` → Audience B (đã wishlist show đó) @ **[Hộp thư thông báo]**: nhận cảnh báo "sắp hết vé" gần như ngay lập tức, dù không thao tác gì.

**2. Thanh toán vé thành công**
Audience @ **[Kết quả thanh toán]** (vé) → *(VNPay IPN xác nhận)* → BE: `GET /payments/vnpay/ipn` → publish `TicketPaymentConfirmed`, tạo lịch `Settlement` → Owner @ **[Thu nhập tổng quan]**: dòng settlement mới xuất hiện (không đẩy realtime tức thì, Owner cần tự vào xem/refresh).

**3. Chat trong lúc xem livestream**
Audience A @ **[Phòng xem Livestream]** → *(gửi tin nhắn)* → BE: SignalR `LivestreamHub` broadcast trong group `loungeShowId` → mọi Audience khác (B, C, D...) @ **[Phòng xem Livestream]** (cùng show): thấy tin nhắn mới ngay lập tức.

**4. Donate thành công (3 bên nhận cùng lúc)**
Audience @ **[Form Donate]** → *(VNPay IPN xác nhận)* → BE: `GET /donations/vnpay-ipn` → `ProcessDonationPaymentCommandHandler` gửi đồng thời 3 hướng:
  - chính Audience đó @ **[Lịch sử Donate của tôi]** / **[Hộp thư thông báo]**: `DonationConfirmed` (riêng tư)
  - Owner @ **[Donate chờ xác nhận]** / **[Hộp thư thông báo]**: `DonationReceived` (chỉ số tiền, ẩn tên nếu donor chọn ẩn danh)
  - mọi người đang xem show đó @ **[Ticker Donate công khai]** (kể cả Anonymous, kể cả không có vé): alert realtime qua `PublicDonationHub`, lọc theo 3 cờ riêng tư donor đã chọn.

**5. Owner acknowledge / confirm-paid 1 donate**
Owner @ **[Donate chờ xác nhận]** hoặc **[Donate chờ trả nghệ sĩ]** → *(acknowledge / confirm-paid)* → BE: `POST /donations/{id}/acknowledge` hoặc `.../confirm-paid` → `PublicDonationBroadcast.PublishAsync` → mọi người xem show đó @ **[Ticker Donate công khai]**: thêm 1 alert cập nhật tiến độ (chặng 2/3, 3/3) — cùng donate, KHÔNG phải donate mới.

**6. Kết nối vào xem livestream**
Audience @ **[Phòng xem Livestream]** → *(kết nối SignalR thành công)* → BE: `LivestreamHub.OnConnectedAsync`, tăng `Livestream.ViewerCount` (đếm nguyên tử tại DB) → Owner/Staff @ **[Vận hành Livestream]**: số viewer thay đổi (đọc lại qua polling/refresh, không tự đẩy).

**7. Owner/Staff bắt đầu phát livestream**
Owner/Staff @ **[Vận hành Livestream]** → *(bắt đầu phát)* → BE: `POST /livestreams/{id}/start` → publish `EventLive` → mọi người đã mua vé + mọi người follow venue đó @ **[Hộp thư thông báo]**: nhận thông báo "Đang phát trực tiếp", dẫn thẳng sang **[Phòng xem Livestream]**.

**8. Owner đổi lịch / đổi format / huỷ show**
Owner @ **[Bảng điều khiển Show (Owner)]** → *(reschedule / format / cancel)* → BE: `POST .../reschedule`, `PUT .../format`, `POST .../cancel` → publish `EventRescheduled`/`EventCancelled`, tự tạo `RefundRequest` nếu áp dụng → mọi Audience đã mua vé show đó @ **[Hộp thư thông báo]** → **[Chi tiết vé]**: trạng thái vé đổi, hoặc → **[Yêu cầu hoàn tiền của tôi]**: có yêu cầu hoàn tiền mới tự sinh.

**9. Owner gán Staff mới**
Owner @ **[Quản lý Nhân viên (Staff)]** → *(gán)* → BE: `POST /lounges/{id}/staff`, đổi `User.Role → Staff` → chính người được gán @ **[Đăng nhập]** (lần kế tiếp): nhận claim `lounge_id`, có quyền vận hành ngay — không cần Owner thông báo thủ công.

**10. Owner/Staff cập nhật trạng thái đơn F&B**
Owner/Staff @ **[Quản lý đơn F&B tại quầy]** → *(đổi trạng thái)* → BE: `PUT /fnb-orders/{id}/status` → Audience đã đặt đơn đó @ **[Đơn F&B của tôi]**: trạng thái đổi — **không có push riêng**, Audience phải tự vào lại/refresh để thấy (khác mọi điểm giao thoa khác đều có kênh chủ động).

**11. Staff bán vé walk-in**
Staff @ **[Bán vé tại quầy (Walk-in)]** → *(bán)* → BE: `POST /tickets/walk-in`, publish `TicketPaymentConfirmed` → Owner @ **[Thu nhập tổng quan]**: thêm dòng settlement (thường không tính hoa hồng, khác vé online).

**12. Staff check-in vé**
Staff @ **[Check-in vé]** → *(quét, xác nhận)* → BE: `POST /tickets/check-in`, `Ticket.Status → Used` → chính khán giả đó @ **[Chi tiết vé]**: QR không dùng lại được, mất khả năng chuyển nhượng/huỷ (điều kiện hiển thị nút đổi ngay).

**13. Admin duyệt/từ chối venue**
Admin @ **[Duyệt Venue mới]** → *(approve/reject)* → BE: `POST /admin/lounges/{id}/approve|reject` → Owner @ **[Danh sách Venue của tôi]**: trạng thái đổi Pending→Approved/Rejected; nếu Approved, nút "Tạo show" ở **[Danh sách Show của tôi]** mở khoá ngay.

**14. Admin duyệt/từ chối show hoặc livestream**
Admin @ **[Kiểm duyệt Show & Livestream]** → *(review)* → BE: `POST /moderations/shows/{id}/review` hoặc `.../livestreams/{id}/review` → Owner @ **[Bảng điều khiển Show (Owner)]** / **[Vận hành Livestream]**: trạng thái đổi ngay; nếu duyệt show → đồng thời mọi Audience/Anonymous @ **[Danh sách/Tìm kiếm Show]**: show xuất hiện công khai ngay lập tức (không có độ trễ cache).

**15. Admin terminate livestream**
Admin @ **[Phòng xem Livestream]** (Admin luôn xem được) → *(terminate, kèm Reason)* → BE: `POST /livestreams/{id}/terminate` → mọi khán giả đang xem @ **[Phòng xem Livestream]**: bị ngắt kết nối SignalR ngay lập tức; đồng thời Owner/Staff @ **[Vận hành Livestream]**: trạng thái đổi `Terminated` (khác `Ended` tự nhiên, có `TerminatedById`+`TerminatedReason`).

**16. Admin duyệt refund request / hoàn donate**
Admin @ **[Yêu cầu hoàn tiền (Refund Requests)]** hoặc **[Hoàn Donate]** → *(process/refund)* → BE: `POST /admin/refund-requests/{id}/process` hoặc `.../donations/{id}/refund` → Audience liên quan @ **[Yêu cầu hoàn tiền của tôi]** hoặc **[Lịch sử Donate của tôi]**: trạng thái đổi + thông báo kết quả.

**17. Admin xử phạt venue**
Admin @ **[Ra quyết định Xử phạt Venue]** → *(tạo phạt)* → BE: `POST /venue-penalties` → Owner @ **[Xử phạt & Kháng cáo của tôi]**: phạt mới xuất hiện, một phần chức năng vận hành có thể bị khoá tuỳ mức (Warning/Suspension/Ban).

**18. Admin xác minh bank account**
Admin @ **[Xác minh Tài khoản ngân hàng]** → *(verify)* → BE: `POST /admin/bank-accounts/{id}/verify` → Owner @ **[Thu nhập tổng quan]**: mọi settlement từng bị chặn trước đó **tự động retry ngay**, không cần Owner thao tác lại.

**19. Admin resolve khiếu nại (TakeDownContent, show)**
Admin @ **[Xử lý Khiếu nại (Admin)]** → *(resolve, TakeDownContent)* → BE: `TakeDownShowAsync` (nội bộ `ResolveComplaintCommandHandler`) → show `Cancelled` + hoàn 100% mọi vé `Confirmed` → mọi Audience đã mua vé show đó @ **[Hộp thư thông báo]** → **[Yêu cầu hoàn tiền của tôi]**: nhận `EventCancelled` + yêu cầu hoàn 100% tự sinh, hàng loạt cùng lúc.

> **Lưu ý về Performer**: Performer không có view/tài khoản riêng (xem [22-performer-presence.md](22-performer-presence.md)) nên không xuất hiện như "Actor B" độc lập trong sơ đồ trên — mọi thay đổi liên quan Performer (nhận donate, được xếp lineup) phản ánh gián tiếp qua view của **Owner** (người quản lý hộ), không phải 1 màn hình riêng của Performer.
> **Lưu ý về khiếu nại khách vãng lai**: Admin resolve khiếu nại của khách vãng lai gửi kết quả qua **SMS** tới `ContactPhone` (xem [21 Journey 3](21-anonymous-journey.md#journey-3--gửi-khiếu-nại-không-cần-tài-khoản)) — đây không phải thay đổi trên 1 "view" trong app (khách vãng lai không có phiên đăng nhập để nhận in-app), nên không liệt kê chung bảng trên dù cũng là 1 dạng giao thoa near-real-time.

---

## 5. Danh sách giả định cần xác nhận lại với đội thiết kế/dev

> 3 loại giả định khác nhau, tách riêng vì cần hỏi **2 nhóm người khác nhau**: 5.1-5.2 là chỗ **BE chưa đủ thông tin để khẳng định chắc chắn** (hỏi đội dev backend); 5.3 là chỗ **BE đã đủ dữ liệu nhưng chưa rõ UX mong muốn** (hỏi đội thiết kế/PM — đây là quyết định sản phẩm, không phải kỹ thuật).

### 5.1 DTO response cần xác minh lại field với BE

33 chỗ trong toàn bộ catalog đánh dấu "(chưa xác minh field cụ thể)" — nghĩa là chỉ thấy tên DTO qua `ProducesResponseType<>`, chưa từng mở file đọc field thật. Gộp theo domain để đội dev tiện đối chiếu 1 lượt thay vì đọc rải rác:

| Domain | DTO cần xác minh field | View liên quan |
|---|---|---|
| Auth & Tài khoản | `RegisterResultDto`, `UserProfileDto`, `MyDataExportDto` | Đăng ký tài khoản, Hồ sơ cá nhân, Quyền riêng tư & Dữ liệu cá nhân |
| Thông báo | `NotificationDto` (khả năng có Type/Title/Body/ReferenceType/ReferenceId/IsRead/CreatedAt, dựa theo tham số `NotifyAsync`) — **đặc biệt cần xác nhận có field điều hướng sẵn theo `ReferenceType`/`ReferenceId` hay không**, quyết định FE tự map `Type`→route hay dùng field có sẵn | Hộp thư thông báo |
| Show/Event | `LoungeShowDetailDto`, `LoungeDetailDto` (venue), `VenueTourDto`, `RecommendedLoungeShowDto`, `PosterGenerationAttemptDto` | Chi tiết Show, Chi tiết Venue, Tour ảo 360°, Gợi ý dành cho bạn, Poster Show |
| Ticket | `TicketTierSummaryDto`, `TicketListItemDto`, `TicketDetailDto`, `RefundRequestDto` | Chọn vé & Giữ chỗ, Vé của tôi, Chi tiết vé, Yêu cầu hoàn tiền của tôi |
| Livestream | `LivestreamDetailDto` (nội dung khác theo actor — xem docs/16 §"Endpoint có response khác nhau theo Actor"), `ChatMessageDto` | Phòng xem Livestream |
| Venue | `LoungeListItemDto`, `SeatingZoneDto`, `CustomCriteriaDto`, `LoungeStaffDto` | Danh sách Venue, Sơ đồ chỗ ngồi, Nội dung bổ sung Venue, Quản lý Nhân viên |
| Payment/Finance | `BankAccountDto` (chắc chắn có `IsVerified`), `SubscriptionPackageDto`, `MySubscriptionDto`, `EarningsSummaryDto`, `WalkInSaleResultDto` | Quản lý Tài khoản ngân hàng, Bảng giá/Gói Subscription, Thu nhập tổng quan, Bán vé tại quầy |
| F&B | `FnbMenuDto`, `FnbMenuItemDto`, `FnbOrderDto` | Đặt món F&B, Đơn F&B của tôi, Quản lý Menu F&B, Quản lý đơn F&B tại quầy |
| Follow/Wishlist | `FollowedLoungeDto` | Danh sách đã theo dõi/yêu thích |
| Admin | `PendingLoungeDto`, `VenuePenaltyDto`, `LedgerIntegrityIssueDto`, `UserAdminDto`, `OwnerAnalyticsDto`, `PlatformAnalyticsDto`, `CreateRefundRequestCommand` (field tạo thủ công) | Duyệt Venue mới, Xử lý Kháng cáo, Kiểm tra Toàn vẹn Sổ cái, Quản lý Người dùng, Thống kê Venue/toàn nền tảng, Yêu cầu hoàn tiền |
| Admin Taxonomy | `CreateEventCategoryCommand`/`CreateMusicGenreCommand`/`CreateMoodCommand`/`CreateVenueAtmosphereCommand` (field tạo) | Quản lý Taxonomy nền tảng |

Riêng 2 trường hợp không chỉ thiếu field mà **thiếu cả field-level xác nhận có khác biệt theo role hay không** (không chỉ "chưa đọc field" mà "chưa biết có 2 bộ field khác nhau"): `LoungeDetailDto` (Chi tiết Venue — Owner có thể thấy thêm field quản trị) và `LoungeShowDetailDto` (Chi tiết Show — tương tự). **Đừng thiết kế 2 layout khác nhau cho 2 role dựa trên suy đoán "Optional Auth chắc là có field riêng" — phải xác nhận field cụ thể trước.**

### 5.2 Endpoint còn thiếu (đã xác nhận qua đọc code, không phải suy đoán)

1. **Xác minh Tài khoản ngân hàng** và **Hoàn Donate** (Admin) — có endpoint hành động (`POST /admin/bank-accounts/{id}/verify`, `POST /admin/donations/{id}/refund`) nhưng **không có endpoint liệt kê hàng chờ**, khác mọi luồng "duyệt" khác trong hệ thống (đều có `GET .../pending` đi kèm). Cần quyết định: bổ sung endpoint liệt kê, hay xác nhận đây là thao tác chủ động hiếm khi cần UI danh sách riêng (Admin biết `id` qua kênh khác).
2. **Xử lý Kháng cáo** (Admin) — cùng dạng thiếu: không có `GET` danh sách "mọi kháng cáo đang mở", chỉ tra được theo `id` đã biết trước.
3. **Quản lý Taxonomy nền tảng** — xác nhận trực tiếp (`AdminController.cs:227-257`): chỉ có `POST` (tạo), **không có** `PUT`/`DELETE`/`GET` liệt kê riêng cho Admin. Tạo sai tên/dữ liệu hiện không có cách sửa qua API.

### 5.3 Quyết định UX cần đội thiết kế xác nhận (BE không quyết định được, đây là lựa chọn sản phẩm)

BE cung cấp đúng dữ liệu/endpoint cho các trường hợp dưới đây, nhưng **cách trình bày** (modal hay điều hướng trang mới, gộp hay tách nhiều trang) là quyết định thiết kế thuần tuý — mọi lựa chọn "modal" ghi trong §2 chỉ là **đề xuất mặc định của lượt phân tích này**, chưa phải quyết định cuối:

1. **Form Donate** — hiện đề xuất là modal nổi trên Phòng xem Livestream (không rời khỏi video). Cần xác nhận: giữ modal (không mất video), hay điều hướng sang trang riêng (rủi ro mất kết nối SignalR/video khi quay lại)?
2. **Đánh giá Show** — đề xuất modal trên Chi tiết Show. Tương tự cần xác nhận modal hay trang riêng, đặc biệt vì có nhiều field (Score + Comment dài) có thể cần không gian riêng trên mobile.
3. **Gửi khiếu nại** — mở từ nhiều context khác nhau (vé/show/venue). Modal-theo-ngữ-cảnh (điền sẵn `TargetType`/`TargetId`) tiện hơn nhưng rủi ro: khách vãng lai cần **thấy rõ `id` trả về sau submit để tự lưu lại** — nếu là modal, đóng modal có làm mất thông tin này không? Đề xuất: nếu modal, kết quả submit phải giữ nguyên trên màn hình (không tự đóng) cho tới khi actor chủ động tắt.
4. **Kết quả thanh toán** — đề xuất 1 template dùng chung cho 3 luồng (vé/donate/subscription). Cần xác nhận: có cần 3 thiết kế khác nhau theo thương hiệu/ngữ cảnh (vd trang kết quả donate có thể muốn hiện lại thông tin nghệ sĩ, trang kết quả vé muốn hiện QR luôn) thay vì 1 khung chung tham số hoá?
5. **"Của tôi" (management) vs danh sách công khai lọc `mine=true`** — `GET /lounge-shows?mine=true` và `GET /lounges?mine=true` dùng **cùng 1 endpoint** với danh sách công khai (Danh sách/Tìm kiếm Show, Danh sách Venue). Catalog này đề xuất Owner có 2 trang riêng (**Danh sách Show của tôi**/**Danh sách Venue của tôi** dạng dashboard quản lý, khác **Danh sách/Tìm kiếm Show**/**Danh sách Venue** dạng duyệt công khai) — cần xác nhận đây có đúng ý đồ sản phẩm hay Owner nên dùng chung 1 trang browse có thêm toggle "Chỉ của tôi"?
6. **Sổ minh bạch Donate công khai** — 2 biến thể (toàn hệ thống có đầy đủ breakdown phí, theo 1 nghệ sĩ chỉ có Gross) hiện đề xuất gộp 1 view mô tả, tham số hoá theo nguồn dữ liệu. Cần xác nhận: xây 1 component linh hoạt ẩn/hiện cột breakdown theo nguồn, hay 2 layout tách biệt hoàn toàn?
7. **Nội dung bổ sung Venue (Gallery & Tiêu chí gợi ý)** — 2 tính năng khác hẳn nhau (thư viện ảnh vs form định nghĩa tiêu chí taxonomy) đang gộp 1 view cho gọn catalog. Cần xác nhận đội thiết kế có muốn tách thành 2 tab/trang riêng trong khu vực cài đặt Venue hay giữ gộp.
8. **Cài đặt sở thích gợi ý AI vs Tiêu chí riêng theo Venue** (`GET/PUT /me/custom-preferences`) — hiện gộp chung 1 trang cài đặt cá nhân. Vì tiêu chí riêng là **của từng Venue** (Owner tự định nghĩa, khác nhau giữa các venue), có hợp lý hơn không nếu đặt UI thiết lập mức độ quan tâm **ngay trên trang Chi tiết Venue** đó (theo ngữ cảnh) thay vì 1 danh sách toàn cục tách rời, nơi actor phải tự nhớ venue nào có tiêu chí gì?
9. **Quên/Đặt lại mật khẩu bước 2** — mở từ link trong email (`Token` truyền qua query string), chắc chắn phải là 1 route/URL độc lập nhận được từ ngoài app (không thể là "tiếp tục phiên form" như bước 1 cùng tab). Cần xác nhận route pattern cụ thể (`/reset-password?token=...`) đã thống nhất với đội FE build chưa, và app có xử lý được deep link đó trên mobile (nếu có bản mobile) hay không.
10. **Bảng điều khiển Show (Owner) — hành động có hệ quả tài chính lớn** — đổi Offline→Online tự động hoàn 100% mọi vé vật lý `Confirmed`; huỷ show tự động hoàn 100% mọi vé. Cả 2 hiện đề xuất là hành động 1-bước trên dashboard kèm cảnh báo text. Cần xác nhận: có cần buộc actor gõ lại tên show hoặc số lượng vé bị ảnh hưởng để xác nhận (như nhiều hệ thống tài chính khác làm với hành động không hoàn tác diện rộng) thay vì chỉ 1 nút "Xác nhận" đơn giản?

---

*Hết. Nguồn đầy đủ theo từng phần: [16-api-endpoint-catalog.md](16-api-endpoint-catalog.md), [17-audience-journey.md](17-audience-journey.md) — [22-performer-presence.md](22-performer-presence.md), [23-view-catalog.md](23-view-catalog.md), [24-view-flow-diagrams.md](24-view-flow-diagrams.md).*
