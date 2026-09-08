using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> b)
    {
        b.ToTable("content_reports");
        b.HasKey(r => r.Id);

        b.Property(r => r.TargetType).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        b.Property(r => r.ResolutionNote).HasMaxLength(1000);

        b.HasIndex(r => new { r.TargetType, r.TargetId, r.Status });

        b.HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction chu khong phai SetNull, va day khong phai lua chon ve kieu dang:
        // content_reports co HAI khoa ngoai tro ve users (Reporter o tren, va cai nay). SQL Server
        // tu choi hai duong xu-ly-khi-xoa cung di tu mot bang cha sang mot bang con — error 1785,
        // "may cause cycles or multiple cascade paths" — nen bang nay KHONG TAO DUOC neu ca hai
        // deu Cascade/SetNull.
        //
        // Vi sao doi ben nay chu khong doi Reporter: ReporterId la bat buoc va Cascade tren do la
        // hanh vi co y nghia (nguoi dung bien mat thi bao cao cua ho di theo). ResolvedByAdminId
        // cho phep null va chi la lien ket toi nguoi da xu ly. Ngoai ra he thong nay KHONG BAO GIO
        // xoa cung nguoi dung — quy trinh xoa du lieu ca nhan an danh hoa hang User tai cho — nen
        // khac biet giua SetNull va NoAction tren thuc te khong bao gio xay ra.
        //
        // SQLite khong kiem rang buoc nay, nen bo test khong bat duoc: loi chi lo ra khi ap
        // migration len SQL Server that.
        b.HasOne(r => r.ResolvedByAdmin)
            .WithMany()
            .HasForeignKey(r => r.ResolvedByAdminId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
