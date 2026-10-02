// MLACP-515: sinh migration chuyen khoa int -> GUID tu MODEL EF THAT (khong go tay). Dung (exit 1, khong ghi tep) khi:
//  - cot doi kieu khong xac dinh duoc bang dich; bang dich khong ton tai / khoa bang dich khong phai int cu;
//  - seed trong model khong khop FromLegacy(bang, idCu) cua ban nhap EF.
// Dung: dotnet run -- <Up() cua ban nhap EF> <tep-migration-ra.cs> Mlacp515GuidKeys
// Kiem sau khi ap (README.md cung thu muc): dau_van_tay.sql truoc/sau phai GIONG HET; luoc_do.sql DB da migrate vs DB EnsureCreated.
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MusicLounge.Domain.Common;
using MusicLounge.Infrastructure.Persistence;

var (upPath, outPath, className) = (args[0], args[1], args[2]);
var loi = new List<string>();
var opt = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=.;Database=x").Options;
using var db = new ApplicationDbContext(opt);
var model = db.GetService<IDesignTimeModel>().Model;
var types = model.GetEntityTypes().Where(e => e.GetTableName() != null && !e.IsOwned()).ToList();
var tables = types.Select(e => e.GetTableName()!).ToHashSet();
StoreObjectIdentifier So(IEntityType e) => StoreObjectIdentifier.Table(e.GetTableName()!, e.GetSchema());
IEntityType Et(string t) => types.First(e => e.GetTableName() == t);

// 1) Cot doi kieu (tu ban nhap EF): bang.cot -> (oldType, nullable moi)
var up = File.ReadAllText(upPath);
var doi = new Dictionary<(string T, string C), (string Old, bool Null)>();
foreach (Match m in Regex.Matches(up, @"AlterColumn<Guid>\(\s*name: ""(\w+)"",\s*table: ""(\w+)"",([\s\S]*?)oldClrType: typeof\((\w+)\)"))
    doi[(m.Groups[2].Value, m.Groups[1].Value)] = (m.Groups[4].Value, Regex.IsMatch(m.Groups[3].Value, @"nullable: true"));
var intPkTables = doi.Keys.Where(k => Et(k.T).FindPrimaryKey()!.Properties.Any(p => p.GetColumnName(So(Et(k.T))) == k.C)
                                      && Et(k.T).FindPrimaryKey()!.Properties.Count == 1).Select(k => k.T).ToHashSet();

string Mid(string t) => OrderedGuid.FromLegacy(t, 0).ToString().Substring(14, 9);
void Dich(string t, string vi)
{
    if (!tables.Contains(t)) loi.Add($"{vi}: bang '{t}' khong co trong model");
    else if (!intPkTables.Contains(t)) loi.Add($"{vi}: khoa chinh cua '{t}' KHONG phai int cu (da la Guid) — anh xa sai");
}

// 2) Anh xa cot khong co FK (kiem tay tung cot, nguon ghi trong migration sinh ra)
var poly = new Dictionary<(string, string), (string TypeCol, Dictionary<string, string> Map)>
{
    [("complaints", "TargetId")] = ("TargetType", new() { ["show"] = "lounge_shows", ["venue"] = "music_lounges", ["donation"] = "donations",
        ["penalty"] = "venue_penalties", ["livestream"] = "livestreams" }), // "ticket": xem ghi chu rieng
    [("content_reports", "TargetId")] = ("TargetType", new() { ["Show"] = "lounge_shows", ["Livestream"] = "livestreams",
        ["Rating"] = "lounge_show_ratings", ["ChatMessage"] = "livestream_chat_messages" }),
    [("event_moderations", "TargetId")] = ("TargetType", new() { ["Show"] = "lounge_shows", ["Livestream"] = "livestreams",
        ["GalleryImage"] = "lounge_gallery_images", ["TourScene"] = "venue_tour_scenes", ["TicketTier"] = "ticket_tiers" }),
};
string? KhongFk(string t, string c) =>
    Regex.IsMatch(c, "^(CreatedBy|UpdatedBy|StatusReviewedBy|CitizenCardReviewedBy|TaxProfileVerifiedBy)$") ? "users"
    : (t, c) switch
    {
        ("bank_accounts", "OwnerId") or ("ledger_accounts", "OwnerId") or ("settlements", "OwnerId") or ("known_admin_snapshots", "UserId") => "users",
        ("venue_penalties", "CompensatedSubscriptionId") => "owner_subscriptions",
        _ => null
    };

