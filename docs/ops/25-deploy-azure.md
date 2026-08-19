# Hướng dẫn Deploy Backend MusicLounge lên Azure App Service

Tài liệu này hướng dẫn deploy **MusicLounge.Api** (đúng theo `diagrams/src/deployment.puml` —
node "App Service (Linux)") lên Azure thật, dùng được ngay cho demo bảo vệ đồ án. Phạm vi:
**chỉ backend .NET** (API + SignalR hub + Hangfire worker chạy chung trong 1 process, đúng như
kiến trúc hiện tại). Panorama-stitcher (Python), frontend, Blob Storage/CDN, Key Vault,
Application Insights **không nằm trong phạm vi bước này** — xem mục "Ngoài phạm vi" ở cuối.

Repo hiện **chưa có sẵn** Dockerfile hay IaC (Bicep/Terraform) cho phần API — tài liệu này dùng
cách đơn giản nhất phù hợp quy mô đồ án: **App Service Linux, deploy code trực tiếp (không cần
Docker)**, tạo resource bằng Azure CLI (copy-paste được, không cần chụp màn hình từng bước).

---

## Bước 0 — Kiểm tra / tạo tài khoản Azure

Bạn trả lời "không biết" khi được hỏi đã có subscription chưa — kiểm tra như sau:

1. Mở https://portal.azure.com, đăng nhập bằng tài khoản Microsoft/email trường của bạn.
2. Nếu vào thẳng được Dashboard (không bị chặn "Start free") → **đã có subscription**, bỏ qua
   phần đăng ký, sang Bước 1.
3. Nếu chưa có: vào https://azure.microsoft.com/free/students (dùng email `.edu`/email trường
   nếu có — được **$100 credit + nhiều dịch vụ free 12 tháng**, không cần thẻ tín dụng). Nếu
   không có email trường, dùng https://azure.microsoft.com/free (cần thẻ, có $200 credit 30
   ngày).
4. Sau khi có subscription, cài **Azure CLI** trên máy Windows:
   ```
   winget install Microsoft.AzureCLI
   ```
   Đóng mở lại terminal, kiểm tra:
   ```
   az --version
   ```
   Thấy số phiên bản (không phải "az không phải lệnh") → đã cài xong.
5. Đăng nhập CLI:
   ```
   az login
   ```
   Trình duyệt tự mở để bạn chọn tài khoản — chọn đúng tài khoản Bước 1-3. Chạy xong terminal in
   ra danh sách subscription dạng JSON, có `"isDefault": true` ở 1 dòng — đó là subscription sẽ
   dùng cho các bước sau. Nếu có **nhiều hơn 1 subscription** và dòng `isDefault:true` không phải
   cái bạn muốn, chạy `az account set --subscription "<tên hoặc id subscription>"`.

**Cảnh báo chi phí** (đọc trước khi tạo resource ở Bước 2): các resource dưới đây **không miễn
phí vĩnh viễn**. Cấu hình khuyến nghị (App Service B1 Linux + SQL Database **Standard S0**) tốn
**~29 USD/tháng chạy 24/7** theo giá niêm yết region `eastasia` — là region các resource đang chạy
thật, KHÔNG phải `southeastasia` trong lệnh mẫu bên dưới (subscription Azure for Students này bị
chính sách capacity nội bộ của Azure từ chối `southeastasia`; gặp lỗi region ở Bước 1 thì thử
`eastasia`). Chi tiết: B1 Linux $0,02/giờ = $14,60/tháng; S0 $0,4839/ngày = $14,72/tháng — gói
Azure for Students ($100 credit) dùng được ~3,4 tháng.

**KHÔNG dùng SQL Serverless cho cấu hình này** — xem Bước 2.4 để biết lý do (đã trả giá bằng tiền
thật một lần). Muốn tiết kiệm thêm: xoá resource group sau khi bảo vệ xong (Bước "Dọn dẹp" cuối
bài). Lưu ý App Service Plan **tính tiền theo giờ kể cả khi Web App đã `stop`** — chỉ xoá hoặc hạ
SKU của plan mới thật sự ngừng tính tiền.

---

## Bước 1 — Tạo Resource Group

Nhóm chứa toàn bộ resource của đồ án, xoá 1 lần là sạch hết khi không cần nữa:

```
az group create --name rg-musiclounge --location southeastasia
```

`southeastasia` (Singapore) là vùng gần Việt Nam nhất hiện có đủ dịch vụ cần dùng — độ trễ thấp
hơn các vùng US/Europe.

---

## Bước 2 — Tạo Azure SQL Database

1. Tạo logical server (khác khái niệm "server" của SQL Server cài local — đây là 1 endpoint quản
   lý, database thật nằm bên trong):
   ```
   az sql server create --name sql-musiclounge --resource-group rg-musiclounge --location southeastasia --admin-user musicloungeadmin --admin-password "<MẬT_KHẨU_MẠNH_CỦA_BẠN>"
   ```
   Đổi `sql-musiclounge` nếu tên bị báo trùng (tên server SQL Azure phải **duy nhất toàn cầu**,
   thử thêm hậu tố như `sql-musiclounge-<tên bạn>`). `<MẬT_KHẨU_MẠNH_CỦA_BẠN>` phải ≥8 ký tự, đủ 3
   trong 4 loại (hoa/thường/số/ký tự đặc biệt) — **ghi lại mật khẩu này**, dùng lại ở Bước 4 và 5.

2. Cho phép Azure services (App Service) kết nối vào SQL server này:
   ```
   az sql server firewall-rule create --resource-group rg-musiclounge --server sql-musiclounge --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
   ```
   Rule `0.0.0.0-0.0.0.0` là cú pháp đặc biệt của Azure = "cho phép mọi resource Azure khác (App
   Service, Container Apps...) trong cùng subscription" — không phải mở ra cho cả Internet.

3. Cho phép **chính máy bạn** kết nối (cần cho Bước 4 — chạy migration từ máy local):
   ```
   az sql server firewall-rule create --resource-group rg-musiclounge --server sql-musiclounge --name AllowMyIP --start-ip-address <IP_CUA_BAN> --end-ip-address <IP_CUA_BAN>
   ```
   Lấy `<IP_CUA_BAN>` bằng cách mở https://whatismyipaddress.com, hoặc chạy
   `curl https://api.ipify.org` trong terminal. IP nhà mạng Việt Nam thường **đổi theo phiên**
   (không cố định) — nếu vài ngày sau chạy migration lại mà bị lỗi kết nối, chạy lại lệnh này với
   IP mới.

4. Tạo database, tier **Standard S0** (10 DTU, 250 GB, giá cố định $0,4839/ngày):
   ```
   az sql db create --resource-group rg-musiclounge --server sql-musiclounge --name SU26SE039 --edition Standard --service-objective S0 --max-size 250GB
   ```
   Tier DTU **không có auto-pause** → không bao giờ có cold-start 30-60 giây, giá cố định, không
   phụ thuộc app chạy nhiều hay ít.

   **Vì sao KHÔNG dùng Serverless auto-pause** (bài học đã trả giá bằng tiền thật, 2026-08-18):
   database ban đầu tạo bằng `--edition GeneralPurpose --compute-model Serverless --auto-pause-delay 60`
   với kỳ vọng "không ai dùng thì không tính tiền". Thực tế auto-pause **không bao giờ kích hoạt**:
   backend chạy Hangfire trên SQL Server storage (`DependencyInjection.cs`) + job `release-expired-holds`
   chạy **mỗi phút**, nên chừng nào Web App còn bật (Always On = true, Bước 3.3) thì database luôn có
   truy vấn → không bao giờ im lặng đủ lâu để pause. Kết quả: bị tính tiền serverless 24/7 ở mức
   ~0,678 vCore × $0,777419/vCore-giờ (giá `eastasia`) = **~$0,53/giờ ≈ $12,65/ngày ≈ $385/tháng**,
   tức **đắt gấp ~26 lần S0** cho đúng một database 33 MB chạy không tải (`cpu_percent` cao nhất
   chỉ 1%).

   Cách kiểm chứng nếu nghi ngờ database serverless không pause — xem metric `app_cpu_billed`, nếu
   thấy ~2440 vCore-giây **mỗi giờ liên tục** nghĩa là đang bị tính tiền 24/7 (`MSYS_NO_PATHCONV=1`
   là để Git Bash không bẻ resource ID thành đường dẫn Windows):
   ```
   MSYS_NO_PATHCONV=1 az monitor metrics list --resource "/subscriptions/<SUB_ID>/resourceGroups/rg-musiclounge/providers/Microsoft.Sql/servers/sql-musiclounge/databases/SU26SE039" --metric app_cpu_billed --interval PT1H --aggregation Total --start-time <ISO_TIME>
   ```
   Quy tắc rút ra: **serverless chỉ rẻ hơn S0 nếu database online dưới ~28 giờ/tháng**. Với backend
   Always On (730 giờ/tháng) thì tier giá cố định luôn thắng — không cần tính toán gì thêm.

   Nếu database đang là serverless, đổi sang S0 tại chỗ (online, giữ nguyên dữ liệu, chỉ gián đoạn
   vài giây; bắt buộc kèm `--max-size` vì Standard không nhận max size 32 GB của serverless):
   ```
   az sql db update -g rg-musiclounge -s sql-musiclounge -n SU26SE039 --edition Standard --service-objective S0 --max-size 250GB
   ```

