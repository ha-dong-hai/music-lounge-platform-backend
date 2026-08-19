# MusicLounge Backend — Phân tích nghiệp vụ tổng hợp

> Tài liệu tổng hợp từ toàn bộ quá trình phân tích code thực tế (không dựa SRS/ERD gốc). Chi tiết đầy đủ kèm trích dẫn code xem bộ [docs/11-15](11-ba-domain-analysis.md). Tài liệu này là bản súc tích dành cho trình bày/bàn giao.
> Cập nhật: 2026-08-13, theo working tree hiện tại (một số phần đã được đội dev vá trong quá trình phân tích — tài liệu phản ánh trạng thái sau khi vá).

---

## 1. Tổng quan dự án

MusicLounge là nền tảng kết nối phòng trà nhạc sống (venue), nghệ sĩ biểu diễn và khán giả tại Việt Nam. Ba trụ cột nghiệp vụ chính: **bán vé** (offline tại quầy, online, hoặc hybrid có kèm livestream), **kiếm tiền cho venue qua gói subscription** (thay vì hoa hồng trên từng vé), và **donate/tip trực tiếp cho nghệ sĩ**. Đi kèm là các tính năng phụ trợ: đặt F&B tại chỗ, gợi ý cá nhân hoá bằng AI, tạo poster AI, và tour ảo 360° cho venue. Hệ thống build trên .NET 8 (Clean Architecture: Domain → Application → Infrastructure → Api, CQRS qua MediatR), SQL Server, tích hợp VNPay (thanh toán), Mux (livestream), Firebase (push notification), SpeedSMS (OTP), và 1 microservice Python riêng để ghép ảnh panorama. Đây là dự án đồ án tốt nghiệp (capstone) — môi trường sandbox, chưa có quan hệ merchant/đối tác thật với các bên thứ ba.

---

## 2. Actor & phân quyền

### 2.1 Vai trò chính

| Actor | Quyền hạn chính | Giới hạn |
|---|---|---|
| **Admin** | Duyệt/từ chối venue mới, xử phạt venue, duyệt nội dung show & livestream (AI hỗ trợ chấm điểm trước), xử lý refund, khoá/mở tài khoản, xác minh bank account | Không có API tự thăng cấp thành Admin — mọi tài khoản Admin hiện tại đều được tạo bằng sửa DB trực tiếp (có chủ đích, có job riêng phát hiện Admin lạ xuất hiện) |
| **Owner** | Tạo/quản lý venue, tạo show + hạng vé, quản lý Staff, đăng ký bank account, subscribe gói, kháng cáo phạt | Không tự duyệt venue/gỡ phạt của chính mình; mọi hành động venue-cụ-thể đều kiểm tra đúng `OwnerId` |
| **Staff** | Vận hành sàn diễn: check-in vé, bán vé quầy, cập nhật đơn F&B, start/end show, điều khiển livestream | **Chỉ 1 venue tại 1 thời điểm** (ràng buộc unique ở cả DB lẫn tầng ứng dụng) — quyền gắn theo claim `lounge_id` trong JWT, không theo Role suông |
| **Audience** | Mua vé, donate, đặt F&B, follow/wishlist, đánh giá, gửi khiếu nại | — |
| **Khách chưa đăng nhập** | Duyệt catalog công khai (venue/show/menu/tour 360°), xem sổ minh bạch donate công khai (theo nghệ sĩ hoặc toàn hệ thống), gửi khiếu nại không cần tài khoản + tra cứu lại kết quả bằng id+SĐT (`GET /complaints/lookup`), hoặc nhận SMS chủ động khi Admin xử lý xong | Không mua/donate/follow được |
| **Performer (nghệ sĩ)** | — | Không có tài khoản đăng nhập — là entity nghiệp vụ thuần, mọi thao tác đều do Owner/Admin làm thay. Vẫn có identity tài chính thật (`AccountType.Performer` trong sổ cái) và xuất hiện công khai gián tiếp qua domain Show, không qua route `/performers` (route đó là `RequireOwner`) — chi tiết: [22-performer-presence.md](22-performer-presence.md) |
| **VNPay / Background Job** | Callback xác nhận thanh toán; job nền dọn dẹp/đối soát/cảnh báo | Không phải người dùng — xác thực bằng chữ ký HMAC (VNPay) hoặc chạy ngoài HTTP pipeline (job) |