// 3) Cot chuoi ReferenceId (khong doi kieu, doi GIA TRI): bang -> loai -> bang dich. null = khong phai id int (bo qua).
var refs = new Dictionary<string, Dictionary<string, string?>>
{
    ["notifications"] = new()
    {
        ["show"] = "lounge_shows", ["livestream"] = "lounge_shows", ["refund_request"] = "refund_requests", ["cash_refund"] = "refund_requests",
        ["venue_penalty"] = "venue_penalties", ["settlement"] = "settlements", ["payment"] = "payments", ["lounge"] = "music_lounges",
        ["fnb_order"] = "fnb_orders", ["donation"] = "donations", ["complaint"] = "complaints", ["bank_account"] = "bank_accounts",
        ["user"] = "users", ["kyc_review"] = "users", ["payout_owner"] = "users", ["event_moderation"] = "event_moderations",
        ["ticket"] = null, ["security_ip"] = null, // ve von la Guid; IP khong phai id
    },
    ["payments"] = new() { ["TicketHold"] = "ticket_holds", ["Donation"] = "donations", ["FnbOrder"] = "fnb_orders",
        ["Subscription"] = "subscription_packages", ["WalkIn"] = "lounge_shows" },
    ["ledger_entries"] = new() { ["payment"] = "payments", ["settlement"] = "settlements", ["donation"] = "donations",
        ["refund"] = "refund_requests", ["subscription"] = "subscription_packages", ["fnb_order"] = "fnb_orders" },
};
// Hai dang ghep trong notifications: "<subId>:<moc>" va "<TargetType>:<TargetId>"
var reportTarget = poly[("content_reports", "TargetId")].Map;

// 4) Xac dinh bang dich cho moi cot doi kieu
var dich = new Dictionary<(string T, string C), string?>();
foreach (var (k, v) in doi)
{
    var et = Et(k.T);
    var p = et.GetProperties().First(x => x.GetColumnName(So(et)) == k.C);
    var fks = p.GetContainingForeignKeys().Select(f => f.PrincipalEntityType.GetTableName()!).Distinct().ToList();
    string? d = fks.Count == 1 ? fks[0] : fks.Count > 1 ? null : p.IsPrimaryKey() ? k.T : poly.ContainsKey(k) ? "(poly)" : KhongFk(k.T, k.C);
    if (fks.Count > 1) loi.Add($"{k.T}.{k.C}: nhieu FK toi {string.Join(",", fks)}");
    if (d is null) loi.Add($"{k.T}.{k.C}: KHONG xac dinh duoc bang dich");
    else if (d != "(poly)") Dich(d, $"{k.T}.{k.C}");
    if (v.Old is not ("int" or "long")) loi.Add($"{k.T}.{k.C}: kieu cu la {v.Old}");
    dich[k] = d;
}
foreach (var (k, v) in poly) foreach (var (ty, t) in v.Map) Dich(t, $"{k.Item1}.{k.Item2}[{ty}]");
foreach (var (t, m) in refs)
{
    if (!tables.Contains(t)) loi.Add($"bang ref '{t}' khong co");
    foreach (var (ty, d) in m) if (d is not null) Dich(d, $"{t}.ReferenceId[{ty}]");
}
Dich("owner_subscriptions", "notifications[subscription]");