**Kiểm tra**: chạy `az sql db show --resource-group rg-musiclounge --server sql-musiclounge --name SU26SE039 --query status` phải thấy `"Online"`. Trường thuộc tính `status`/`resumedDate` do ARM trả về **có thể bị cũ** (đã gặp: báo `Paused` suốt 13 tiếng trong khi metric cho thấy database vẫn online liên tục) — khi nghi ngờ, tin metric `app_cpu_billed` hơn trường `status`.

---

## Bước 3 — Tạo App Service (Linux, .NET 8)

1. Tạo App Service Plan — tier **B1 (Basic)**, **không dùng F1 (Free)**: F1 không hỗ trợ "Always
   On" (app tự ngủ sau 20 phút không có request), khiến Hangfire recurring jobs (ledger integrity
   job chạy hằng ngày, dọn login-failure log...) không chạy được đều đặn:
   ```
   az appservice plan create --name plan-musiclounge --resource-group rg-musiclounge --location southeastasia --sku B1 --is-linux
   ```

2. Tạo Web App, runtime .NET 8:
   ```
   az webapp create --name musiclounge-api --resource-group rg-musiclounge --plan plan-musiclounge --runtime "DOTNETCORE:8.0"
   ```
   Đổi `musiclounge-api` nếu báo trùng tên (tên App Service cũng phải **duy nhất toàn cầu**, vì nó
   trở thành `<tên>.azurewebsites.net`). **Ghi lại tên cuối cùng bạn dùng** — cần cho mọi bước sau.

3. Bật Always On (cần vì tier B1 trở lên mới có tuỳ chọn này, mặc định tắt):
   ```
   az webapp config set --name musiclounge-api --resource-group rg-musiclounge --always-on true
   ```

4. Bật WebSockets (SignalR hub `/hubs/livestream`, `/hubs/public-donations` cần cái này):
   ```
   az webapp config set --name musiclounge-api --resource-group rg-musiclounge --web-sockets-enabled true
   ```

**Kiểm tra**: mở `https://musiclounge-api.azurewebsites.net` (đổi đúng tên bạn dùng) — thấy trang
mặc định "Your app is up and running" của Azure (chưa phải app thật, code chưa deploy) → App
Service đã tạo đúng, có HTTPS free tự động (không cần tự cấu hình chứng chỉ).

---

## Bước 4 — Cấu hình Application Settings (biến môi trường)

Đây là bước **quan trọng nhất, dễ thiếu nhất**. `Program.cs` cố tình **crash ngay lúc khởi động**
(fail-fast, không chạy ngầm với giá trị rỗng nguy hiểm) nếu thiếu 1 trong 2 nhóm sau:

- `Jwt:Secret` — rỗng hoặc ngắn hơn 32 byte → app không start.
- `Business:PaymentSuccessUrl`, `Business:PaymentFailedUrl`, `Business:PasswordResetUrl` — **3 key
  này KHÔNG có sẵn trong `appsettings.json`** đã commit (chỉ có ở
  `appsettings.Development.json`, không tồn tại bản Production) → **nếu bạn không tự set 3 key
  này ở Application Settings, app sẽ crash ngay khi mở, log ra
  `InvalidOperationException: Business:{PaymentSuccessUrl, ...} is missing`**.

