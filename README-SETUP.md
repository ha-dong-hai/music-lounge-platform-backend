# Hướng dẫn chạy Backend MusicLounge (dành cho dev Frontend)

Backend viết bằng .NET 8 (ASP.NET Core Web API), dùng SQL Server làm database.
Làm theo đúng thứ tự các bước dưới đây, mỗi bước đều có cách kiểm tra đã đúng chưa
trước khi sang bước tiếp theo.

---

## Bước 0 — Cài .NET 8 SDK (nếu máy chưa có)

Mở terminal (PowerShell/CMD/Terminal) gõ:

```
dotnet --version
```

Nếu hiện ra số bắt đầu bằng `8.x.x` → đã có, bỏ qua bước này.

Nếu báo lỗi "không tìm thấy lệnh" → tải và cài **.NET 8 SDK** (không phải Runtime) tại:
https://dotnet.microsoft.com/download/dotnet/8.0

Cài xong, **đóng và mở lại terminal**, gõ lại `dotnet --version` để xác nhận thấy `8.x.x`.

---

## Bước 1 — Cấu hình kết nối SQL Server của bạn

Backend không cài kèm database — bạn dùng SQL Server đã có sẵn trên máy mình.

1. Vào thư mục `src/MusicLounge.Api/`, tìm file **`appsettings.Development.Local.json.example`**.
2. **Copy** file đó thành file mới tên **`appsettings.Development.Local.json`** (bỏ đúng
   phần `.example` ở cuối, giữ nguyên phần còn lại — file phải nằm cùng thư mục với file
   `.example` gốc).
