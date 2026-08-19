# 18 — Journey của Owner (Chủ phòng trà)

← [12-actors-and-authorization.md](12-actors-and-authorization.md) · [14-usecase-traces.md](14-usecase-traces.md) · [16-api-endpoint-catalog.md](16-api-endpoint-catalog.md) · [17-audience-journey.md](17-audience-journey.md) · → [23-view-catalog.md](23-view-catalog.md)

> **Actor**: `Owner` (`UserRole.Owner`) — actor thứ 2 trong chuỗi chạy lần lượt qua từng actor đã phân tích (sau [Audience](17-audience-journey.md)).
>
> **Ký hiệu**: giống [17](17-audience-journey.md) — `[NHẬP]`/`[XEM]`/`[BẤM]`, 🔀 rẽ nhánh, ↔ ảnh hưởng real-time tới actor khác.
>
> **Cập nhật**: 2026-08-13, dựng từ [14-usecase-traces.md §5/§7/§8](14-usecase-traces.md) (đọc lại trực tiếp) + [16-api-endpoint-catalog.md](16-api-endpoint-catalog.md).

---

## Mục lục journey

1. Đăng ký & thiết lập tài khoản Owner
2. Tạo & đưa venue vào hoạt động
3. Đăng ký gói subscription (điều kiện bắt buộc trước khi tạo show)
4. Tạo show mới & mở bán vé
5. Vận hành show tới ngày diễn (Offline / Livestream)
6. Điều chỉnh show đã publish (đổi lịch / định dạng / huỷ)
7. Nhận & xử lý donate cho nghệ sĩ
8. Theo dõi doanh thu & tài chính
9. Vận hành F&B
10. Đối phó xử phạt & kháng cáo

---

## Journey 1 — Đăng ký & thiết lập tài khoản Owner

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Đăng ký với Role="Owner" | `POST /auth/register` | `[NHẬP]` Email, Password, FullName, Phone?, AcceptTerms, Role="Owner" | Cùng luồng OTP email như Audience — không có form đăng ký riêng cho Owner |
| 2 | Xác thực email, đăng nhập | `POST /auth/verify-email` → `POST /auth/login` | `[NHẬP]` | |
| 3 | Đăng ký bank account nhận tiền (venue) | `POST /bank-accounts` | `[NHẬP]` OwnerType=Lounge, OwnerId, BankName, AccountNumber, AccountHolder, IsDefault → `[BẤM]` | 🔀 **Bắt buộc phải làm trước Journey 4** — chưa có bank account mặc định thì lịch settlement không tạo được khi vé đầu tiên bán ra (J8 bước 1) |
| 4 | Chờ Admin xác minh bank account | — (thụ động) | — | ↔ Admin bấm `POST /admin/bank-accounts/{id}/verify` — settlement nào từng bị chặn vì tài khoản chưa xác minh sẽ **tự động retry** ngay sau đó, Owner không cần thao tác lại |

---

