# 20 — Journey của Admin (Quản trị nền tảng)

← [12-actors-and-authorization.md](12-actors-and-authorization.md) · [14-usecase-traces.md](14-usecase-traces.md) · [16-api-endpoint-catalog.md](16-api-endpoint-catalog.md) · [18-owner-journey.md](18-owner-journey.md) · [19-staff-journey.md](19-staff-journey.md) · → [23-view-catalog.md](23-view-catalog.md)

> **Actor**: `Admin` (`UserRole.Admin`). Actor thứ 4 trong chuỗi chạy lần lượt. **Không có journey "đăng ký"** — không có API nào tự thăng cấp lên Admin; mọi tài khoản Admin hiện tại được tạo bằng sửa DB trực tiếp (có chủ đích, có `AdminRoleDriftDetectionJob` riêng phát hiện Admin lạ xuất hiện và cảnh báo mọi Admin khác — xem Journey 10). Vì vậy journey của Admin bắt đầu thẳng từ "đăng nhập bằng tài khoản đã được cấp sẵn".
>
> **Ký hiệu**: giống [17](17-audience-journey.md)/[18](18-owner-journey.md)/[19](19-staff-journey.md).
>
> **Cập nhật**: 2026-08-13.

---

## Mục lục journey

1. Duyệt venue mới
2. Kiểm duyệt show & livestream
3. Xử phạt venue & xử lý kháng cáo
4. Xử lý yêu cầu hoàn tiền (vé) & hoàn donate
5. Xác minh bank account
6. Xử lý khiếu nại (complaint)
7. Quản lý người dùng
8. Quản lý taxonomy nền tảng & gói subscription
9. Buộc dừng livestream vi phạm
10. Giám sát vận hành & tài chính

---

## Journey 1 — Duyệt venue mới

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Xem danh sách venue đang chờ duyệt | `GET /admin/lounges/pending` | `[XEM]` | |
| 2a | 🔀 Duyệt | `POST /admin/lounges/{id}/approve` | `[BẤM]` | ↔ Owner của venue đó mở khoá được Journey "tạo show" ([18 J4](18-owner-journey.md)) ngay lập tức |
| 2b | 🔀 Từ chối | `POST /admin/lounges/{id}/reject` | `[NHẬP]` Reason → `[BẤM]` | Owner không có cách "nộp lại đúng venue đó" — phải tạo venue mới |

---

## Journey 2 — Kiểm duyệt show & livestream

Cùng 1 controller (`RequireAdmin`), 2 route riêng cho 2 loại target — đây là **duy nhất 1 điểm nghẽn kiểm duyệt** cho cả 2 domain Show/Event và Livestream.

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Xem hàng chờ kiểm duyệt | `GET /moderations/pending?targetType=` | `[XEM]` | Lọc theo `show`/`livestream`; AI đã chấm điểm rủi ro trước (enqueue tự động lúc Owner nộp duyệt/tạo livestream), Admin xem điểm này làm gợi ý |
| 2a | 🔀 Duyệt show | `POST /moderations/shows/{id}/review` | `[NHẬP]` Decision=Approved, ReviewNote? → `[BẤM]` | ↔ `LoungeShow→Published`, hiện công khai ngay cho mọi Audience |
| 2b | 🔀 Từ chối show | `POST /moderations/shows/{id}/review` | `[NHẬP]` Decision=Rejected, ReviewNote → `[BẤM]` | Show về lại `Draft`, Owner sửa và nộp lại |
| 3a | 🔀 Duyệt livestream | `POST /moderations/livestreams/{id}/review` | `[NHẬP]` Decision=Approved → `[BẤM]` | ↔ Mở khoá `POST /livestreams/{id}/start` cho Owner/Staff — thiếu bước này thì có khai báo VCPMC đầy đủ cũng không phát được |
| 3b | 🔀 Từ chối livestream | (như trên) | `[NHẬP]` Decision=Rejected, ReviewNote → `[BẤM]` | |

---