App Service Application Settings tương đương biến môi trường — ASP.NET Core tự map dấu `__` (2
gạch dưới) thành cấp con của JSON config (`Business:PaymentSuccessUrl` ↔ `Business__PaymentSuccessUrl`).

1. Sinh `Jwt:Secret` ngẫu nhiên ≥32 byte (PowerShell):
   ```powershell
   $rng = [System.Security.Cryptography.RNGCryptoServiceProvider]::new()
   $bytes = New-Object byte[] 48
   $rng.GetBytes($bytes)
   [Convert]::ToBase64String($bytes)
   ```
   Copy chuỗi in ra — đây là secret ký JWT, **khác hoàn toàn** secret dùng ở local dev.

2. Lấy connection string Azure SQL:
   ```
   az sql db show-connection-string --server sql-musiclounge --name SU26SE039 --client ado.net
   ```
   Kết quả có dạng
   `Server=tcp:sql-musiclounge.database.windows.net,1433;Initial Catalog=SU26SE039;...`
   — thay `<username>`/`<password>` trong chuỗi bằng `musicloungeadmin` và mật khẩu Bước 2.1.

3. Set toàn bộ Application Settings **bắt buộc** (đổi `musiclounge-api` đúng tên App Service của
   bạn, đổi giá trị placeholder cho đúng):
   ```
   az webapp config appsettings set --name musiclounge-api --resource-group rg-musiclounge --settings ^
     ASPNETCORE_ENVIRONMENT="Production" ^
     ConnectionStrings__DefaultConnection="Server=tcp:sql-musiclounge.database.windows.net,1433;Initial Catalog=SU26SE039;Persist Security Info=False;User ID=musicloungeadmin;Password=<MẬT_KHẨU_BƯỚC_2>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" ^
     Jwt__Secret="<CHUỖI_SINH_Ở_BƯỚC_4.1>" ^
     Business__TicketPaymentReturnUrl="https://musiclounge-api.azurewebsites.net/api/v1/payments/vnpay/callback" ^
     Business__DonationPaymentReturnUrl="https://musiclounge-api.azurewebsites.net/api/v1/donations/vnpay-return" ^
     Business__SubscriptionPaymentReturnUrl="https://musiclounge-api.azurewebsites.net/api/v1/subscriptions/vnpay-return" ^
     Business__PaymentSuccessUrl="https://<domain-frontend-cua-ban>/payment/success" ^
     Business__PaymentFailedUrl="https://<domain-frontend-cua-ban>/payment/failed" ^
     Business__PasswordResetUrl="https://<domain-frontend-cua-ban>/reset-password"
   ```
   (`^` là ký tự nối dòng của CMD — nếu chạy trong PowerShell đổi `^` thành `` ` `` cuối mỗi dòng,
   hoặc gộp hết vào 1 dòng.) Chưa deploy frontend? Cứ điền tạm URL App Service (
   `https://musiclounge-api.azurewebsites.net/...`) để app start được — quay lại sửa 3 dòng
   `Business__Payment*`/`PasswordResetUrl` bằng lệnh trên (chạy lại, chỉ ghi đè key cần đổi) khi
   frontend đã có domain thật.

4. Set CORS cho origin frontend thật (đổi domain khi có):
   ```
   az webapp cors add --name musiclounge-api --resource-group rg-musiclounge --allowed-origins "https://<domain-frontend-cua-ban>"
   ```
   **Lưu ý**: đây là CORS **cấp Azure App Service** (khác `Cors:AllowedOrigins` trong
   `appsettings.json` mà code tự đọc) — set cả 2 nơi dễ gây nhầm lẫn. Cách chắc chắn nhất và đúng
   với code hiện tại: dùng luôn Application Settings để ghi đè `Cors:AllowedOrigins` mà code tự
   đọc (`app.UseCors("Default")` trong `Program.cs`), thay vì tính năng CORS riêng của App Service:
   ```
   az webapp config appsettings set --name musiclounge-api --resource-group rg-musiclounge --settings Cors__AllowedOrigins__0="https://<domain-frontend-cua-ban>"
   ```
   Nhiều origin (vd vừa test local vừa có frontend thật) thì thêm `Cors__AllowedOrigins__1="..."`,
   `__2` v.v.

5. (Tuỳ chọn — chỉ set nếu cần test tính năng tương ứng ngay, giống ghi chú trong
   `appsettings.Development.Local.json.example`): `VnPay__TmnCode`, `VnPay__HashSecret`,
   `Sms__ApiToken`, `Mux__TokenId`, `Mux__TokenSecret`, `Gemini__ApiKey`, `OpenAi__ApiKey`,
   `Firebase__ProjectId`, `PanoramaStitcher__ApiKey`. Bỏ trống là bình thường — các service tương
   ứng tự log warning và không crash app (khác nhóm bắt buộc ở trên).

---

## Bước 5 — Chạy EF Core Migrations lên Azure SQL

Chạy **1 lần từ máy local** của bạn (không phải từ App Service) — trỏ vào Azure SQL vừa tạo:

```
$env:ConnectionStrings__DefaultConnection = "Server=tcp:sql-musiclounge.database.windows.net,1433;Initial Catalog=SU26SE039;Persist Security Info=False;User ID=musicloungeadmin;Password=<MẬT_KHẨU_BƯỚC_2>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
dotnet ef database update --project src/MusicLounge.Infrastructure --startup-project src/MusicLounge.Api
```
(PowerShell — biến `$env:...` chỉ tồn tại trong phiên terminal hiện tại, không ảnh hưởng máy bạn
lâu dài.) Cần đã làm Bước 2.3 (mở firewall cho IP máy bạn) — nếu không sẽ báo lỗi timeout/không
kết nối được.

**Kiểm tra**: log cuối cùng in `Done.`, không có dòng đỏ `error`. Muốn chắc chắn hơn: mở SSMS,
connect `Server: sql-musiclounge.database.windows.net`, SQL Authentication, login
`musicloungeadmin` — thấy database `SU26SE039` với đầy đủ bảng (`users`, `lounges`,
`lounge_shows`...) là đúng.

---

## Bước 6 — Deploy code

### Cách A — Deploy nhanh 1 lần để kiểm tra (thủ công, từ máy local)

Dùng để test ngay xem cấu hình Bước 1-5 đã đúng chưa, trước khi setup CI/CD:

```powershell
dotnet publish src/MusicLounge.Api/MusicLounge.Api.csproj -c Release -o ./publish-tmp

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sourceDir = (Resolve-Path ./publish-tmp).Path
$zipPath = Join-Path (Get-Location) "publish.zip"
$zipStream = New-Object System.IO.FileStream($zipPath, [System.IO.FileMode]::Create)
$archive = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create)
Get-ChildItem -Path $sourceDir -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($sourceDir.Length + 1).Replace([System.IO.Path]::DirectorySeparatorChar, [char]47)
    $entry = $archive.CreateEntry($rel, [System.IO.Compression.CompressionLevel]::Optimal)
    $es = $entry.Open(); $fs = [System.IO.File]::OpenRead($_.FullName); $fs.CopyTo($es); $fs.Close(); $es.Close()
}
$archive.Dispose(); $zipStream.Close()

az webapp deploy --name musiclounge-api --resource-group rg-musiclounge --src-path ./publish.zip --type zip --clean true
```
**Không dùng `Compress-Archive`** — trên Windows PowerShell 5.1, cmdlet này ghi dấu `\` (thay vì `/`
theo chuẩn ZIP) vào tên file bên trong thư mục lồng nhau (`runtimes\win-x64\...`), khiến Kudu (chạy
Linux) không giải nén đúng cấu trúc thư mục — toàn bộ đoạn PowerShell ở trên tự dựng file zip bằng
`System.IO.Compression.ZipArchive` trực tiếp để tránh lỗi này.

**Luôn dùng `--clean true`**: `az webapp deploy --type zip` mặc định deploy kiểu "incremental" —
chỉ thêm/ghi đè file có trong gói mới, **không tự xoá file cũ không còn trong gói** (vd nếu bạn từng
lỡ deploy 1 file rồi sau đó loại nó khỏi build, file cũ vẫn nằm im trên server mãi mãi trừ khi dùng
`--clean true` để dọn sạch trước khi ghi đè). Từng gây lỗi thật: `appsettings.Development.Local.json`
sau khi bị xoá khỏi gói build (xem `MusicLounge.Api.csproj`'s `<Content Remove>`) vẫn còn sót lại
trên server hàng giờ sau, và tái phát lỗi kết nối `localhost` mỗi khi bật `ASPNETCORE_ENVIRONMENT=Development`.

Chạy xong dọn file tạm: `Remove-Item -Recurse ./publish-tmp, ./publish.zip`.

### Cách B — Tự động deploy qua GitHub Actions mỗi khi push (khuyến nghị lâu dài)

Repo đã có sẵn workflow `.github/workflows/deploy-azure.yml` (tạo cùng đợt với tài liệu này) —
chạy **thủ công** qua tab Actions (không tự chạy mỗi lần push, để bạn kiểm soát thời điểm deploy
demo). Cần cấu hình 2 việc **1 lần**:

1. Lấy publish profile của App Service:
   ```
   az webapp deployment list-publishing-profiles --name musiclounge-api --resource-group rg-musiclounge --xml
   ```
   Copy toàn bộ output XML.

2. Vào GitHub repo → **Settings → Secrets and variables → Actions → New repository secret**:
   - Name: `AZURE_WEBAPP_PUBLISH_PROFILE`, Value: dán nguyên XML Bước 6.B.1.
   - Name: `AZURE_WEBAPP_NAME`, Value: tên App Service thật của bạn (vd `musiclounge-api`).

3. Vào tab **Actions** trên GitHub → chọn workflow **"Deploy Backend to Azure"** → **Run
   workflow** → chọn branch `main` → **Run workflow**. Theo dõi log — bước cuối
   `azure/webapps-deploy` trả về URL app là deploy thành công.

Muốn deploy tự động mỗi khi merge vào `main` (không cần bấm Run workflow thủ công): mở
`.github/workflows/deploy-azure.yml`, bỏ comment dòng `# push:` / `#   branches: [main]` ở đầu
file (xem chú thích ngay trong file).

---

## Bước 7 — Kiểm tra deploy thành công

1. `https://musiclounge-api.azurewebsites.net/health` → trả `200`/nội dung OK (health check có
   kiểm tra kết nối DB thật — 200 nghĩa là app **và** Azure SQL đều sống).
2. `https://musiclounge-api.azurewebsites.net/swagger` → thấy Swagger UI đầy đủ danh sách API.
   **Lưu ý**: `Program.cs` chỉ bật Swagger khi `IsDevelopment()` — nếu set
   `ASPNETCORE_ENVIRONMENT=Production` (đúng như Bước 4.3) thì `/swagger` sẽ **404**, đây là hành
   vi cố ý (không lộ tài liệu API công khai ở production), không phải lỗi deploy. Muốn xem Swagger
   trên Azure để demo/kiểm tra nhanh: tạm set `ASPNETCORE_ENVIRONMENT=Development` (chỉ nên bật
   tạm thời lúc demo, tắt lại sau vì Development cũng tắt `UseHsts()`).
3. Test đăng ký tài khoản qua Swagger hoặc `curl`, xác nhận response đúng format
   `{success, message, errors}` như tài liệu `README-SETUP.md` Bước 6.
4. Xem log realtime nếu có lỗi:
   ```
   az webapp log tail --name musiclounge-api --resource-group rg-musiclounge
   ```

---

## Ngoài phạm vi tài liệu này (đọc để biết giới hạn, không phải việc cần làm ngay)

- **Upload ảnh/model 3D lưu ổ đĩa cục bộ (`wwwroot/uploads`), không phải Blob Storage.** Sơ đồ
  kiến trúc (`diagrams/src/deployment.puml`) có vẽ "Blob Storage + CDN" nhưng code hiện tại
  (`UploadsController`) **chưa** tích hợp — vẫn ghi vào ổ đĩa của App Service. Với 1 instance
  (không scale-out) dữ liệu này **tồn tại được giữa các lần restart bình thường**, nhưng **có thể
  bị xoá khi deploy code mới** tuỳ cách deploy (zip deploy ở Bước 6 ghi đè toàn bộ thư mục ứng
  dụng, bao gồm `wwwroot`). Với quy mô demo đồ án chấp nhận được — nếu cần bền vững thật, cần thêm
  code tích hợp Azure Blob Storage (việc code, không phải việc hạ tầng, ngoài phạm vi hướng dẫn
  deploy này).
- **Log ghi file (`logs/musiclounge-*.log`) cũng nằm trên ổ đĩa App Service** — dùng
  `az webapp log tail` (Bước 7.4) hoặc Kudu (`https://musiclounge-api.scm.azurewebsites.net`) để
  xem thay vì tin tưởng file tồn tại lâu dài. Muốn log bền vững + tìm kiếm được, cần thêm
  Application Insights (SDK `Microsoft.ApplicationInsights.AspNetCore`, chưa có trong
  `MusicLounge.Api.csproj`) — việc code, ngoài phạm vi.
- **Key Vault**: hiện secrets nằm trực tiếp trong Application Settings (đã mã hoá at-rest bởi
  Azure, đủ an toàn cho quy mô đồ án) thay vì Key Vault riêng như sơ đồ kiến trúc vẽ — bỏ qua để
  đơn giản hoá, không bắt buộc cho demo.
- **panorama-stitcher (Python)**: cần deploy riêng lên Azure Container Apps (có Dockerfile sẵn ở
  `services/panorama-stitcher/Dockerfile`) rồi set `PanoramaStitcher__BaseUrl` trỏ tới đó — không
  nằm trong hướng dẫn này vì tính năng tour 360° không bắt buộc cho luồng demo chính.
- **Custom domain** (thay vì `*.azurewebsites.net`): `az webapp config hostname add`, cần bạn sở
  hữu domain riêng — bỏ qua nếu domain `azurewebsites.net` đủ dùng cho demo.

---

## Dọn dẹp (xoá hết resource khi không cần nữa, tránh phát sinh phí)

```
az group delete --name rg-musiclounge --yes --no-wait
```
Xoá **toàn bộ** resource group (App Service, SQL Database, App Service Plan) — không thể hoàn
tác. Chỉ chạy khi chắc chắn không cần lại (vd sau khi đã bảo vệ đồ án xong).

---

## Troubleshooting

**App Service trả 503 / "Application Error" ngay khi mở**
→ Gần như chắc chắn là lỗi fail-fast ở Bước 4 (thiếu `Jwt__Secret` hoặc 1 trong 3
`Business__Payment*Url`/`PasswordResetUrl`). Xem log: `az webapp log tail ...`, tìm dòng
`InvalidOperationException` — message nói rõ đang thiếu key nào.

**`dotnet ef database update` báo timeout/không kết nối được tới `*.database.windows.net`**
→ IP máy bạn chưa (hoặc không còn) nằm trong firewall rule Bước 2.3 — IP nhà mạng VN hay đổi,
chạy lại lệnh `az sql server firewall-rule create ... AllowMyIP` với IP hiện tại.

**`/swagger` trả 404 trên Azure dù local chạy được**
→ Đúng thiết kế (xem Bước 7.2), không phải bug — Swagger chỉ bật khi
`ASPNETCORE_ENVIRONMENT=Development`.

**Frontend gọi API bị lỗi CORS dù đã set `Cors__AllowedOrigins__0`**
→ Kiểm tra đúng domain (kể cả `https://` và không có dấu `/` ở cuối), và **restart App Service**
sau khi đổi Application Settings nếu thay đổi không có hiệu lực ngay:
`az webapp restart --name musiclounge-api --resource-group rg-musiclounge`.

**Deploy xong (Bước 6) nhưng ảnh/model 3D upload trước đó biến mất**
→ Đúng như mục "Ngoài phạm vi" ở trên — zip deploy ghi đè `wwwroot`. Không phải lỗi, là giới hạn
đã biết của cách lưu file hiện tại.

**Database "đánh thức" chậm (request đầu tiên sau 1 lúc không dùng bị treo ~30-60s)**
→ Triệu chứng của tier Serverless đang auto-pause. Từ 2026-08-18 database đã chuyển sang Standard S0
(Bước 2.4) — tier DTU không có auto-pause nên triệu chứng này **không còn nữa**. Nếu vẫn gặp, kiểm
tra `az sql db show ... --query "{tier:sku.tier,autoPause:autoPauseDelay}"`: đúng cấu hình phải ra
`Standard` và `autoPause: null`.

**Query chậm / timeout sau khi chuyển sang S0**
→ S0 chỉ có 10 DTU. Xem metric `dtu_consumption_percent`, nếu chạm 100% thường xuyên thì nâng lên S1
(`az sql db update ... --service-objective S1`, ~$29/tháng) — vẫn rẻ hơn serverless chạy 24/7 nhiều lần.