## Journey 2 — Tạo & đưa venue vào hoạt động

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Tạo venue mới | `POST /lounges` | `[NHẬP]` Name, Description, AtmosphereId, Street/Ward/District/City, Lat/Long → `[BẤM]` | Venue tồn tại ngay ở trạng thái `Pending` — **chưa tạo được show** cho tới khi qua bước 2 |
| 2 | Chờ Admin duyệt | — (thụ động) | — | ↔ Admin `POST /admin/lounges/{id}/approve` hoặc `.../reject`. 🔀 Approved → mở khoá Journey 4; Rejected → phải sửa và tạo lại (không có endpoint "nộp lại đúng venue đó") |
| 3 | Upload & gắn ảnh đại diện, giấy phép kinh doanh | `POST /uploads/images` → `PUT /lounges/{id}/image`, `PUT /lounges/{id}/business-license` | `[NHẬP]` file → ImageUrl/DocumentUrl | |
| 4 | Thiết lập khu vực chỗ ngồi (Seating Zone) | `POST /lounges/{id}/zones` → `PUT .../layout-2d` hoặc `.../layout-3d` | `[NHẬP]` Name, Capacity → X/Y/Width/Height hoặc X/Y/Z | Zone + Capacity ở đây chính là **nguồn giới hạn sức chứa vật lý thật** dùng khi Audience giữ chỗ vé sau này (J4-Audience bước 2, lớp quota #3) |
| 5 | Gán nhân viên (Staff) cho venue | `GET /lounges/staff/lookup` → `POST /lounges/{id}/staff` | `[NHẬP]` email → UserId → `[BẤM]` | 🔀 User đang là Owner/Admin → chặn (`ConflictException`); User đang là Staff active ở venue khác → chặn, yêu cầu tạo tài khoản riêng. Chỉ tự đổi `Role: Audience→Staff` |
| 6 | *(tự chọn)* Tour ảo 360° | `POST /lounges/{id}/tour/scenes` (thủ công) hoặc `.../tour/scenes/stitch` (tự động ghép nhiều ảnh) | `[NHẬP]` ảnh panorama hoặc nhiều ảnh xoay vòng | 🔀 Ghép ảnh chạy nền — trả `attemptId` ngay, Owner tự poll `GET .../stitch/{attemptId}` (Pending/Succeeded/Failed) vì có thể mất 15–30+ giây. Giới hạn theo gói subscription (số scene) + giới hạn số lần thử riêng (chống lạm dụng CPU server) |
| 7 | *(tự chọn)* Thêm ảnh gallery | `POST /lounges/{id}/gallery` | `[NHẬP]` ImageUrl, Caption | Miễn phí, không giới hạn theo gói — khác tour 360° |
| 8 | *(tự chọn)* Tạo tiêu chí gợi ý riêng của venue | `POST /lounges/{id}/custom-criteria` | `[NHẬP]` Name, Key, DataType, Options | Dữ liệu này Audience sẽ set mức quan tâm ở `PUT /me/custom-preferences/{criteriaId}` |
| 9 | Tạo menu F&B (nếu venue có phục vụ) | Xem Journey 9 | | |

---

## Journey 3 — Đăng ký gói subscription (điều kiện bắt buộc trước khi tạo show)

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Xem các gói đang mở bán | `GET /subscriptions/packages` | `[XEM]` | Không cần login để xem giá |
| 2 | Chọn gói, khởi tạo thanh toán | `POST /subscriptions/subscribe` | `[NHẬP]` PackageId → `[BẤM]` | 🔀 Đang có subscription Active khác → chặn, phải huỷ trước (`POST /subscriptions/cancel`) |
| 3 | Thanh toán qua VNPay | (ngoài hệ thống) | `[NHẬP]` (trên VNPay) | |
| 4a | 🔀 **Thành công** | `GET /subscriptions/vnpay-return` + `.../vnpay-ipn` | tự động | `OwnerSubscription→Active`, snapshot quyền lợi (MaxTicketsPerEvent, HasAiPoster, MaxTourScenes...) — mở khoá Journey 4 |
| 4b | 🔀 **Thất bại** | (như trên) | tự động | Quay lại bước 2 |
| 5 | Gia hạn khi sắp hết hạn | `POST /subscriptions/renew` | `[BẤM]` (+ VNPay OTP) | Dùng lại gói lần trước — vẫn cần 1 lần thao tác VNPay thật, không tự động trừ tiền được |
| 6 | Xem gói hiện tại | `GET /subscriptions/my` | `[XEM]` | |

---

## Journey 4 — Tạo show mới & mở bán vé

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Tạo show (Draft) | `POST /lounge-shows` | `[NHẬP]` Name, Description, Format, ScheduledStart/End, CategoryId, Offline/OnlineQuota, GenreIds[], Performances[] (PerformerId hoặc tên mới, Role, SetTime, AcceptsDonation), CustomValues[] → `[BẤM]` | 🔀 Chưa có subscription Active tại đúng thời điểm này → chặn tạo. Nếu truyền tên nghệ sĩ mới (không có `PerformerId`) → **tự động tạo `Performer` mới** ngay trong bước này |
| 2 | Tạo hạng vé (TicketTier) + đợt giá | `POST /ticket-tiers` | `[NHẬP]` ShowId, Name, AccessType, ZoneId (nếu Physical), TotalCapacity, Prices[] → `[BẤM]` | 🔀 Chỉ tạo được khi show còn `Draft`. Tổng `TotalCapacity` mọi tier cộng lại không được vượt hạn mức gói subscription — kiểm tra sớm, **kiểm tra lại lần 2** lúc Audience giữ chỗ thật |
| 3 | *(tự chọn)* Tạo poster bằng AI | `POST /lounge-shows/{id}/ai-poster` | `[NHẬP]` StyleHint? → `[BẤM]` | Gate theo snapshot quyền lợi gói (`HasAiPosterSnapshot`) — kiểm trong handler, không phải ở tầng policy |
| 3b | 🔀 *(thay thế)* Tự upload poster thủ công | `POST /uploads/images` → `PUT /lounge-shows/{id}/poster` | `[NHẬP]` file → ImageUrl | Dùng khi không có quyền AI poster hoặc muốn ảnh riêng |
| 4 | Khai báo giấy phép biểu diễn + tác quyền VCPMC | `PUT /lounge-shows/{id}/legal-approval`, `PUT .../vcpmc-royalty` | `[NHẬP]` LegalApprovalReference, VcpmcRoyaltyReference | NĐ 144/2020 — bắt buộc kiểm tra lại lúc nộp duyệt |
| 5 | Nộp duyệt | `POST /lounge-shows/{id}/publish` | `[BẤM]` | 🔀 Venue chưa `Approved` hoặc chưa có ≥1 hạng vé → chặn |
| 6 | Chờ kiểm duyệt AI + Admin | — (thụ động) | — | ↔ Admin `POST /moderations/shows/{id}/review`. 🔀 Approved → `Status=Published`, show hiện công khai (Audience thấy ở J2); Rejected → về `Draft`, Owner sửa lại |
| 7 | Show hiện công khai, khán giả bắt đầu giữ chỗ/mua vé | — (thụ động) | — | ↔ Nối tiếp Journey 3 của [Audience](17-audience-journey.md) |

---

## Journey 5 — Vận hành show tới ngày diễn (Offline / Livestream)

### Nhánh A — Show Offline thuần (không livestream)

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| A1 | Bấm bắt đầu show tại quầy | `POST /lounge-shows/{id}/start` | `[BẤM]` (Staff/Owner) | `LoungeShow.Status→Ongoing`, `ActualStart=now` |
| A2 | Bấm kết thúc show | `POST /lounge-shows/{id}/end` | `[BẤM]` | `Status→Ended`, `ActualEnd=now`, mở cửa sổ đánh giá 7 ngày cho Audience (J7-Audience) |

### Nhánh B — Show Online (có Livestream)

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| B1 | Tạo Livestream cho show | `POST /livestreams` | `[NHẬP]` ShowId → `[BẤM]` | Cho tạo cả khi show còn `Draft`/`Pending` (hạ tầng phát chuẩn bị trước). Gọi Mux thật để cấp RTMP/Stream Key. **Tự động sinh thêm 1 vòng kiểm duyệt riêng** (`EventModeration(TargetType=Livestream)`) — độc lập với duyệt Show ở J4 bước 6 |
| B2 | Chờ Admin duyệt livestream | — (thụ động) | — | ↔ `POST /moderations/livestreams/{id}/review` |
| B3 | Lấy thông tin đăng nhập OBS | `GET /livestreams/{id}/credentials` | `[XEM]` | RTMP URL + Stream Key — không lộ ra khán giả |
| B4 | Bắt đầu phát | `POST /livestreams/{id}/start` | `[BẤM]` | 🔀 Chặn cứng nếu: chưa được Admin duyệt (dù đã publish show), hoặc chưa khai báo `VcpmcRoyaltyReference`. Thành công → đồng thời `Livestream→Live` **và** `LoungeShow→Ongoing`. ↔ Gửi `EventLive` cho mọi người đã mua vé + mọi người follow venue |
| B5 | Theo dõi viewer/chat trong lúc phát | `GET /livestreams/{id}/chat` | `[XEM]` | ↔ Chính là lúc Journey 4 của Audience (xem live + donate) diễn ra — Owner nhận thông báo donate real-time trong lúc này |
| B6 | Kết thúc phát | `POST /livestreams/{id}/end` | `[BẤM]` | `Livestream→Ended`, đồng thời `LoungeShow→Ended`, mở cửa sổ đánh giá 7 ngày. Best-effort xoá stream trên Mux (lỗi không chặn) |
| B7 | 🔀 *(ngoại lệ)* Cần dừng livestream giữa chừng vì vi phạm | — | — | ↔ Admin `POST /livestreams/{id}/terminate` — nằm ngoài quyền Owner, chỉ Admin làm được |

---

## Journey 6 — Điều chỉnh show đã publish

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1a | 🔀 Đổi lịch diễn | `PUT /lounge-shows/{id}/reschedule` | `[NHẬP]` NewScheduledStart → `[BẤM]` | Chỉ khi show `Published` (chưa `Ongoing`). Áp lại quy tắc 7-ngày-làm-việc cho **ngày mới**. ↔ Mọi chủ vé `Confirmed` nhận thông báo `EventRescheduled` |
| 1b | 🔀 Đổi Offline → Online | `PUT /lounge-shows/{id}/format` | `[NHẬP]` NewFormat → `[BẤM]` | Chỉ 1 chiều, không đổi ngược được. ↔ Mọi vé vật lý `Confirmed` bị huỷ + hoàn 100% tự động (`RefundRequest`, chờ Admin duyệt) |
| 1c | 🔀 Đổi chế độ phát 2D/3D | `PUT /lounge-shows/{id}/playback-mode` | `[NHẬP]` PlaybackMode → `[BẤM]` | Chỉ cần show không phải `Offline` — không gate theo subscription |
| 1d | 🔀 Huỷ toàn bộ show | `POST /lounge-shows/{id}/cancel` | `[BẤM]` | Chặn nếu livestream đang `Live` (phải terminate trước). ↔ Mọi vé `Confirmed` huỷ + hoàn 100%, mọi người mua vé nhận `EventCancelled` |

---

## Journey 7 — Nhận & xử lý donate cho nghệ sĩ

Tiếp nối trực tiếp Journey 4 của [Audience](17-audience-journey.md) (donate khi xem livestream).

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | ↔ Audience donate thành công | — (thụ động) | — | Nhận thông báo riêng tư "Bạn vừa nhận donate!" — chỉ số tiền, tên bị ẩn nếu donor chọn ẩn danh |
| 2 | Xem danh sách donate đang chờ xác nhận | `GET /donations/pending-ack` | `[XEM]` | |
| 3 | Xác nhận đã nhận tiền qua VNPay | `POST /donations/{id}/acknowledge` | `[BẤM]` | 🔀 Không thao tác trong 24h → hệ thống **tự động** chuyển `OwnerReceived` giùm (`AutoConfirmed=true`), kèm cảnh báo `DonationPending` nếu quá hạn. ↔ Broadcast công khai chặng 2/3 tới người xem show đó |
| 4 | Xem danh sách chờ trả nghệ sĩ | `GET /donations/awaiting-payout` | `[XEM]` | |
| 5 | Chuyển khoản thủ công cho nghệ sĩ (ngoài hệ thống), rồi xác nhận | `POST /donations/{id}/confirm-paid` | `[NHẬP]` PaymentRef, PaymentEvidenceUrl (ảnh chụp chuyển khoản) → `[BẤM]` | ↔ Broadcast công khai chặng 3/3 (cuối) |

---

## Journey 8 — Theo dõi doanh thu & tài chính

Phần lớn là journey **thụ động** — Owner chỉ xem, tiền tự vận hành theo lịch nền.

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | *(thụ động)* Vé đầu tiên của show được xác nhận | — | — | ↔ Hệ thống tự tạo 2 `Settlement` (Partial70 sau 48h, Final30 sau 14 ngày) — tốc độ giải ngân (50/70/80%) tính theo điểm rating trung bình + số show đã diễn của venue. 🔀 Chưa có bank account mặc định → **chặn cứng tại đây**, không cho tạo lịch (quay lại Journey 1 bước 3) |
| 2 | *(thụ động)* Tới hạn giải ngân | — | — | ↔ `SettlementReleaseJob` tự chạy, ghi có vào tài khoản Owner, gửi `SettlementReleased`. 🔀 Tranche cuối (Final30): nếu show kết thúc bất thường sớm (< 70% thời lượng dự kiến) → **không tự giải ngân**, chuyển `PendingReview`, chờ Admin xét — Owner chỉ nhận thông báo, không tự xử lý được |
| 3 | Xem tổng thu nhập | `GET /me/earnings` | `[XEM]` | Tổng hợp Settlement/Payment/Donation liên quan mọi venue của mình |
| 4 | Xem thống kê chi tiết venue | `GET /analytics/my-lounge?loungeId=` | `[XEM]` | |

---

## Journey 9 — Vận hành F&B

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Tạo menu | `POST /fnb-menus` | `[NHẬP]` LoungeId, Name, Description, DisplayOrder → `[BẤM]` | |
| 2 | Thêm món vào menu | `POST /fnb-menu-items` | `[NHẬP]` MenuId, Category, Name, Price, ImageUrl, DisplayOrder → `[BẤM]` | |
| 3 | *(vận hành hàng ngày)* Đặt hộ khách tại quầy | `POST /fnb-orders` (Role=Owner/Staff) | `[NHẬP]` | Cùng endpoint Audience dùng, khác ở việc gắn `StaffId` thay vì `AudienceUserId` |
| 4 | Xem danh sách đơn của venue | `GET /fnb-orders?loungeId=` | `[XEM]` | |
| 5 | Cập nhật trạng thái đơn khi phục vụ | `PUT /fnb-orders/{id}/status` | `[BẤM]` (chuỗi bắt buộc Pending→Preparing→Served→Paid) | 🔀 `Cancelled` là lối thoát riêng, được từ bất kỳ bước nào trước `Paid`. Khi chuyển `Paid` → tự tạo 1 `Payment` ghi nhận (chỉ để đối soát nội bộ, **không** vào sổ cái nền tảng — F&B không thu hoa hồng) |

---

## Journey 10 — Đối phó xử phạt & kháng cáo

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | *(thụ động)* Nhận quyết định xử phạt | — | — | ↔ Admin `POST /venue-penalties` (Warning/Suspension/Ban) |
| 2 | Xem chi tiết + lịch sử mọi phạt của mình | `GET /venue-penalties/mine`, `GET /venue-penalties/{id}` | `[XEM]` | |
| 3 | Gửi kháng cáo | `POST /venue-penalties/{id}/appeal` | `[NHẬP]` AppealReason → `[BẤM]` | 🔀 Chỉ kháng cáo được khi phạt đang `Active`. Không xử lý kịp SLA → hệ thống **tự động Overturn** giùm (job nền), Owner không cần chờ Admin |
| 4 | *(thụ động)* Admin/hệ thống ra quyết định | — | — | 🔀 `Overturned` **không tự mở lại** venue nếu còn hình phạt Active khác chồng lên; nếu phạt đã ảnh hưởng tài chính (co ngắn subscription) thì **không tự hoàn tác** — Admin phải tự điều chỉnh thủ công, Owner chỉ nhận kết quả cuối |

---

## Tổng hợp điểm giao thoa real-time (Owner ↔ actor khác)

| Hành động của Owner | Actor khác bị ảnh hưởng ngay lập tức | Kênh |
|---|---|---|
| Duyệt show/livestream (thực ra Admin, không phải Owner) | — | — |
| Bắt đầu phát livestream | Mọi người đã mua vé + mọi người follow venue | Push/in-app `EventLive` |
| Owner acknowledge / confirm-paid 1 donate | Mọi người đang xem show đó (kể cả chưa đăng nhập) | SignalR `PublicDonationHub` — chặng 2/3, 3/3 |
| Đổi lịch / đổi định dạng / huỷ show | Mọi Audience đã mua vé show đó | Push/in-app `EventRescheduled`/`EventCancelled` + tự động tạo `RefundRequest` nếu áp dụng |
| Gán Staff mới | Chính người được gán (đổi `Role→Staff`, có quyền vận hành venue ngay) | Cập nhật quyền tức thì ở lần đăng nhập kế tiếp |
| Cập nhật trạng thái đơn F&B | Audience đã đặt đơn đó (theo dõi qua `GET /fnb-orders/my`) | Đọc lại qua polling, chưa có push riêng cho F&B |