## Journey 3 — Xử phạt venue & xử lý kháng cáo

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Phát hiện vi phạm, ra quyết định xử phạt | `POST /venue-penalties` | `[NHẬP]` LoungeId, PenaltyType (Warning/Suspension/Ban), Reason, EvidenceRef, SuspensionDays? → `[BẤM]` | ↔ Owner nhận thông báo, có thể bị khoá 1 phần chức năng tuỳ mức phạt |
| 2 | ↔ *(thụ động)* Owner gửi kháng cáo | — | — | Xem hàng chờ ở đâu? — không có endpoint "danh sách mọi kháng cáo đang mở" riêng, Admin tra từng `GET /venue-penalties/{id}` theo id đã biết (khoảng trống UX tiềm ẩn, đáng lưu ý khi thiết kế màn hình Admin) |
| 3 | Xử lý kháng cáo | `POST /venue-penalties/{id}/appeal/review` | `[NHẬP]` Decision (Overturned/Upheld), ReviewNote → `[BẤM]` | 🔀 Chỉ xử lý được kháng cáo đang `Appealed`. `Overturned` **không tự mở lại venue** nếu còn hình phạt Active khác chồng lên; nếu phạt đã ảnh hưởng tài chính (co ngắn subscription) thì **không tự hoàn tác** — Admin phải tự vào sửa `owner_subscriptions`/ledger thủ công, hệ thống chỉ nhắc chứ không tự làm |
| 4 | 🔀 *(nhánh thay thế)* Admin không xử lý kịp SLA | — | — | ↔ `AutoApproveOverdueAppealsJob` tự động Overturn giùm — cùng khoá (`appeal-review:{penaltyId}`) với bước 3 để tránh 2 quyết định chồng nhau nếu Admin bấm đúng lúc job cũng chạy |

---

## Journey 4 — Xử lý yêu cầu hoàn tiền (vé) & hoàn donate

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Xem danh sách refund request đang chờ | `GET /admin/refund-requests` | `[XEM]` | Sinh ra từ 3 nguồn: Audience tự huỷ vé, Owner huỷ show, hoặc Journey 6 bước 3 (Take-down do khiếu nại) |
| 2a | 🔀 Từ chối | `POST /admin/refund-requests/{id}/process` | `[NHẬP]` Decision=Rejected → `[BẤM]` | Không đụng tiền |
| 2b | 🔀 Duyệt (toàn phần hoặc một phần) | `POST /admin/refund-requests/{id}/process` | `[NHẬP]` Decision=Approved, ApprovedAmount? (mặc định = AmountRequested nếu bỏ trống) → `[BẤM]` | Đảo bút toán sổ cái theo đúng tỉ lệ, co lại settlement chưa `Released` tương ứng. **Chỉ đảo bút toán nội bộ — không gọi API hoàn tiền thật của VNPay** (giới hạn môi trường sandbox, không phải thiếu sót) |
| 3 | *(escape-hatch)* Tự tạo refund request thủ công | `POST /admin/refund-requests` | `[NHẬP]` (tương đương field CreateRefundRequestCommand) → `[BẤM]` | Dùng khi có khiếu nại chính đáng nhưng khán giả đã lỡ hạn tự huỷ — vẫn phải qua bước duyệt như bình thường |
| 4 | Hoàn 1 donate | `POST /admin/donations/{id}/refund` | `[NHẬP]` Reason → `[BẤM]` | 🔀 Chỉ hợp lệ **trước khi** Owner xác nhận đã trả nghệ sĩ (chặng 2). Đảo bút toán chặng 1, không gọi VNPay (donate không có `Payment` row riêng để dùng lại cơ chế refund vé) |

---

## Journey 5 — Xác minh bank account

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Owner đăng ký bank account (ngoài tầm Admin) | — | — | Xem [18 J1 bước 3](18-owner-journey.md) |
| 2 | Xác minh thủ công (đối soát ngoài hệ thống, không có API ngân hàng tích hợp) | `POST /admin/bank-accounts/{id}/verify` | `[BẤM]` | ↔ Mọi `Settlement` từng bị chặn vì tài khoản chưa xác minh **tự động retry ngay** — Owner không cần thao tác gì thêm để nhận lại tiền đang bị giữ |

---

## Journey 6 — Xử lý khiếu nại (complaint)

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Xem hàng chờ khiếu nại | `GET /admin/complaints` | `[XEM]` | Gồm cả khiếu nại từ khách vãng lai (không tài khoản) |
| 2 | Ra quyết định xử lý | `POST /admin/complaints/{id}/resolve` | `[NHẬP]` Status, Resolution, ResolvedAction (vd Refund/Compensate/TakeDownContent), RefundAmount? → `[BẤM]` | 🔀 3 nhánh theo `ResolvedAction`: (a) `Refund`/`Compensate` nhắm vào 1 vé cụ thể → tự tạo `RefundRequest` thật, chờ xử lý tiếp ở Journey 4; (b) `TakeDownContent` với `TargetType="show"` → gỡ show, hoàn 100% mọi vé `Confirmed` (logic riêng, **không** gọi lại `CancelLoungeShowCommand` — 2 bản logic song song cần đồng bộ tay nếu 1 bên đổi); (c) các action khác → chỉ ghi nhận kết quả, không tự động hoá gì thêm |
| 3 | ↔ Người khiếu nại nhận kết quả | — | — | Nếu là khách vãng lai gửi ban đầu → **không tra lại được** kết quả qua `GET /complaints/{id}` (yêu cầu tài khoản), khác người đã đăng nhập |

---

