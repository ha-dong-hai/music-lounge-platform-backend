namespace MusicLounge.Api.Swagger;

// Danh dau 1 action [AllowAnonymous] nhung handler cua no van doc ICurrentUserService de ca nhan
// hoa/gate 1 phan response khi nguoi goi CO dang nhap (vd Lounges.GetAll?mine=true, LoungeShows
// wishlist flag, Complaints.Create gan ComplainantUserId). AuthorizeCheckOperationFilter mac dinh
// khong gan Bearer security requirement cho endpoint AllowAnonymous (de khong hien nham icon khoa
// tren endpoint cong khai) — nhung he qua la nut "Authorize" cua Swagger UI khong bao gio gui token
// cho NHUNG endpoint nay, du da Authorize thanh cong, khien nhanh "da dang nhap" khong the test qua
// Swagger duoc (vd mine=true luon nhan ve "Vui long dang nhap" du token hop le). Gan attribute nay
// de filter van dinh kem security scheme, thay vi lam action tro thanh yeu cau bat buoc dang nhap.
[AttributeUsage(AttributeTargets.Method)]
public sealed class SwaggerOptionalAuthAttribute : Attribute;
