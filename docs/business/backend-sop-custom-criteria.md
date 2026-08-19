# Backend SOP: wire up Owner-defined Custom Criteria (AI recommendation)

Đề xuất cho backend cân nhắc — không phải bug, đây là schema đã có sẵn (3 bảng, đã migrate) nhưng chưa có bất kỳ API nào. Không có API thì frontend không thể thiết kế/code view thật cho tính năng "Owner tự đặt tiêu chí riêng cho phòng trà" được.

## Bối cảnh

`CustomCriteria` / `EventCustomValue` / `UserCustomPreference` (`src/MusicLounge.Domain/Entities/`) đã tồn tại từ migration `20260809143542_AddSecurityDetectionAndSocialLinks` nhưng không có command/query/controller nào tham chiếu tới (đã grep xác nhận: 0 kết quả trong `MusicLounge.Application` và `MusicLounge.Api`).

Ý tưởng theo comment gốc trong entity: mỗi Owner tự định nghĩa tiêu chí riêng cho lounge của mình (khác 4 tiêu chí nền tảng Genre/Mood/Atmosphere/Category do Admin quản lý — đã có API), gắn giá trị tiêu chí đó vào từng show, để engine gợi ý dùng làm tín hiệu match với sở thích user.

## Phạm vi đề xuất

**Trong phạm vi (đủ để frontend build 1 view thật):**
- Owner tạo/sửa/vô hiệu hoá `CustomCriteria` cho lounge của mình.
- Gắn giá trị `EventCustomValue` cho từng show khi tạo/sửa show.

**Ngoài phạm vi (không đề xuất làm cùng đợt):**
- `UserCustomPreference` — đây là trọng số do AI tự học từ hành vi user (công thức EMA đã có comment sẵn trong entity: `weight_new = 0.3 × new_signal + 0.7 × old_weight`), không phải form ai đó nhập tay. Cần một engine gợi ý riêng để ghi vào bảng này — quy mô lớn hơn hẳn, nên tách thành đề xuất khác sau khi phần định nghĩa tiêu chí + gắn giá trị đã chạy ổn.

## API đề xuất

Theo đúng pattern đã dùng cho `PerformersController`/`BankAccountsController` (MediatR command/query + FluentValidation + REST controller), route dưới `lounges/{loungeId}/custom-criteria`, `[Authorize(Policy = Policies.RequireOwner)]` + kiểm tra lounge thuộc về user hiện tại (giống cách `PerformersController` giới hạn sửa theo `CreatedByUserId`).

| Method | Route | Command/Query |
|---|---|---|
| GET | `/lounges/{loungeId}/custom-criteria` | `GetCustomCriteriaByLoungeQuery(int LoungeId)` → `IReadOnlyList<CustomCriteriaDto>` |
| POST | `/lounges/{loungeId}/custom-criteria` | `CreateCustomCriteriaCommand(int LoungeId, string Name, string Key, CustomCriteriaDataType DataType, string? Options)` → `int` |
| PUT | `/custom-criteria/{id}` | `UpdateCustomCriteriaCommand(int Id, string Name, string? Options, bool IsActive)` — **không cho sửa `Key`/`DataType`** sau khi tạo (đổi kiểu dữ liệu sẽ làm hỏng các `EventCustomValue` đã lưu theo kiểu cũ) |

Không đề xuất `DELETE` — dùng `IsActive=false` qua Update để tắt hiển thị, tránh phải xử lý các `EventCustomValue` đang tham chiếu tới criteria đó.

```csharp
public sealed record CustomCriteriaDto(
    int Id, int LoungeId, string Name, string Key,
    CustomCriteriaDataType DataType, string? Options, bool IsActive);
```

**Validation (`CreateCustomCriteriaCommandValidator`):**
- `Name`: NotEmpty, tối đa 100 ký tự.
- `Key`: NotEmpty, tối đa 100 ký tự, chỉ chữ thường/số/underscore (machine-readable), **unique trong phạm vi `LoungeId`** (query `uow.Repository<CustomCriteria,int>().AnyAsync(c => c.LoungeId == LoungeId && c.Key == Key)` — cùng cách `CreatePerformerCommandValidator` check `GenreId` tồn tại).
- `DataType`: phải parse được enum.
- `Options`: bắt buộc phải có nếu `DataType == Select` (JSON array chuỗi) hoặc `Range` (JSON object `{min,max,step}`); bỏ qua nếu `Boolean`/`Text`. Validate parse được JSON đúng shape tương ứng, không chỉ check NotEmpty.

## Wiring vào show creation

`CreateLoungeShowCommand`/`UpdateLoungeShowCommand` đã có tiền lệ nhận list inline (`GenreIds`, `Performances` — xác nhận qua so sánh với repo MusicLounge cũ). Đề xuất thêm cùng kiểu:

```csharp
public sealed record CustomCriteriaValueInput(int CriteriaId, string Value);
// thêm vào CreateLoungeShowCommand / UpdateLoungeShowCommand:
IReadOnlyList<CustomCriteriaValueInput> CustomValues
```

Validator cần: `CriteriaId` phải thuộc đúng `LoungeId` của show (không cho gắn tiêu chí của lounge khác), và `Value` phải hợp lệ theo `DataType` của criteria đó (Select → nằm trong Options; Range → số trong khoảng min/max; Boolean → "true"/"false"; Text → không giới hạn ngoài độ dài hợp lý).

`LoungeShowDetailDto` nên trả thêm `IReadOnlyList<EventCustomValueDto> CustomValues` (CriteriaId, CriteriaName, Value) để frontend hiển thị lại khi sửa show.

## Sau khi có API — frontend sẽ làm gì

1. Trang Owner "Tiêu chí riêng" (trong `/manager/lounges/:id` hoặc mục riêng): danh sách criteria hiện có (chip/tag như 4 tiêu chí nền tảng) + form thêm mới, trong đó chọn DataType sẽ đổi động phần nhập Options (giống mô tả trước: chọn Select hiện ô nhập nhiều giá trị, chọn Range hiện 3 ô min/max/step).
2. `ShowCreatePage`/trang sửa show: sau khi load được danh sách criteria của lounge đang chọn, render thêm field tương ứng mỗi criteria (dropdown cho Select, slider/number cho Range, checkbox cho Boolean, input cho Text) để Owner set giá trị cho show đó.

---

_Đây là đề xuất, chưa implement. Cần xác nhận trước khi code: route đặt dưới `LoungesController` hay tách `CustomCriteriaController` riêng — đề xuất trên giả định route lồng dưới lounges cho nhất quán với ownership check, nhưng có thể tách nếu muốn quản lý version độc lập._