### 2.2 Cơ chế phân quyền

3 lớp: **Role** (JWT claim) → **Policy** (`RequireAuthenticated/Owner/Admin/VenueOperator` — ánh xạ N-role→1-policy, không dùng `[Authorize(Roles=...)]` trực tiếp) → **Venue scoping** (`VenueOperatorAccess`, so JWT với `OwnerId`/`lounge_id` thật). Có `FallbackPolicy` yêu cầu đăng nhập mặc định toàn hệ thống — endpoint nào lỡ quên gắn `[Authorize]` vẫn an toàn (fail-closed).

**Biến thể dữ liệu đáng chú ý**: khách mua vé tại quầy (`Ticket.BuyerId = null`) và người gửi khiếu nại vãng lai (`Complaint.ComplainantUserId = null`, dùng `ContactPhone` thay) — 2 trường hợp thực thể nghiệp vụ tồn tại mà **không có** `User` tương ứng.

---

## 3. Mô hình dữ liệu (domain model)

68 entity, chia thành ~15 nhóm chức năng (Auth, Venue, Catalog/Taxonomy, Show, Performer, Ticket, Payment/Finance, Livestream, F&B, Recommendation, Notification, Follow/Wishlist, Security-nội bộ, Complaint, Config). Một số điểm kiến trúc dữ liệu quan trọng:

- **Sổ cái kế toán kép thật sự**: `Account` (5 loại: Gateway/Platform/Tax/User/Performer) + `LedgerEntry` (append-only, `SUM(debit)=SUM(credit)` theo từng `JournalId`) — không phải bảng log thông thường.
- **Polymorphic không FK thật**: `BankAccount`, `Account` dùng `OwnerType+OwnerId` trỏ tới Lounge hoặc Performer — toàn vẹn dữ liệu phụ thuộc tầng ứng dụng, không phải DB constraint.
- **1-1 thật qua shared primary key**: `PhysicalTicketDetail`/`LivestreamTicketDetail` dùng chính `TicketId` làm PK (không có `Id` riêng).
- **Snapshot pattern (D12)** lặp lại xuyên suốt: `OwnerSubscription`, `Settlement`, `Payment` đều chốt số liệu (quyền lợi gói, tỷ lệ chia, giá) tại đúng thời điểm giao dịch — sửa cấu hình gốc sau đó không ảnh hưởng ngược giao dịch cũ.

### 3.1 Quan hệ cốt lõi (rút gọn — đầy đủ xem [13-data-model.md §2](13-data-model.md#2-quan-hệ-giữa-các-entity))

```
User (1)──(n) MusicLounge ──(n) LoungeShow ──(n) TicketTier ──(n) TicketPrice ──(n) Ticket
                  │                  │
                  │                  └──(n) Performance ──(n) Performer
                  └──(n) LoungeStaff──(n) User (Staff, 1 venue active/lúc)

Payment (1)──(n) LedgerEntry          Payment (1)──(n) Settlement (2 tranche)
Performance (1)──(n) Donation          User (1)──(n) OwnerSubscription──(n) SubscriptionPackage
```

### 3.2 State machine chính