3. Mở file `appsettings.Development.Local.json` vừa tạo, sửa dòng `DefaultConnection` cho
   khớp với SQL Server thật của bạn:

   - **Nếu SQL Server đăng nhập bằng tài khoản Windows** (phổ biến nhất khi cài SQL Server
     Developer/Express trên máy cá nhân):
     ```
     Server=localhost;Database=SU26SE039;Trusted_Connection=True;TrustServerCertificate=True;
     ```
     (đây cũng chính là giá trị mặc định sẵn trong file — nếu dùng kiểu này thường
     **không cần sửa gì thêm**)

   - **Nếu SQL Server đăng nhập bằng username/password riêng** (SQL Authentication):
     ```
     Server=localhost;Database=SU26SE039;User Id=sa;Password=MẬT_KHẨU_CỦA_BẠN;TrustServerCertificate=True;
     ```

   - **Nếu SQL Server của bạn là Named Instance** (ví dụ cài kèm Visual Studio thường ra
     tên `SQLEXPRESS`) — mở "SQL Server Configuration Manager" hoặc SSMS để xem đúng tên
     instance, rồi sửa `Server=localhost` thành `Server=localhost\SQLEXPRESS` (giữ 1 dấu
     `\` khi gõ trực tiếp, không phải 2 dấu như trong ví dụ ở file — file JSON cần 2 dấu
     `\\` nhưng khi bạn gõ vào SQL Server Management Studio/connection string thật thì chỉ
     cần 1 dấu).

   Không biết chắc server mình tên gì? Mở **SQL Server Management Studio (SSMS)**, xem ở màn
   hình "Connect to Server" — tên hiện ở ô "Server name" chính là giá trị cần điền vào
   `Server=...`.

4. **Không cần sửa** phần `Jwt.Secret` — giá trị có sẵn trong file dùng được ngay, chỉ chạy
   trên máy bạn, không ảnh hưởng ai khác.

5. Phần `VnPay` và `Mux` để trống là bình thường — chỉ ảnh hưởng tính năng thanh toán vé
   (VNPay) và livestream video (Mux). Các màn hình khác (đăng nhập, danh sách phòng trà,
   đặt vé cơ bản...) vẫn chạy bình thường. Nếu sau này cần test 2 tính năng đó, hỏi lại
   người đã gửi bạn source này để xin key riêng.

---

## Bước 2 — Tạo cấu trúc database (chạy 1 lần đầu tiên)

Backend dùng EF Core Migrations để tự tạo toàn bộ bảng/cột trong SQL Server — bạn
**không cần tự tạo bảng thủ công**.

1. Cài công cụ dòng lệnh EF Core (chỉ cần làm 1 lần trên máy):
   ```
   dotnet tool install --global dotnet-ef
   ```
   Nếu terminal báo đã cài rồi ("already installed") thì bỏ qua, không sao cả.

2. Đứng ở thư mục gốc (thư mục chứa file `MusicLounge.sln`), chạy:
   ```
   dotnet ef database update --project src/MusicLounge.Infrastructure --startup-project src/MusicLounge.Api
   ```
   Lệnh này sẽ tự tạo database `SU26SE039` (nếu chưa có) và tạo toàn bộ bảng theo đúng
   cấu trúc mới nhất. Chạy xong sẽ thấy dòng cuối kiểu `Done.` — không có dòng `error`
   màu đỏ là thành công.

   **Lỗi thường gặp:** nếu báo không kết nối được SQL Server → kiểm tra lại bước 1
   (đúng connection string chưa), và kiểm tra SQL Server đang thực sự chạy (mở
   "Services" trên Windows, tìm dịch vụ tên `SQL Server (MSSQLSERVER)` hoặc
   `SQL Server (SQLEXPRESS)`, trạng thái phải là "Running").

---

## Bước 3 — Chạy backend

Vẫn ở thư mục gốc, chạy:

```
dotnet run --project src/MusicLounge.Api
```

Đợi vài giây, thấy dòng:
```
Now listening on: http://localhost:5289
Application started. Press Ctrl+C to shut down.
```
→ backend đã chạy thành công, **để terminal này chạy nền, đừng đóng** (đóng terminal là
tắt backend luôn).

Muốn dừng backend: bấm `Ctrl+C` trong terminal đó.

---

## Bước 4 — Kiểm tra backend chạy đúng chưa

Mở trình duyệt, vào:
```
http://localhost:5289/swagger
```
Nếu thấy trang Swagger UI liệt kê đầy đủ danh sách API (Auth, Lounges, Tickets...) →
backend đã chạy đúng và sẵn sàng nhận request.

Kiểm tra nhanh hơn (không cần mở trình duyệt): mở terminal MỚI (không phải terminal
đang chạy backend), gõ:
```
curl http://localhost:5289/health
```
Thấy trả về `200`/nội dung OK → backend sống khoẻ.

---

## Bước 5 — Chạy Frontend trỏ vào backend này

**Tin vui: không cần sửa gì ở phía Frontend cả.** File `frontend/vite.config.js` đã
được cấu hình sẵn để tự động chuyển tiếp mọi request `/api`, `/hubs`, `/uploads` từ
Frontend sang `http://localhost:5289` — đúng y hệt cổng backend chạy ở Bước 3.

Chỉ cần đảm bảo **backend đang chạy** (Bước 3) trước khi chạy:
```
cd frontend
npm run dev
```

Frontend sẽ tự gọi được API thật ngay, không cần thêm file `.env` hay sửa code nào.

---

## Bước 6 — Tạo dữ liệu mẫu qua Swagger để test giao diện

Database mới migrate xong **trống trơn** — chưa có phòng trà, chương trình, vé nào. Làm đúng theo
thứ tự dưới đây (dán JSON có sẵn vào Swagger, không cần viết code) để có đủ dữ liệu: 1 tài khoản
Owner, 1 phòng trà, 1 chương trình **đã lên trang chủ**, 1 hạng vé, 1 vé đã bán — đủ cho FE dựng
màn Home/Detail/Booking mà không phải tự tạo dữ liệu.

Mở `http://localhost:5289/swagger`. Mỗi bước dưới đây đều làm theo mẫu: bấm đúng endpoint (nhóm
tag bên trái) → **Try it out** → dán JSON mẫu vào ô Body → **Execute**.

### 6.1 — Đăng ký tài khoản Owner

`POST /api/v1/auth/register`
```json
{
  "email": "owner-demo@test.com",
  "password": "P@ssword123",
  "fullName": "Chủ Phòng Trà Demo",
  "phone": null,
  "role": "Owner"
}
```
⚠️ Field `role` tự đăng ký **chỉ nhận `"Audience"` hoặc `"Owner"`** — không tự tạo được tài khoản
Staff/Admin qua API này (xem mục Ràng buộc bên dưới).

### 6.2 — Lấy mã OTP (không cần email thật)

Mở lại **cửa sổ terminal đang chạy `dotnet run`** (Bước 3) — mã 6 số được in thẳng ra đó, dạng:
```
Ma xac thuc cho owner-demo@test.com: 123456
```
Copy đúng 6 số này.

### 6.3 — Xác thực email, lấy token, bấm Authorize

`POST /api/v1/auth/verify-email`
```json
{ "email": "owner-demo@test.com", "code": "123456" }
```
(thay `123456` bằng mã thật ở Bước 6.2). Response trả `data.token` — copy nguyên chuỗi đó, bấm nút
🔒 **Authorize** ở góc trên-phải trang Swagger, dán vào (không gõ chữ `Bearer ` phía trước), bấm
**Authorize** → **Close**. Từ giờ mọi request Try it out tự động gắn token này.

### 6.4 — Tạo tài khoản Admin (chỉ cần làm 1 lần cho cả team)

Hệ thống **không có sẵn tài khoản Admin** sau khi migrate, và tự đăng ký (Bước 6.1) chặn không cho
chọn role Admin — bắt buộc tạo thủ công 1 lần:

1. Đăng ký thêm 1 tài khoản khác (email khác) như Bước 6.1-6.3 (role Audience hoặc Owner đều được,
   không quan trọng vì sắp đổi bằng SQL).
2. Mở **SQL Server Management Studio (SSMS)**, connect vào database `SU26SE039` (như Bước 1), chạy:
   ```sql
   UPDATE users SET Role = 'Admin' WHERE Email = 'email-vua-dang-ky@test.com';
   ```
3. Gọi lại `POST /api/v1/auth/login` bằng đúng email/password đó để lấy **token mới** — token cũ
   lấy trước khi sửa DB vẫn mang role cũ (role được đóng gói cứng vào token lúc đăng nhập, không tự
   cập nhật). Giữ lại token Admin này — các bước 6.5 và 6.12 cần bấm Authorize đổi sang token này,
   rồi đổi lại token Owner (Bước 6.3) cho các bước còn lại.

### 6.5 — (Admin) Tạo gói subscription

Bấm Authorize, dán **token Admin** (Bước 6.4).

`POST /api/v1/subscriptions/packages`
```json
{
  "name": "Gói Pro Demo",
  "description": "Gói test kết nối FE",
  "price": 250000,
  "billingCycle": "Monthly",
  "maxTicketsPerEvent": 100,
  "hasAiPoster": true
}
```
Ghi lại `data.data` (chính là `packageId`) trong response.

Bấm Authorize lại, đổi về **token Owner** (Bước 6.3) trước khi làm tiếp.

### 6.6 — (Owner) Tạo phòng trà

`POST /api/v1/lounges`
```json
{
  "name": "Phòng Trà Demo",
  "description": "Không gian acoustic ấm cúng",
  "atmosphereId": null,
  "street": "1 Nguyễn Huệ",
  "ward": "Bến Nghé",
  "district": "Quận 1",
  "city": "TP.HCM",
  "latitude": 10.776,
  "longitude": 106.700
}
```
Ghi lại `data.data` (`loungeId`).

### 6.7 — (Owner) Kích hoạt subscription (bắt buộc trước khi tạo được show)

⚠️ **Đây là bước duy nhất cần lưu ý kỹ**: `POST /subscriptions/subscribe` chỉ tạo 1 giao dịch thanh
toán ở trạng thái chờ — subscription chỉ thật sự "Active" sau khi có **callback VNPay với chữ ký
hợp lệ**. Có 2 cách:

**Cách A — có VNPay sandbox key thật** (xin từ người quản lý source, theo đúng ghi chú ở Bước 1.5):
1. `POST /api/v1/subscriptions/subscribe` với body `{ "packageId": <packageId Bước 6.5> }`, ghi lại
   `data.orderId` và `data.amount`.
2. Mở `data.paymentUrl` trả về, thanh toán trên trang sandbox VNPay bằng thẻ test VNPay cấp.
3. VNPay tự gọi lại `GET /api/v1/subscriptions/vnpay-return` với chữ ký hợp lệ — không cần FE làm
   gì thêm.

**Cách B — chưa có VNPay key (dự phòng, dùng SQL trực tiếp)**:
1. Vẫn gọi `POST /api/v1/subscriptions/subscribe` như trên để có `orderId` (không bắt buộc, chỉ để
   có log giao dịch — có thể bỏ qua nếu muốn nhanh).
2. Mở SSMS, chạy (đổi `owner-demo@test.com` và giá trị `PackageId` cho khớp dữ liệu của bạn):
   ```sql
   INSERT INTO owner_subscriptions
     (OwnerId, PackageId, StartedAt, ExpiresAt, Status, AutoRenew,
      MaxTicketsPerEventSnapshot, HasAiPosterSnapshot)
   SELECT u.Id, <packageId Bước 6.5>, SYSDATETIMEOFFSET(), DATEADD(MONTH, 1, SYSDATETIMEOFFSET()),
          'Active', 0, 100, 1
   FROM users u WHERE u.Email = 'owner-demo@test.com';
   ```
   ⚠️ Câu SQL này **chưa chạy thử được trên SQL Server thật** (môi trường soạn tài liệu này không có
   SQL Server) — tên bảng/cột đã đối chiếu đúng với entity/config trong code, nhưng nếu báo lỗi khi
   chạy, nhờ backend dev kiểm tra lại tên cột trước khi báo là bug.
3. Kiểm tra lại: `GET /api/v1/subscriptions/my` phải thấy `"status":"Active"`.

### 6.8 — (Owner) Tạo chương trình ca nhạc (Draft)

`POST /api/v1/lounge-shows`
```json
{
  "loungeId": 1,
  "name": "Đêm Nhạc Acoustic Demo",
  "description": "Chương trình test kết nối FE",
  "format": "Offline",
  "scheduledStart": "2026-09-01T19:00:00+07:00",
  "scheduledEnd": null,
  "categoryId": null,
  "offlineQuota": 100,
  "onlineQuota": null,
  "genreIds": [],
  "performances": []
}
```
Thay `loungeId` bằng giá trị thật ở Bước 6.6. `scheduledStart` **phải cách ngày bạn test ít nhất 7
ngày làm việc** (dùng dư ra 14 ngày lịch cho chắc — xem mục Ràng buộc). Ghi lại `data.data`
(`showId`).

### 6.9 — (Owner) Thêm hạng vé

`POST /api/v1/ticket-tiers`
```json
{
  "showId": 1,
  "name": "Vé Thường",
  "description": null,
  "accessType": "Physical",
  "zoneId": null,
  "totalCapacity": 100,
  "prices": [
    {
      "name": "Giá chuẩn",
      "price": 150000,
      "quota": 50,
      "purchaseChannel": "Both",
      "saleStart": "2026-08-07T00:00:00+07:00",
      "saleEnd": "2026-08-31T23:59:59+07:00"
    }
  ]
}
```
Thay `showId` bằng giá trị ở Bước 6.8, chỉnh `saleStart`/`saleEnd` bao quanh ngày bạn thực sự test.

### 6.10 — (Owner) Khai văn bản chấp thuận biểu diễn

Theo NĐ 144/2020 Điều 10, bắt buộc phải có trước khi nộp duyệt:

`PUT /api/v1/lounge-shows/{id}/legal-approval` (điền `showId` vào `{id}` trên URL)
```json
{ "legalApprovalReference": "SoVHTT-DEMO-0001" }
```

### 6.11 — (Owner) Nộp duyệt

`POST /api/v1/lounge-shows/{id}/publish` (điền `showId`, **không cần body**) → show chuyển
**Pending** — lưu ý: **CHƯA hiện trên trang chủ** ở bước này.

### 6.12 — (Admin) Duyệt show

Bấm Authorize, đổi sang **token Admin** (Bước 6.4).

`POST /api/v1/moderations/shows/{id}/review` (điền `showId`)
```json
{ "decision": "Approved", "reviewNote": "OK" }
```

### 6.13 — Kiểm tra: show đã lên trang chủ chưa

`GET /api/v1/lounge-shows` (không cần token — public). Show vừa duyệt phải xuất hiện trong danh
sách trả về — **đây chính là API FE dùng để đổ dữ liệu trang chủ**.

### 6.14 — (Owner) Gán nhân viên + (Staff) bán thử 1 vé

1. Đăng ký thêm **1 tài khoản riêng** làm Staff (như Bước 6.1-6.3, role `"Audience"` — Owner sẽ
   nâng thành Staff ở bước tiếp theo, không tự đăng ký role Staff được). Response `verify-email`
   trả `data.userId` — ghi lại số này.
2. Bấm Authorize, đổi lại **token Owner**.
3. `POST /api/v1/lounges/{id}/staff` (điền `loungeId` Bước 6.6):
   ```json
   { "userId": 3 }
   ```
   (thay `3` bằng `userId` thật lấy ở bước 1).

Lấy `priceId` vừa tạo ở Bước 6.9 bằng `GET /api/v1/ticket-tiers?showId={showId}` (xem field
`data[0].prices[0].id`). Bấm Authorize, đổi sang token của tài khoản vừa gán Staff, gọi:

`POST /api/v1/tickets/walk-in`
```json
{ "priceId": 1, "quantity": 1 }
```

Xong bước này: có đủ 1 Owner, 1 phòng trà, 1 show Published, 1 hạng vé, 1 vé Confirmed — FE có dữ
liệu thật để dựng và kiểm tra toàn bộ màn hình chính.

---

## Ràng buộc & lưu ý quan trọng FE cần nắm

### Xác thực

- Token gắn ở header `Authorization: Bearer <token>`, hết hạn (`expiresAt` trong response login) thì
  phải đăng nhập lại — không tự refresh.
- Tự đăng ký (`POST /auth/register`) chỉ nhận `role` là `"Audience"` hoặc `"Owner"`. Staff do Owner
  tự gán qua `POST /lounges/{id}/staff` (không tự đăng ký được); Admin phải tạo thủ công qua SQL
  (Bước 6.4) — không có API nào tạo được Admin.
- Response login/verify-email trả sẵn field `role` — FE nên dùng đúng field này để định tuyến màn
  hình theo vai trò, không tự suy luận.

### Điều kiện để tạo được chương trình (show)

- Owner phải sở hữu **ít nhất 1 phòng trà**.
- Owner phải có **subscription đang Active tại thời điểm tạo show** — thiếu thì API trả 422 kèm
  message rõ ràng. Nên chặn ở UI (disable nút "Tạo chương trình", hiện CTA mua gói) thay vì để user
  bấm rồi mới hiện lỗi.

### Vòng đời show — "Nộp duyệt" không có nghĩa là hiển thị ngay

```
Draft → (Owner nộp duyệt) → Pending → (Admin duyệt) → Published — MỚI hiện GET /lounge-shows
                                     └→ (Admin từ chối) → về lại Draft
```
Điều kiện để nộp duyệt (`/publish`) không bị lỗi: đã có ≥1 hạng vé, đã khai `legal-approval`, ngày
diễn cách hiện tại ≥7 ngày làm việc, và nếu show Online/Hybrid hoặc có hạng vé Livestream thì phải
tạo Livestream cho show trước. FE nên đặt tên nút đúng bản chất — **"Nộp duyệt"**, không phải
"Xuất bản/Publish" — để Owner không hiểu lầm là show lên ngay.

### Format lỗi API — đã thống nhất 1 kiểu duy nhất

```json
{ "success": false, "message": "Mô tả lỗi", "errors": { "TenField": ["chi tiết"] } }
```
(`errors` có thể là `null` nếu lỗi không gắn với field cụ thể). FE chỉ cần 1 interceptor chung dựa
vào field `success` cho toàn bộ API, không cần xử lý nhiều dạng response lỗi khác nhau.

### Upload ảnh / model 3D

- `POST /uploads/images`, `POST /uploads/models` trả về **URL tương đối** (vd `/uploads/xxx.jpg`).
- Nếu FE chạy qua Vite dev proxy (Bước 5) thì dùng thẳng được luôn, không cần nối thêm domain.
- Giới hạn: ảnh ≤5MB (jpg/jpeg/png/webp/gif), model 3D ≤30MB (chỉ nhận `.glb`).

### Giới hạn tần suất gọi API (rate limit)

- Toàn bộ API: tối đa 100 request/phút/IP.
- Riêng nhóm `/auth/*` (login/register/google): tối đa 10 request/phút/IP — test đăng nhập lặp lại
  nhanh lúc dev dễ dính lỗi 429, đợi khoảng 1 phút rồi thử lại.
- Response 429 có kèm body JSON (`{success:false, message,...}`) và header `Retry-After` (số giây
  cần đợi) — FE có thể đọc header này để tự động chờ đúng thời gian thay vì đoán.

### Realtime (chat trong livestream)

Kết nối SignalR tới `/hubs/livestream`, gắn token qua **query string** `?access_token=<token>`
(không phải header `Authorization`) — đây là giới hạn kỹ thuật của WebSocket, không phải thiếu sót,
cần cấu hình riêng `accessTokenFactory` cho client SignalR, không dùng chung interceptor REST được.

### Phân trang

Mọi endpoint danh sách nhận `page`, `pageSize` qua query string — `pageSize` tự động giới hạn tối đa
100 dù FE truyền cao hơn, `page` < 1 tự động về 1. Không cần FE tự validate 2 field này trước khi gọi.

### CORS (chỉ liên quan nếu FE gọi thẳng API, không qua Vite proxy)

Origin whitelist mặc định chỉ `http://localhost:5173`. Nếu FE chạy port/domain khác mà gọi thẳng API
(không qua proxy Bước 5) sẽ bị chặn CORS ngay ở trình duyệt — báo lại người quản lý source để thêm
origin vào `Cors:AllowedOrigins` trong `appsettings.Development.json`.

---

## Tóm tắt quy trình mỗi lần muốn chạy lại (sau lần setup đầu tiên)

Từ lần 2 trở đi, không cần lặp lại Bước 0-2 (chỉ làm 1 lần đầu). Mỗi lần muốn chạy:

1. Đảm bảo SQL Server đang chạy (Services → SQL Server... → Running).
2. Terminal 1: `dotnet run --project src/MusicLounge.Api`
3. Terminal 2: `cd frontend && npm run dev`

---

## Troubleshooting

**"Port 5289 đã được sử dụng" / lỗi bind port khi chạy Bước 3**
→ Có 1 lần chạy `dotnet run` khác chưa tắt hẳn. Đóng hết terminal cũ liên quan tới
backend, hoặc khởi động lại máy nếu không tìm được tiến trình đang giữ port.

**Backend chạy được nhưng gọi API từ Frontend bị lỗi CORS**
→ Kiểm tra Frontend có đang chạy đúng cổng `5173` không (`npm run dev` mặc định là
5173). Nếu Frontend chạy cổng khác, báo lại người gửi source này để họ thêm cổng đó
vào `Cors:AllowedOrigins` trong `appsettings.Development.json`.

**`dotnet ef database update` báo lỗi "Login failed"**
→ Sai username/password hoặc chưa bật đúng kiểu xác thực (Windows Auth vs SQL Auth) —
xem lại Bước 1.

**Muốn xem/sửa dữ liệu trực tiếp trong database**
→ Mở SQL Server Management Studio (SSMS), connect vào server như Bước 1, database tên
`SU26SE039`.

**Database đang trống, chưa có dữ liệu mẫu (phòng trà, show...) để test giao diện**
→ Bình thường — migration chỉ tạo cấu trúc bảng + vài dữ liệu tham chiếu cơ bản
(danh sách thể loại nhạc, cấu hình hệ thống...), chưa có phòng trà/show/user mẫu.
Làm theo **Bước 6** ở trên (dán JSON có sẵn vào Swagger) để tạo đủ 1 bộ dữ liệu mẫu.

**Tạo show xong mà không thấy trên `GET /lounge-shows`**
→ Đúng thiết kế, không phải bug — show phải qua đủ Draft → Pending (Owner nộp duyệt) →
Published (Admin duyệt) mới hiện công khai. Xem mục "Vòng đời show" và Bước 6.11-6.13.

**Gọi `POST /subscriptions/subscribe` xong nhưng `GET /subscriptions/my` vẫn không thấy Active**
→ Subscription chỉ kích hoạt sau khi có callback VNPay với chữ ký hợp lệ — chưa có VNPay
sandbox key thì bước này sẽ không tự chạy được. Xem Bước 6.7 (Cách A/B).

**Không tạo được tài khoản Admin để test duyệt show / tạo gói subscription**
→ Đúng, không có API nào tự đăng ký ra Admin — phải tạo thủ công 1 lần qua SQL. Xem Bước 6.4.
