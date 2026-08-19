# Platform Architecture — MusicLounge Client Surfaces

← [View-Design-Spec.md](View-Design-Spec.md)

> Backend (REST + SignalR) không ép buộc client nào — mobile hay web đều gọi được y hệt API. Việc chia surface dưới đây là **quyết định sản phẩm**, chốt ngày 2026-08-14, dựa theo bản chất công việc thật của từng actor (đối chiếu tiền lệ thực tế: Eventbrite/DICE cho vé, Toast/Square cho vận hành tại chỗ, Shopify Admin/Stripe Dashboard cho quản trị doanh nghiệp).

## 5 client surface

| Surface | Actor | Loại | Vì sao |
|---|---|---|---|
| **Audience Web** | Audience | Website (responsive, chạy được cả desktop lẫn trình duyệt điện thoại) | Phần lớn hành vi xem hòa nhạc là **đến trực tiếp venue**, không phải theo dõi liên tục qua app — khám phá/mua vé/xem live/donate/quản lý vé không cần app riêng. Vé QR ở cửa vẫn mở được bình thường qua trình duyệt điện thoại, không cần app cài sẵn. |
| **Audience Mobile (F&B)** | Audience | App mobile native (iOS/Android) | Đúng 1 tình huống thật sự cần "điện thoại cầm tay, tại bàn, ngay lúc đó": đặt đồ ăn/thức uống khi đang ngồi xem trực tiếp tại venue. Phạm vi cố ý rất hẹp — không nhồi thêm tính năng khác vào app này. |
| **Owner Web** | Owner | Website (dashboard) | Nghiệp vụ nặng: form nhiều field, bảng dữ liệu, Zone Editor kéo-thả sơ đồ chỗ ngồi, dashboard tài chính — công việc kiểu bàn làm việc, không hợp mobile. |
| **Staff Mobile** | Staff | App mobile/tablet native | Vận hành tại chỗ, di chuyển trong venue: bán vé quầy, quét QR check-in cửa, quản lý đơn F&B bếp/quầy. |
| **Admin Web** | Admin | Website (console nội bộ) | Công cụ back-office: hàng chờ kiểm duyệt, đối soát sổ cái, quản lý người dùng — bảng dữ liệu dày, cross-reference nhiều nguồn. |

## Map 73 view (theo domain nhóm ở [View-Design-Spec.md §2](View-Design-Spec.md#2-danh-sách-chi-tiết-từng-view-theo-domain-nghiệp-vụ)) sang từng surface

| Surface | Nhóm domain | Số view |
|---|---|---|
| **Audience Web** | A, B, C, D, E, F, H, I (toàn bộ trừ nhóm G) | 30 |
| **Audience Mobile (F&B)** | G | 2 |
| **Owner Web** | J, K, L, M, O, P, Q, AA + phần Owner trong N, R | 36 |
| **Staff Mobile** | S + phần Staff trong N (Vận hành Livestream), M (Bảng điều khiển Show — chỉ nút start/end) | 11 |
| **Admin Web** | T, U, V, W, X, Y, Z + phần Admin trong F (Phòng xem Livestream — nút terminate), R (edit/delete Performer) | 21 |

*4 nhóm N/R/M/F có view dùng chung nhiều surface (xem [View-Design-Spec.md §1.4](View-Design-Spec.md#14-view-dùng-chung-giữa-nhiều-role)) — mỗi surface chỉ build đúng phần UI mình cần, không phải toàn bộ view đó.*

## Stitch design brief tương ứng

Gộp đủ cả 5 vào 1 file: **[Stitch-Master-Brief.md](Stitch-Master-Brief.md)** (feed từng surface riêng cho Stitch, không đưa cả file — xem ghi chú đầu file đó). File riêng từng surface vẫn giữ để tiện đọc/sửa:

| Surface | File |
|---|---|
| Audience Web | [stitch-brief-audience-web.md](stitch-brief-audience-web.md) |
| Audience Mobile (F&B) | [stitch-brief-audience-mobile-fnb.md](stitch-brief-audience-mobile-fnb.md) |
| Owner Web | [stitch-brief-owner-web.md](stitch-brief-owner-web.md) |
| Staff Mobile | [stitch-brief-staff-mobile.md](stitch-brief-staff-mobile.md) |
| Admin Web | [stitch-brief-admin-web.md](stitch-brief-admin-web.md) |