| Entity | Trạng thái | Ghi chú chuyển trạng thái |
|---|---|---|
| `MusicLounge.Status` | Pending→Approved/Rejected, Warned, Suspended, Locked | Admin duyệt venue mới; xử phạt 3 mức có độ trễ báo trước |
| `LoungeShow.Status` | Draft→Pending→Published→Ongoing→Ended, hoặc Cancelled | Nộp duyệt cần venue Approved + ≥1 hạng vé; duyệt qua AI chấm điểm trước + Admin quyết cuối |
| `Ticket.Status` | Pending→Confirmed→Used, hoặc Cancelled/Refunded | QR chỉ sinh khi Confirmed |
| `Donation.Status` | PendingPayment→PendingOwnerAck→OwnerReceived→PerformerPaid, hoặc Cancelled/Refunded (hoàn chỉ được trước PerformerPaid) | Auto-confirm sau 24h nếu Owner không thao tác |
| `Settlement.Status` | Scheduled→Released, hoặc PendingReview/Cancelled | PendingReview nếu show kết thúc bất thường sớm (chống gian lận) |

---

## 4. Luồng nghiệp vụ theo từng domain

| Domain | Tóm tắt luồng & quy tắc nổi bật |
|---|---|
| **Auth & Tài khoản** | Đăng ký chỉ chọn được Audience/Owner (không tự đăng ký Admin/Staff); xác thực email bằng OTP mới cấp token lần đầu; đăng nhập có 3 lớp phòng vệ (khoá tạm sau nhiều lần sai, chống dò thời gian phản hồi, log tấn công hàng loạt); DSAR (xuất/xoá dữ liệu) tuân Luật 91/2025/QH15 — xoá là ẩn danh hoá tại chỗ, giữ nguyên bản ghi tài chính/audit theo Luật Kế toán (giữ 10 năm). |
| **Venue/Lounge** | Owner tạo venue → Admin duyệt (`Pending→Approved`) mới cho phép nộp duyệt show. Xử phạt 3 mức (Warning/Suspension/Ban) có độ trễ báo trước, tự động gỡ phạt nếu Admin không xử lý kháng cáo kịp SLA. Staff gán theo venue, chỉ hoạt động ở 1 venue/lúc. Tour 360° gate theo gói subscription (`MaxTourScenes`), ghép ảnh qua microservice riêng, có giới hạn chống lạm dụng số lần thử. |
| **Show/Event** | Tạo show yêu cầu Owner đang có subscription Active. Nộp duyệt → kiểm duyệt 2 lớp (AI chấm điểm rủi ro + Admin quyết, đồng thời xác nhận luôn giấy phép biểu diễn NĐ 144/2020). Đổi lịch phải áp lại quy tắc 7-ngày-làm-việc cho ngày mới (chặn lách hạn); đổi Offline→Online hoàn 100% vé vật lý. AI Poster gate theo 2 lớp giới hạn độc lập (quota tháng theo gói + chống-lạm-dụng theo show). Số nghệ sĩ/lượt diễn mỗi show **không giới hạn** (0 đến vô hạn về code — không có rule tối thiểu/tối đa), chỉ ràng buộc 1 nghệ sĩ không trùng lặp trong cùng show (DB unique index). |
| **Ticket** | Giữ chỗ (15 phút) → Mua → Xác nhận qua callback VNPay (verify chữ ký + đối chiếu số tiền, fail-closed). 5 lớp kiểm tra sức chứa trước khi giữ chỗ, trong đó có kiểm tra **sức chứa vật lý thật của khu vực** (an toàn, không chỉ logic bán hàng). Check-in QR khoá chống quét trùng. Chuyển nhượng vé đổi cả QR lẫn access token vì lý do bảo mật. |
| **Payment/Finance** (Donate, Settlement, Refund) | Donate chỉ khi show đang diễn ra, có Owner xác nhận nhận tiền (tự động sau 24h nếu im lặng) rồi mới chuyển nghệ sĩ — tỷ lệ chia chốt tại lúc xác nhận thanh toán. Donor giờ được báo riêng khi donate thành công (trước đây chỉ Owner được báo). Có sổ minh bạch donate công khai toàn hệ thống (đầy đủ breakdown phí, không cần đăng nhập) — cố ý chỉ hiện giao dịch đã "chốt" (`OwnerReceived`+), theo đúng pattern "pending vs. posted" ngành ngân hàng; 2 tuỳ chọn ẩn danh/ẩn số tiền độc lập nhau, đối chiếu khớp mô hình thật của GoFundMe. Settlement 2 đợt (tốc độ giải ngân theo uy tín venue: rating + số show đã diễn), đợt cuối chặn tự động nếu show kết thúc bất thường sớm. Refund giờ có đủ 3 đường tạo (khán giả tự hủy, Owner hủy show, Admin escape-hatch thủ công) + gọi API hoàn tiền VNPay thật (dù sandbox dự án chưa hỗ trợ, có cờ báo Admin chuyển tay khi cần). |
| **Livestream** | Tạo livestream tự động kéo theo 1 vòng kiểm duyệt AI+Admin bắt buộc. Bắt đầu phát yêu cầu đã duyệt + đã khai báo tác quyền VCPMC. Giới hạn số thiết bị xem đồng thời/vé (mặc định 2). Show Offline thuần vẫn có cặp lệnh Start/End riêng (không qua Livestream) để đảm bảo luôn có mốc thời gian thật cho cơ chế chống gian lận settlement. |
| **F&B** | Đặt món do khách tự đặt hoặc Staff đặt hộ tại quầy, chỉ thanh toán tiền mặt. Trạng thái đơn tuần tự bắt buộc (Pending→Preparing→Served→Paid), có lối thoát riêng (Cancelled) trước khi thanh toán. Đánh dấu "Paid" tạo bản ghi kiểm toán riêng (không vào sổ cái nền tảng vì F&B không thu hoa hồng). |
| **Recommendation/Analytics** | Gợi ý 3 tầng (Trending → Content-based → Hybrid), công thức `FinalScore = Content×0.5 + Collab×0.3 + Custom×0.2 (+0.15 nếu follow venue)`, chỉ chạy Hybrid khi đủ dữ liệu hành vi. Toàn bộ gợi ý cá nhân hoá tắt nếu user chưa đồng ý `AiConsent`. Venue tự định nghĩa tiêu chí gợi ý riêng ngoài genre/mood/atmosphere chuẩn. |
| **Notification** | 1 điểm phát dùng chung cho toàn hệ thống — ghi row in-app + enqueue push FCM cùng lúc, 2 kênh độc lập (lỗi push không mất thông báo in-app). Cảnh báo vận hành nội bộ (bảo mật, đối soát, sổ cái lệch) gửi tới **mọi Admin**, không phân công theo ca. |
| **Follow/Wishlist** | Đơn giản — follow venue không kiểm tra trạng thái venue, wishlist show có chặn Draft/Cancelled. Cả 2 là input cho gợi ý cá nhân hoá và trigger thông báo (show mới, sắp hết vé). |

