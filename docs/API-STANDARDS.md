# API Response & Validation Standard

Tài liệu này là **nguồn chân lý duy nhất** (source of truth) cho hình dạng response, mã lỗi HTTP,
và quy tắc validate của toàn bộ API. Mọi endpoint mới — do người hay do AI viết — đều phải tuân
theo đúng chuẩn này. Không tự chế thêm 1 shape response khác, dù chỉ cho 1 endpoint.

Tài liệu phản ánh đúng những gì code hiện tại đang làm (đã verify bằng test thật), không phải mô
tả lý tưởng chưa triển khai.

---

## 1. Nguyên tắc chung

1. **Chỉ 1 hình dạng JSON cho mọi response thành công, chỉ 1 hình dạng cho mọi response lỗi.**
   Không có ngoại lệ theo từng controller.
2. **Không tự tay dựng response lỗi trong controller/handler.** Muốn báo lỗi, `throw` đúng loại
   exception ở §3 — để `GlobalExceptionHandler` xử lý tập trung. Không `return BadRequest(...)`,
   không `new { success = false, ... }` viết tay ở bất kỳ đâu khác.
3. **Validate business rule ở FluentValidation Validator, không ở tận `SaveChangesAsync`.** Nếu 1
   field tham chiếu khóa ngoại (FK) tới bảng khác, validator phải check tồn tại trước khi vào
   handler (§4) — không để nó rớt xuống DB rồi bắt `DbUpdateException` chung chung.

---

## 2. Response thành công

Toàn bộ response 2xx dùng `ApiResponse<T>`
([Common/Models/ApiResponse.cs](../src/MusicLounge.Application/Common/Models/ApiResponse.cs)):

```json
{ "success": true, "data": { /* T, có thể null nếu action không trả data */ }, "message": null }
```

- `GET`/`POST` trả data → `200 OK` / `201 Created`, `ApiResponse<T>.Ok(data)`.
- Action không có gì để trả (cancel, delete, update thuần) → `204 No Content`, **không** bọc
  `ApiResponse` (trả body rỗng đúng chuẩn REST).
- Danh sách phân trang → `T` là `PaginatedResult<TItem>` (§5), không tự chế shape phân trang khác.

---

## 3. Response lỗi

Toàn bộ lỗi — dù ném từ đâu trong pipeline — phải ra đúng 1 shape này:

```json
{ "success": false, "message": "Mô tả lỗi bằng tiếng Việt cho user", "errors": { "TenField": ["chi tiết"] } }
```

`errors` là `null` nếu lỗi không gắn với field cụ thể (vd NotFound, Forbidden). Có 2 con đường tạo
ra đúng shape này:

### 3a. Exception nghiệp vụ → `GlobalExceptionHandler`

[Middleware/GlobalExceptionHandler.cs](../src/MusicLounge.Api/Middleware/GlobalExceptionHandler.cs)
bắt mọi exception ném ra từ MediatR handler và map theo bảng dưới. **Không thêm case mới vào bảng
này nếu chưa thật sự cần 1 status code khác** — tái dùng đúng exception có sẵn thay vì tạo loại mới.

| Ném exception | HTTP Status | Khi dùng |
|---|---|---|
| `MusicLounge.Domain.Exceptions.NotFoundException` | 404 | Không tìm thấy entity theo ID |
| `MusicLounge.Domain.Exceptions.UnauthorizedException` | 401 | Sai mật khẩu, chưa xác thực email... (không phải thiếu token — thiếu token do middleware auth tự trả 401) |
| `MusicLounge.Domain.Exceptions.ForbiddenException` | 403 | Có quyền đăng nhập nhưng không đủ quyền/không sở hữu resource |
| `MusicLounge.Domain.Exceptions.ConflictException` | 409 | Trạng thái hiện tại xung đột với hành động (đã duyệt rồi, đã check-in rồi...) |
| `MusicLounge.Domain.Exceptions.DomainException` | 422 | Vi phạm business rule (chưa đủ ngày làm việc, hết vé, thiếu điều kiện tiên quyết...) |
| `MusicLounge.Application.Common.Exceptions.ValidationException` | 400 | FluentValidation fail (tự động ném bởi `ValidationBehavior`, xem §4) |
| `MusicLounge.Application.Common.Exceptions.ExternalServiceException` | 503 | Dịch vụ ngoài (VNPay, Mux...) lỗi/timeout |
| `DbUpdateException` | 409 | **Lưới an toàn cuối cùng**, không phải nơi để cố ý dựa vào — nếu code hit case này thường xuyên nghĩa là thiếu validate FK ở §4, phải bổ sung validator, không phải chấp nhận sống chung với message chung chung này |
| (chưa bắt) | 500 | Bug thật — phải sửa, không được để rơi vào nhánh này khi đã biết trước tình huống |

### 3b. Lỗi tự động của `[ApiController]`

Thiếu field bắt buộc, sai kiểu enum trong query/route, JSON malformed... → **cũng phải ra đúng
shape trên**, không phải RFC7807 `ProblemDetails` mặc định của ASP.NET Core. Đã override sẵn tại
[Program.cs — `ConfigureApiBehaviorOptions`](../src/MusicLounge.Api/Program.cs). Không được xóa
đoạn override này khi refactor `Program.cs`.

### 3c. Rate limit (429)

Không đi qua `GlobalExceptionHandler` (bị chặn ở tầng middleware trước đó) nhưng vẫn phải cùng
shape — xem `opt.OnRejected` trong
[Program.cs](../src/MusicLounge.Api/Program.cs), kèm header `Retry-After` (giây).

