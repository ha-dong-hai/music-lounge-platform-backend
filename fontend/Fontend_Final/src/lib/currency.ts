// Shared VND formatter. Extracted 2026-08-18 when the Admin finance screens landed -- the same
// `${Math.round(n).toLocaleString("vi-VN")}đ` line was about to be pasted into a fourth file.
//
// Math.round before formatting is deliberate, not lazy: every monetary column the API returns is a
// decimal, and vi-VN's default grouping would render fractional dong (e.g. "120.000,5đ") which is
// not a real denomination -- the smallest circulating note is 1.000đ and prices are always whole.
export function formatVnd(amount: number): string {
  return `${Math.round(amount).toLocaleString("vi-VN")}đ`;
}