## Journey 7 — Quản lý người dùng

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Tìm kiếm người dùng | `GET /admin/users?searchText=&role=&isActive=` | `[XEM]` | |
| 2 | Xem chi tiết 1 người dùng | `GET /admin/users/{id}` | `[XEM]` | |
| 3 | Đối chiếu CCCD/CMND đã nộp (KYC) | `GET /admin/users/{id}/citizen-card/{side}` | `[XEM]` | File nằm ngoài `wwwroot`, không đoán URL truy cập trực tiếp được |
| 4a | 🔀 Khoá tài khoản vi phạm | `POST /admin/users/{id}/deactivate` | `[BẤM]` | |
| 4b | 🔀 Mở lại tài khoản | `POST /admin/users/{id}/reactivate` | `[BẤM]` | |

---

## Journey 8 — Quản lý taxonomy nền tảng & gói subscription

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Thêm category/genre/mood/atmosphere mới | `POST /admin/categories`, `/admin/genres`, `/admin/moods`, `/admin/atmospheres` | `[NHẬP]` → `[BẤM]` | Taxonomy dùng chung **toàn nền tảng**, không phải của riêng 1 Owner — trước đây các bảng này không có cách nào populate, Owner tạo show sẽ luôn thấy danh sách rỗng nếu Admin chưa làm bước này |
| 2 | Tạo gói subscription mới | `POST /subscriptions/packages` | `[NHẬP]` Name, Price, BillingCycle, MaxTicketsPerEvent, HasAiPoster, MaxAiPostersPerMonth, MaxTourScenes → `[BẤM]` | ↔ Owner thấy gói mới ngay ở `GET /subscriptions/packages` |
| 3 | Sửa gói đang bán | `PUT /subscriptions/packages/{id}` | `[NHẬP]` (như trên) + IsActive → `[BẤM]` | Owner đã mua gói cũ giữ nguyên **snapshot quyền lợi** tại thời điểm mua — sửa gói không ảnh hưởng ngược |

---

## Journey 9 — Buộc dừng livestream vi phạm

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Phát hiện vi phạm khi livestream đang phát | — | — | |
| 2 | Buộc dừng | `POST /livestreams/{id}/terminate` | `[NHẬP]` Reason → `[BẤM]` | `Status=Terminated` (khác `Ended` tự nhiên — ghi rõ `TerminatedById`+`TerminatedReason`). ↔ Mọi khán giả đang xem bị ngắt kết nối ngay |

---

## Journey 10 — Giám sát vận hành & tài chính

Phần lớn là journey **phản ứng** (Admin không chủ động tìm, mà nhận cảnh báo trước).

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | *(thụ động)* Nhận cảnh báo bảo mật/vận hành | — | — | ↔ 4 job nền (`AdminRoleDriftDetectionJob`, `LoginSpikeDetectionJob`, `PushFailureAlertJob`, `VnPayReconciliationJob`) đều gửi **tới mọi Admin đang tồn tại cùng lúc** — không có khái niệm "Admin trực ca" |
| 2 | Kiểm tra toàn vẹn sổ cái | `GET /admin/ledger/integrity-check` | `[XEM]` | Quét `SUM(debit)=SUM(credit)` theo từng `JournalId` — công cụ vận hành, không phải use case người dùng cuối |
| 3 | Xem thống kê toàn nền tảng | `GET /analytics/platform` | `[XEM]` | Khác `GET /analytics/my-lounge` (Owner) — endpoint riêng biệt theo policy, không phải cùng 1 endpoint check quyền bên trong |
| 4 | Ép chạy ngay 1 job nền (kiểm tra/khắc phục thủ công) | `POST /admin/jobs/{jobId}/trigger` | `[NHẬP]` jobId → `[BẤM]` | Chỉ chạy ngay 1 lần, không đổi lịch Cron định kỳ |

---

## Tổng hợp điểm giao thoa real-time (Admin ↔ actor khác)

| Hành động của Admin | Actor khác bị ảnh hưởng ngay lập tức | Kênh |
|---|---|---|
| Duyệt/từ chối venue | Owner (mở/khoá khả năng tạo show) | Push/in-app |
| Duyệt/từ chối show hoặc livestream | Owner + (nếu duyệt) mọi Audience thấy show công khai ngay | Push/in-app + hiển thị công khai tức thì |
| Terminate livestream | Mọi khán giả đang xem bị ngắt kết nối ngay | SignalR disconnect |
| Duyệt refund request / hoàn donate | Audience liên quan (số dư/trạng thái vé-donate đổi) | Push/in-app |
| Xử phạt venue | Owner (khoá 1 phần chức năng tuỳ mức) | Push/in-app |
| Xác minh bank account | Owner (settlement bị chặn trước đó tự giải phóng) | Ghi DB, Owner thấy khi vào lại `GET /me/earnings` |