---

## 4. Validate — quy tắc viết FluentValidation Validator

Mỗi `Command`/`Query` có validator riêng, đặt cùng thư mục, tên `<Command>Validator.cs`.

**a) Rule đồng bộ** (độ dài, bắt buộc, range số, enum-string hợp lệ...) — dùng `RuleFor(...).NotEmpty()/.MaximumLength()/.Must(...)` như bình thường. Không cần DB.

**b) Field tham chiếu khóa ngoại (FK) sang bảng khác** — **bắt buộc** check tồn tại bằng
`MustAsync` gọi `IUnitOfWork.Repository<T,TKey>().AnyAsync(...)`, tiêm `IUnitOfWork` qua
constructor validator:

```csharp
public sealed class CreateXCommandValidator : AbstractValidator<CreateXCommand>
{
    public CreateXCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.SomeForeignId)
            .MustAsync(async (id, ct) => await uow.Repository<TargetEntity, int>().AnyAsync(e => e.Id == id!.Value, ct))
            .When(x => x.SomeForeignId.HasValue)   // bỏ .When(...) nếu field bắt buộc, không nullable
            .WithMessage("SomeForeignId không tồn tại.");
    }
}
```

Message luôn theo mẫu `"<TenField> không tồn tại."` — giữ nhất quán để FE dựa vào `errors.<TenField>`
mà không cần parse message tự do. Ví dụ đã áp dụng: `AtmosphereId`
([CreateLoungeCommandValidator](../src/MusicLounge.Application/Lounges/Commands/CreateLounge/CreateLoungeCommandValidator.cs)),
`CategoryId`/`GenreIds`
([CreateLoungeShowCommandValidator](../src/MusicLounge.Application/LoungeShows/Commands/CreateLoungeShow/CreateLoungeShowCommandValidator.cs)),
`ZoneId`
([CreateTicketTierCommandValidator](../src/MusicLounge.Application/TicketTiers/Commands/CreateTicketTier/CreateTicketTierCommandValidator.cs)).

**Vì sao bắt buộc, không phải tùy chọn:** thiếu bước này thì FK sai chỉ bị chặn ở tận
`SaveChangesAsync`, rớt vào case `DbUpdateException` ở bảng §3a — FE nhận 409 mơ hồ, không biết
field nào sai (sự cố thật đã xảy ra với `atmosphereId`, xem lịch sử sửa lỗi trong repo).

---

## 5. Phân trang

Query danh sách nhận `page` (mặc định 1) và `pageSize` (mặc định tùy endpoint, tối đa 100 —
`ClampPaginationActionFilter` tự clamp ở tầng ngoài cùng, handler **không cần** tự validate lại 2
field này). Response luôn là:

```json
{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 137 }
```

(chính là `PaginatedResult<T>`, nằm trong `data` của `ApiResponse<T>` như §2)

---

## 6. Quy ước đặt tên & serialize

- JSON: **camelCase** cho mọi property (mặc định `System.Text.Json`, không cấu hình khác đi).
- Enum: serialize dạng **string** (`"Offline"`, không phải `0`) —
  `JsonStringEnumConverter` đã đăng ký global trong `Program.cs`. Enum mới thêm không cần cấu hình
  gì thêm, tự động theo đúng chuẩn này.
- `DateTimeOffset` cho MỌI thời điểm có ý nghĩa với người dùng (không dùng `DateTime` trừ cột nội bộ
  thuần kỹ thuật như `CreatedAt` một số entity cũ) — giữ nguyên offset múi giờ, không quy về UTC rồi
  bỏ offset.
- Message lỗi hiển thị cho user: **tiếng Việt**. Comment trong code: tiếng Anh hoặc tiếng Việt không
  dấu đều được (repo hiện dùng cả hai, không có rule cứng) — nhưng string HIỂN THỊ RA NGOÀI API
  luôn phải có dấu, đúng chính tả.

---

## 7. Checklist khi thêm 1 endpoint mới

Trước khi coi 1 endpoint là xong, tự tick đủ danh sách này:

- [ ] Trả `ApiResponse<T>` cho response có data, `204 NoContent` (không bọc) cho action thuần.
- [ ] Mọi field bắt buộc có `RuleFor(...).NotEmpty()`/tương đương trong Validator.
- [ ] Mọi field FK (kiểu `int`/`int?` trỏ tới entity khác) có `MustAsync` check tồn tại — xem §4.
- [ ] Không có `try/catch` tự trả response lỗi trong controller — để exception tự nổi lên
      `GlobalExceptionHandler`.
- [ ] Danh sách trả `PaginatedResult<T>`, nhận `page`/`pageSize` qua query, không tự chế tên field
      phân trang khác (`total`, `count`, `pages`...).
- [ ] `[Authorize(Policy = Policies.X)]` đúng vai trò yêu cầu — kiểm tra chéo với bảng phân quyền
      hiện có trong `Authorization/Policies.cs`, không tạo policy string mới nếu 1 trong 4 policy có
      sẵn (`RequireAuthenticated`/`RequireStaff`/`RequireOwner`/`RequireAdmin`) đã đủ dùng.
- [ ] Đã có test tích hợp: 1 test role đúng → thành công, 1 test role sai → 401/403 (xem CF1-CF7 làm
      mẫu trong `tests/MusicLounge.Tests.Integration/`).
