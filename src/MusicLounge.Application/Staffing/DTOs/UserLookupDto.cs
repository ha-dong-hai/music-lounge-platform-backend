namespace MusicLounge.Application.Staffing.DTOs;

// Khong tra Role — endpoint nay chi phuc vu Owner tra cuu 1 user (chua chac lien quan gi toi
// lounge cua ho) de moi lam staff, khong phai de xem thong tin nguoi khac. Tra them Role bien no
// thanh cong cu do role/danh tinh cua bat ky ai tren he thong neu doan/biet duoc email cua ho.
public sealed record UserLookupDto(int Id, string FullName, string Email);