---

## 5. Ràng buộc & rủi ro cần lưu ý

Qua 4 lượt rà soát rủi ro phi chức năng + 2 finding phát hiện sau đó khi xây tính năng minh bạch donate và viết journey actor "Khách chưa đăng nhập" (chi tiết: [15-risk-audit.md](15-risk-audit.md)), phát hiện 16 vấn đề — **tất cả đã được xác nhận khắc phục** tính đến thời điểm tài liệu này. Điểm chung đáng ghi nhớ cho các lần phát triển tiếp theo:

- **Nền tảng chắc, nhánh phụ dễ hở**: cơ chế transaction (`TransactionBehavior` bọc thật mọi command trong 1 DB transaction), phân quyền (`FallbackPolicy` deny-by-default), và validate input cốt lõi (donate/vé/giá) đều làm tốt ngay từ đầu. Toàn bộ 16 vấn đề tìm được đều nằm ở **nhánh rẽ ít đi qua** — venue quên xác minh bank account, khiếu nại/donate cần hoàn tiền ngoài kịch bản chính, dữ liệu ML input chưa từng được ghi (khiến 30% công thức gợi ý luôn bằng 0 mà không có lỗi rõ ràng nào báo hiệu), donor không được báo khi donate thành công (chỉ Owner được báo), khiếu nại khách vãng lai không có kênh biết kết quả hay tra cứu lại.
- **Bài học quy trình**: 1 handler tiền throw exception bên trong transaction của 1 handler khác (qua domain event `Publish`) có thể rollback luôn cả giao dịch đã hợp lệ — đã xảy ra thật với luồng xác nhận vé, sửa bằng cách tách "ghi nhận thanh toán" và "lên lịch trả tiền" thành 2 bước độc lập, bước sau lỗi thì chỉ báo chứ không cuốn theo bước trước.
- **Giới hạn môi trường (không phải lỗi code)**: VNPay sandbox của dự án không cấp chức năng hoàn tiền qua API (capstone, chưa có quan hệ merchant thật) — code đã implement đúng cấu trúc, có cờ báo Admin xử lý tay khi gateway không phản hồi. Tương tự, kênh SMS demo thực tế dùng Twilio (số nước ngoài) dù code hiện gọi SpeedSMS.vn.
- **Mã hoá PII** (CCCD, số tài khoản NH) dùng Data Protection API chuẩn, đã cấu hình lưu khoá bền vững cho single-machine — cần bổ sung key store dùng chung nếu sau này scale nhiều instance.