// 5) Seed: Id moi phai = FromLegacy(bang, idCu) voi idCu lay tu DeleteData cua ban nhap
foreach (var g in Regex.Matches(up, @"DeleteData\(\s*table: ""(\w+)"",\s*keyColumn: ""Id"",\s*keyValue: (\d+)\)").GroupBy(m => m.Groups[1].Value))
{
    var cu = g.Select(m => OrderedGuid.FromLegacy(g.Key, long.Parse(m.Groups[2].Value))).ToHashSet();
    var moi = Et(g.Key).GetSeedData().Select(r => (Guid)r["Id"]!).ToHashSet();
    if (!cu.SetEquals(moi)) loi.Add($"seed {g.Key}: {moi.Count} Id moi khong khop FromLegacy cua {cu.Count} Id cu");
}

if (loi.Count > 0) { Console.Error.WriteLine("DUNG — " + loi.Count + " loi:\n  " + string.Join("\n  ", loi)); return 1; }

// ───────────── Sinh ma ─────────────
string Q(string s) => s.Replace("\"", "\"\"");
var sb = new StringBuilder();
void Sql(string s) => sb.AppendLine($"            migrationBuilder.Sql(@\"{Q(s)}\");");
string F(string col, string midExpr) => $"[dbo].[__mlacp515_g]([{col}], {midExpr})";
const string Fn = "[dbo].[__mlacp515_g]";

