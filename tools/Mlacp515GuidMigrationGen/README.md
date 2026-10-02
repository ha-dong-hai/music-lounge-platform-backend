# MLACP-515 — sinh và diễn tập migration đổi khoá int → GUID

Migration `20261001181153_Mlacp515GuidKeys` **không viết tay**: nó được sinh từ model EF thật bằng chương trình trong thư
mục này. Bản nháp EF tự sinh không dùng được (241 `AlterColumn` int→uniqueidentifier mà SQL Server không ép kiểu được, và
55 `DeleteData` seed sẽ cascade xoá liên kết thể loại/tâm trạng của mọi buổi hòa nhạc).

Không nằm trong `MusicLounge.sln` — chỉ chạy tay khi cần sinh lại.

## Sinh lại

```bash
# Infrastructure phải build được: nếu migration hiện tại hỏng, thay tạm Up() bằng thân rỗng rồi chạy.
dotnet run --project tools/Mlacp515GuidMigrationGen -- \
  tools/Mlacp515GuidMigrationGen/ban-nhap-ef-up.txt \
  src/MusicLounge.Infrastructure/Persistence/Migrations/20261001181153_Mlacp515GuidKeys.cs Mlacp515GuidKeys
```

`ban-nhap-ef-up.txt` là thân `Up()` của bản nháp EF (`dotnet ef migrations add`) — nguồn danh sách cột đổi kiểu và id seed
cũ. Chương trình **dừng, không ghi tệp** khi một cột không xác định được bảng đích, bảng đích không tồn tại hoặc khoá của nó
vốn đã là Guid, hay seed không khớp `OrderedGuid.FromLegacy` (đã thử đột biến 3 kiểu, cả 3 đều dừng).

## Diễn tập (bắt buộc trước khi áp lên Azure)

1. Sao DB (Azure: `az sql db copy`; máy dev: `BACKUP ... WITH COPY_ONLY` rồi `RESTORE` sang tên mới).
2. `sqlcmd -I -i dau_van_tay.sql` → lưu "trước". Kết quả chỉ so bằng chuỗi nên chạy được trước lẫn sau khi đổi kiểu.
3. `dotnet ef database update` trỏ vào bản sao.
4. Chạy lại `dau_van_tay.sql` → phải **giống hệt** "trước" (số dòng mọi bảng, số dòng nối được qua từng FK, tham chiếu
   không FK, chuỗi ReferenceId, tổng nợ/có sổ cái).
5. Tạo DB mẫu bằng `EnsureCreated` từ model, chạy `luoc_do.sql` trên cả hai: cột / chỉ mục / FK phải trùng.
   Ràng buộc mặc định (`df`) thừa ở DB đã migrate là di sản `defaultValue` của các migration cũ, có từ trước 515.
6. `sqlcmd` phải có `-I` (QUOTED_IDENTIFIER ON) — thiếu thì mọi UPDATE trên bảng có chỉ mục lọc bị từ chối.

### Lần diễn tập 02/10/2026 (máy dev, bản sao `SU26SE039_FE_KIEM` ở migration MLACP489)

| Kiểm | Kết quả |
|---|---|
| Lần áp đầu | **Đỏ** — `IX_ledger_accounts_OwnerType` chỉ dùng `OwnerId` trong bộ lọc nên không bị gỡ; transaction lùi sạch (vẫn ở MLACP489, không sót cột `__g` hay hàm). Đã sửa bộ sinh ở cả hai pha (gỡ + dựng lại). |
| Dấu vân tay trước/sau | 214/214 dòng giống hệt (71 bảng, 121 FK, nợ = có = 13.899.000) |
| Dấu vân tay có bắt lỗi không | Đổi 1 thông báo + 1 khiếu nại sang GUID sai trong transaction → lệch đúng 2 dòng (50→49, 29→28) |
| Lược đồ vs model | 737 cột, 234 chỉ mục, 121 FK trùng khớp; 0 FK `is_not_trusted` |
| Cột id còn int | 0 |
| DB rỗng chạy toàn bộ migration | Đạt, lược đồ trùng model, seed "Jazz" = `FromLegacy("music_genres", 1)` |
| API mới đọc dữ liệu đã đổi | `/health` 200, danh sách buổi diễn đúng thứ tự mới-nhất-trước, chi tiết 200, URL số cũ → 404 |

### Lần diễn tập 02/10/2026 trên bản sao Azure (`az sql db copy` SU26SE039 → SU26SE039_Dien515, S0)

| Kiểm | Kết quả |
|---|---|
| Chốt loại lạ (Pha 0) | Không chặn — mọi ReferenceType/TargetType trên dữ liệu thật đều đã ánh xạ |
| Thời gian áp | 146 giây (S0) — tính vào thời gian dừng app |
| Dấu vân tay trước/sau | 225/225 dòng giống hệt (bổ sung 11 phép kiểm cho loại chỉ có trên dữ liệu thật: 638 thông báo khiếu nại, hoàn tiền, quyết toán…); nợ = có = 3.586.000 |
| Lược đồ vs model | 737 cột / 234 chỉ mục / 121 FK trùng; thừa đúng `donations.PlatformFee` — cột thêm ngoài migration từ đợt deploy 17/08, có mặc định 0, có từ trước 515, không ảnh hưởng ghi dữ liệu |
| FK `is_not_trusted` / cột id còn int / hàm tạm | 0 / 0 / đã xoá |
| Chạy API trên bản sao | **Cố ý không chạy**: app khởi động sẽ chạy job Hangfire định kỳ trên dữ liệu thật (có thể gửi email/SMS/push thật). Đã kiểm API trên bản sao dev |

Bản sao và luật tường lửa tạm đã xoá ngay sau đó (deploy_log.md).

## Vận hành khi áp thật

Không phải expand–contract: code cũ không đọc được GUID, code mới không đọc được int.

1. Dừng App Service. 2. Chờ hàng đợi Hangfire (`Enqueued`, `Scheduled`, `Retries`) rỗng — job đang chờ mang tham số id int.
3. Ghi mốc PITR vào `deploy_log.md`. 4. Áp migration. 5. Deploy code mới. 6. Mọi người dùng bị đăng xuất một lần (token cũ
mang id int, bị từ chối 401 rồi làm mới cũng thất bại) — báo trước. 7. Frontend/mobile phát hành cùng lúc (`Number(id)`,
hiển thị `#id`).

Quay lui: `Down()` cố ý ném lỗi — khôi phục PITR về mốc ở bước 3.