---

## 6. Câu hỏi / điểm chưa rõ cần xác nhận lại

| # | Câu hỏi | Gửi tới |
|---|---|---|
| 1 | Policy `RequireStaff` (Program.cs) được định nghĩa nhưng không dùng ở endpoint nào — dọn bỏ hay giữ lại có chủ đích (dự phòng)? | Đội dev |
| 2 | Kênh SMS chính thức cho production sau này là SpeedSMS.vn (đã code) hay giữ Twilio — quyết định cuối để đồng bộ code với hạ tầng thật? | Đội dev |
| 3 | `LegalApprovalReference`/`VcpmcRoyaltyReference` (giấy phép biểu diễn, tác quyền VCPMC) hiện là Owner tự khai báo, Admin xác nhận thủ công bằng mắt — có cần tích hợp tra cứu với hệ thống của Sở VHTT/VCPMC không, hay giữ nguyên xác nhận thủ công vì phạm vi capstone? | GVHD (định hướng phạm vi đồ án) |
| 4 | Ngưỡng "% giữ lại làm bộ đệm an toàn" theo tier uy tín venue (50/30/20%) và ngưỡng hoàn thành show 70% để giải ngân — đã có giá trị mặc định hợp lý, cần GVHD/đội dev xác nhận đây có phải con số cuối cùng để bảo vệ đồ án hay chỉ là giá trị tạm? | GVHD |
| 5 | Chưa có kế hoạch scale nhiều instance — nếu đồ án cần demo high-availability, cần bổ sung key store dùng chung cho Data Protection (hiện chỉ lưu file hệ thống 1 máy). | Đội dev |
| 6 | Sổ donate công khai hiện cho donor ẩn cả số tiền (`IsAmountPublic`), giống GoFundMe — có nên siết chặt hơn theo hướng Ko-fi (luôn bắt buộc hiện số tiền, chỉ được ẩn tên) để tăng minh bạch tài chính? Đây là quyết định chính sách, chưa có câu trả lời. | GVHD / Đội dev |

---

*Tài liệu tổng hợp — chi tiết đầy đủ kèm trích dẫn dòng code xem [11-ba-domain-analysis.md](11-ba-domain-analysis.md), [12-actors-and-authorization.md](12-actors-and-authorization.md), [13-data-model.md](13-data-model.md), [14-usecase-traces.md](14-usecase-traces.md), [15-risk-audit.md](15-risk-audit.md).*