// Pha 0: ham doi + kiem cong thuc SQL == C# + chan loai la (truoc moi buoc pha)
sb.AppendLine("            // ── Pha 0: hàm đổi id cũ → GUID (đúng công thức OrderedGuid.FromLegacy), tự kiểm khớp C#, chặn loại lạ ──");
Sql($@"CREATE FUNCTION {Fn}(@v bigint, @mid char(9)) RETURNS uniqueidentifier AS
BEGIN
    IF @v IS NULL RETURN NULL;
    DECLARE @h char(12) = LOWER(RIGHT(CONVERT(varchar(16), CONVERT(binary(8), @v), 2), 12));
    RETURN CONVERT(uniqueidentifier, LEFT(@h, 8) + '-' + RIGHT(@h, 4) + '-' + @mid + '-' + @h);
END");
foreach (var (t, n) in new[] { ("users", 1L), ("music_genres", 7L), ("lounge_shows", 123456789L), ("payments", 0xFFFF_FFFF_FFFFL) })
    Sql($"IF {Fn}({n}, '{Mid(t)}') <> '{OrderedGuid.FromLegacy(t, n)}' THROW 51500, N'MLACP-515: công thức SQL lệch OrderedGuid.FromLegacy ({t},{n}).', 1;");
foreach (var (k, v) in poly)
    Sql($"IF EXISTS (SELECT 1 FROM [{k.Item1}] WHERE [{v.TypeCol}] NOT IN ({string.Join(", ", v.Map.Keys.Select(x => $"N'{x}'"))}{(k.Item1 == "complaints" ? ", N'ticket'" : "")})) THROW 51501, N'MLACP-515: {k.Item1}.{v.TypeCol} có loại chưa ánh xạ — dừng, chưa đổi gì.', 1;");
foreach (var (t, m) in refs)
    Sql($"IF EXISTS (SELECT 1 FROM [{t}] WHERE [ReferenceId] <> N'' AND [ReferenceId] NOT LIKE N'%[^0-9]%' AND [ReferenceType] NOT IN ({string.Join(", ", m.Where(x => x.Value != null).Select(x => $"N'{x.Key}'"))})) THROW 51502, N'MLACP-515: {t}.ReferenceId là số nhưng ReferenceType chưa ánh xạ — dừng, chưa đổi gì.', 1;");

// Pha 1: go rang buoc THEO DB THAT (khong theo ten trong model — chiu duoc lech ten)
sb.AppendLine();
sb.AppendLine("            // ── Pha 1: gỡ FK / PK / chỉ mục / mặc định / thống kê đụng tới các cột đổi kiểu — đọc từ sys.* của DB thật ──");
var vals = string.Join(",\n    ", doi.Keys.OrderBy(k => k.T).ThenBy(k => k.C).Select(k => $"(N'{k.T}', N'{k.C}')"));
Sql($@"SET NOCOUNT ON;
DECLARE @c TABLE (obj int, colid int, PRIMARY KEY (obj, colid));
INSERT INTO @c SELECT OBJECT_ID(N'[dbo].' + QUOTENAME(t)), COLUMNPROPERTY(OBJECT_ID(N'[dbo].' + QUOTENAME(t)), c, 'ColumnId')
FROM (VALUES
    {vals}) v(t, c);
IF EXISTS (SELECT 1 FROM @c WHERE obj IS NULL OR colid IS NULL) THROW 51503, N'MLACP-515: DB thiếu bảng/cột mà model nói là có — dừng.', 1;
IF EXISTS (SELECT 1 FROM sys.check_constraints k JOIN @c c ON c.obj = k.parent_object_id AND c.colid = k.parent_column_id)
    THROW 51504, N'MLACP-515: có CHECK constraint trên cột khoá — chưa xử lý, dừng.', 1;
DECLARE @s nvarchar(max) = N'';
SELECT @s += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
    + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(10)
FROM sys.foreign_keys fk
WHERE EXISTS (SELECT 1 FROM sys.foreign_key_columns fc JOIN @c c
    ON (c.obj = fc.parent_object_id AND c.colid = fc.parent_column_id) OR (c.obj = fc.referenced_object_id AND c.colid = fc.referenced_column_id)
    WHERE fc.constraint_object_id = fk.object_id);
EXEC (@s); SET @s = N'';
SELECT @s += CASE WHEN i.is_primary_key = 1 OR i.is_unique_constraint = 1
        THEN N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + N'.' + QUOTENAME(OBJECT_NAME(i.object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(i.name)
        ELSE N'DROP INDEX ' + QUOTENAME(i.name) + N' ON ' + QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + N'.' + QUOTENAME(OBJECT_NAME(i.object_id)) END
    + N';' + CHAR(10)
FROM sys.indexes i
WHERE i.index_id > 0 AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN @c c ON c.obj = ic.object_id AND c.colid = ic.column_id
    WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id)
   -- chi muc loc: cot chi nam trong BO LOC (vd IX_ledger_accounts_OwnerType ... WHERE [OwnerId] IS NULL) khong co trong index_columns
   OR (i.has_filter = 1 AND EXISTS (SELECT 1 FROM @c c JOIN sys.columns col ON col.object_id = c.obj AND col.column_id = c.colid
       WHERE c.obj = i.object_id AND i.filter_definition LIKE N'%' + REPLACE(QUOTENAME(col.name), N'[', N'[[]') + N'%'));
EXEC (@s); SET @s = N'';
SELECT @s += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(d.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(d.parent_object_id))
    + N' DROP CONSTRAINT ' + QUOTENAME(d.name) + N';' + CHAR(10)
FROM sys.default_constraints d JOIN @c c ON c.obj = d.parent_object_id AND c.colid = d.parent_column_id;
EXEC (@s); SET @s = N'';
SELECT @s += N'DROP STATISTICS ' + QUOTENAME(OBJECT_SCHEMA_NAME(st.object_id)) + N'.' + QUOTENAME(OBJECT_NAME(st.object_id)) + N'.' + QUOTENAME(st.name) + N';' + CHAR(10)
FROM sys.stats st
WHERE st.user_created = 1 AND EXISTS (SELECT 1 FROM sys.stats_columns sc JOIN @c c ON c.obj = sc.object_id AND c.colid = sc.column_id
    WHERE sc.object_id = st.object_id AND sc.stats_id = st.stats_id);
EXEC (@s);");

// Pha 2: doi tung cot
sb.AppendLine();
sb.AppendLine("            // ── Pha 2: mỗi cột: thêm cột GUID → tính bằng FromLegacy(bảng đích, id cũ) → bỏ cột int → đổi tên ──");
foreach (var k in doi.Keys.OrderBy(k => k.T).ThenBy(k => k.C))
{
    var (t, c) = k; var g = c + "__g"; var d = dich[k]!;
    string expr;
    if (d == "(poly)")
    {
        var (tc, map) = poly[k];
        var cases = string.Join(" ", map.Select(x => $"WHEN N'{x.Key}' THEN '{Mid(x.Value)}'"));
        if (t == "complaints") cases += $" WHEN N'ticket' THEN '{Mid("tickets")}'";
        expr = F(c, $"CASE [{tc}] {cases} END");
    }
    else expr = F(c, $"'{Mid(d)}'");
    sb.AppendLine($"            // {t}.{c} → {(d == "(poly)" ? "theo " + poly[k].TypeCol : d)}");
    Sql($"ALTER TABLE [{t}] ADD [{g}] uniqueidentifier NULL;");
    Sql($"UPDATE [{t}] SET [{g}] = {expr};");
    Sql($"IF EXISTS (SELECT 1 FROM [{t}] WHERE [{c}] IS NOT NULL AND [{g}] IS NULL) THROW 51505, N'MLACP-515: {t}.{c} còn dòng chưa đổi được.', 1;");
    Sql($"ALTER TABLE [{t}] DROP COLUMN [{c}];");
    Sql($"EXEC sp_rename N'[{t}].[{g}]', N'{c}', N'COLUMN';");
    if (!doi[k].Null) Sql($"ALTER TABLE [{t}] ALTER COLUMN [{c}] uniqueidentifier NOT NULL;");
}

// Pha 3: chuoi ReferenceId
sb.AppendLine();
sb.AppendLine("            // ── Pha 3: cột chuỗi ReferenceId giữ id cũ dạng số (deep link, chống gửi trùng, sổ cái) → GUID chữ thường ──");
string RefSql(string tbl, string mid, string where) =>
    $"UPDATE [{tbl}] SET [ReferenceId] = LOWER(CONVERT(nvarchar(36), {Fn}(CAST([ReferenceId] AS bigint), '{mid}'))) WHERE {where} AND [ReferenceId] <> N'' AND [ReferenceId] NOT LIKE N'%[^0-9]%';";
foreach (var (t, m) in refs)
    foreach (var (ty, d) in m.Where(x => x.Value != null))
        Sql(RefSql(t, Mid(d!), $"[ReferenceType] = N'{ty}'"));
Sql($@"UPDATE [notifications] SET [ReferenceId] = LOWER(CONVERT(nvarchar(36), {Fn}(CAST(LEFT([ReferenceId], CHARINDEX(N':', [ReferenceId]) - 1) AS bigint), '{Mid("owner_subscriptions")}')))
    + SUBSTRING([ReferenceId], CHARINDEX(N':', [ReferenceId]), 100)
WHERE [ReferenceType] = N'subscription' AND [ReferenceId] LIKE N'[0-9]%:%' AND LEFT([ReferenceId], CHARINDEX(N':', [ReferenceId]) - 1) NOT LIKE N'%[^0-9]%';");
foreach (var (ty, d) in reportTarget)
    Sql($@"UPDATE [notifications] SET [ReferenceId] = N'{ty}:' + LOWER(CONVERT(nvarchar(36), {Fn}(CAST(SUBSTRING([ReferenceId], {ty.Length + 2}, 100) AS bigint), '{Mid(d)}')))
WHERE [ReferenceType] = N'content_report_target' AND [ReferenceId] LIKE N'{ty}:[0-9]%' AND SUBSTRING([ReferenceId], {ty.Length + 2}, 100) NOT LIKE N'%[^0-9]%';");
Sql("IF EXISTS (SELECT 1 FROM [notifications] WHERE [ReferenceType] IN (N'subscription', N'content_report_target') AND [ReferenceId] LIKE N'%:[0-9]%' AND [ReferenceId] NOT LIKE N'%-%') THROW 51506, N'MLACP-515: còn ReferenceId dạng ghép chưa đổi.', 1;");

// Pha 4: dung lai PK / chi muc / FK THEO MODEL (ten, unique, filter, include, on delete)
sb.AppendLine();
sb.AppendLine("            // ── Pha 4: dựng lại khoá chính, chỉ mục, khoá ngoại đúng như model EF đích ──");
bool Cham(IEntityType e, IEnumerable<IProperty> ps) => ps.Any(p => doi.ContainsKey((e.GetTableName()!, p.GetColumnName(So(e))!)));
string Arr(IEnumerable<string> xs) { var l = xs.ToList(); return l.Count == 1 ? $"\"{l[0]}\"" : $"new[] {{ {string.Join(", ", l.Select(x => $"\"{x}\""))} }}"; }
string Cols(IEntityType e, IEnumerable<IProperty> ps) => Arr(ps.Select(p => p.GetColumnName(So(e))!));
int nPk = 0, nIx = 0, nFk = 0;
foreach (var e in types.OrderBy(e => e.GetTableName()))
{
    var pk = e.FindPrimaryKey()!;
    if (Cham(e, pk.Properties)) { nPk++; sb.AppendLine($"            migrationBuilder.AddPrimaryKey(name: \"{pk.GetName()}\", table: \"{e.GetTableName()}\", column{(pk.Properties.Count > 1 ? "s" : "")}: {Cols(e, pk.Properties)});"); }
    foreach (var ak in e.GetKeys().Where(k => !k.IsPrimaryKey() && Cham(e, k.Properties)))
        loi.Add($"khoa phu {ak.GetName()} tren {e.GetTableName()} — chua ho tro");
}
foreach (var e in types.OrderBy(e => e.GetTableName()))
    foreach (var ix in e.GetIndexes().Where(ix => Cham(e, ix.Properties)
                 || (ix.GetFilter() is { } loc && doi.Keys.Any(k => k.T == e.GetTableName() && loc.Contains($"[{k.C}]")))))
    {
        nIx++;
        var inc = ix.GetIncludeProperties();
        var extra = (ix.IsUnique ? ", unique: true" : "") + (ix.GetFilter() is { } f ? $", filter: \"{f.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"" : "");
        if (inc is { Count: > 0 }) loi.Add($"index {ix.GetDatabaseName()} co INCLUDE — chua ho tro");
        sb.AppendLine($"            migrationBuilder.CreateIndex(name: \"{ix.GetDatabaseName()}\", table: \"{e.GetTableName()}\", column{(ix.Properties.Count > 1 ? "s" : "")}: {Cols(e, ix.Properties)}{extra});");
    }
foreach (var e in types.OrderBy(e => e.GetTableName()))
    foreach (var fk in e.GetForeignKeys().Where(fk => Cham(e, fk.Properties) || Cham(fk.PrincipalEntityType, fk.PrincipalKey.Properties)))
    {
        if (fk.PrincipalEntityType.GetTableName() is null || e.IsOwned()) continue;
        nFk++;
        var act = fk.DeleteBehavior switch
        {
            DeleteBehavior.Cascade => "Cascade", DeleteBehavior.SetNull => "SetNull", DeleteBehavior.Restrict => "Restrict", _ => "NoAction"
        };
        var pc = fk.PrincipalKey.Properties.Select(p => p.GetColumnName(So(fk.PrincipalEntityType))!);
        sb.AppendLine($"            migrationBuilder.AddForeignKey(name: \"{fk.GetConstraintName()}\", table: \"{e.GetTableName()}\", column{(fk.Properties.Count > 1 ? "s" : "")}: {Cols(e, fk.Properties)}, " +
                      $"principalTable: \"{fk.PrincipalEntityType.GetTableName()}\", principalColumn{(fk.Properties.Count > 1 ? "s" : "")}: {Arr(pc)}, onDelete: ReferentialAction.{act});");
    }
sb.AppendLine();
Sql($"DROP FUNCTION {Fn};");
if (loi.Count > 0) { Console.Error.WriteLine("DUNG — " + string.Join("\n  ", loi)); return 1; }

var header = $$"""
// <auto-generated-by-hand-reviewed />
// MLACP-515 (D-19): đổi MỌI khoá int sang GUID có thứ tự, GIỮ NGUYÊN dữ liệu.
//
// Vì sao không dùng migration EF tự sinh: bản nháp EF gồm 241 AlterColumn int→uniqueidentifier (SQL Server không ép kiểu
// được, cũng không gỡ được IDENTITY bằng ALTER) và 55 DeleteData/InsertData cho seed — chạy được thì sẽ XOÁ thể loại/tâm
// trạng/không khí rồi cascade xoá liên kết của mọi buổi hòa nhạc và sở thích người dùng.
//
// Cách làm: mỗi id cũ n của bảng T thành OrderedGuid.FromLegacy(T, n) — CÙNG công thức seed trong mã dùng, nên khoá ngoại
// tính lại tự khớp. Tệp này SINH BẰNG MÁY từ model EF thật (tools/Mlacp515GuidMigrationGen), kèm các chốt:
//   - Pha 0 tự kiểm công thức SQL = C# trên 4 giá trị, và DỪNG nếu gặp loại đối tượng/ReferenceType chưa ánh xạ — trước
//     mọi bước phá; cả migration chạy trong một transaction nên dừng giữa chừng cũng không để lại nửa vời.
//   - Pha 1 gỡ ràng buộc theo sys.* của DB THẬT (chịu được tên lệch snapshot); Pha 4 dựng lại theo model ({{nPk}} PK, {{nIx}} chỉ mục, {{nFk}} FK).
//   - Cột không có FK, ánh xạ tay có nguồn: *By/OwnerId/known_admin_snapshots.UserId → users (người dùng),
//     CompensatedSubscriptionId → owner_subscriptions; TargetId đa hình theo TargetType (validator CreateComplaint,
//     SubmitContentReport, nơi tạo EventModeration); ReferenceId của notifications/payments/ledger_entries theo từng nơi tạo
//     (NotifyAsync, WriteJournalAsync, Payment.ReferenceType). Khiếu nại "ticket" cũ: id int chưa từng trỏ được vé (vé vốn
//     là Guid) — vẫn đổi dạng để không mất dòng, nhưng không trỏ vé nào.
//   - ledger_entries là append-only (D8): migration này chỉ đổi DẠNG id, không đổi bút toán nào.
//
// Vận hành (bắt buộc, xem deploy_log): đây KHÔNG phải expand–contract — code cũ không đọc được GUID. Dừng app → chờ hàng
// đợi Hangfire rỗng (job đang chờ mang tham số int) → áp migration → deploy code mới. Mọi phiên đăng nhập bị đăng xuất một lần
// (token cũ mang id int, bị từ chối 401). Diễn tập trên bản sao DB trước.
//
// Down: KHÔNG hỗ trợ — GUID không đổi ngược về id cũ một cách an toàn khi đã có bản ghi mới. Quay lui = khôi phục PITR.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class {{className}} : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

""";
var footer = $$"""
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "MLACP-515: không đổi ngược GUID → int. Quay lui bằng khôi phục theo thời điểm (PITR) về trước lúc áp migration.");
        }
    }
}

""";
File.WriteAllText(outPath, header + sb.ToString().TrimEnd() + "\n" + footer);
Console.WriteLine($"DAT — cot doi {doi.Count} (PK {intPkTables.Count} bang), PK {nPk}, chi muc {nIx}, FK {nFk}, poly {poly.Count}, ref {refs.Sum(r => r.Value.Count)} -> {outPath}");
return 0;
