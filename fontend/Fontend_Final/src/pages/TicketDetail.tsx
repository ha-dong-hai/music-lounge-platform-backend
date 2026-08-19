import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, getStoredToken } from "../api/client";
import { cancelTicket, getTicketDetail, getTicketQrSvg, TicketDetail as TicketDetailType } from "../api/tickets";
import { formatVnd } from "../lib/currency";
import AudienceHeader from "../components/AudienceHeader";
import AudienceFooter from "../components/AudienceFooter";

// 2026-08-18: swapped the old standalone plain header for the shared AudienceHeader/AudienceFooter
// and corrected border-radius to the real AuraLounge scale (rounded-[8px] for the outer card --
// DESIGN.md "Containment: larger containers use 8px" -- rounded-[4px] for the cancel button, per
// stitch_ai_audience/aura_echo/DESIGN.md). No Stitch mock exists for this screen specifically.

const STATUS_LABEL: Record<string, string> = {
  Pending: "Chờ thanh toán",
  Confirmed: "Đã xác nhận",
  Used: "Đã sử dụng",
  Cancelled: "Đã huỷ",
  Refunded: "Đã hoàn tiền",
};

function formatShowDateTime(iso: string): string {
  const d = new Date(iso);
  const weekday = d.toLocaleDateString("vi-VN", { weekday: "long" });
  const date = d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
  const time = d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  return `${weekday.charAt(0).toUpperCase()}${weekday.slice(1)}, ${date} · ${time}`;
}

export default function TicketDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [ticket, setTicket] = useState<TicketDetailType | null>(null);
  const [qrSvg, setQrSvg] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);
  const [cancelError, setCancelError] = useState<string | null>(null);

  useEffect(() => {
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }
  }, [navigate]);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    getTicketDetail(id)
      .then((result) => {
        if (cancelled) return;
        setTicket(result);
        // QR only makes sense to show for a valid, checkable-in ticket -- Confirmed status.
        if (result.status === "Confirmed") {
          getTicketQrSvg(id)
            .then((svg) => {
              if (!cancelled) setQrSvg(svg);
            })
            .catch(() => {
              // Non-fatal -- ticket details still render without the QR image.
            });
        }
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(
          err instanceof ApiError && err.status === 404 ? "Không tìm thấy vé này." : "Không thể kết nối tới máy chủ."
        );
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  async function handleCancel() {
    if (!id || !confirm("Huỷ vé này? Yêu cầu hoàn tiền sẽ được tạo và Admin sẽ xử lý.")) return;
    setCancelling(true);
    setCancelError(null);
    try {
      await cancelTicket(id);
      const refreshed = await getTicketDetail(id);
      setTicket(refreshed);
      setQrSvg(null);
    } catch (err) {
      setCancelError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setCancelling(false);
    }
  }

  if (errorMessage) {
    return (
      <div className="bg-[#fbf9f4] min-h-screen flex items-center justify-center text-[#1b1c19] px-6" style={{ fontFamily: "'Manrope', sans-serif" }}>
        <div className="text-center">
          <p className="text-lg mb-2" style={{ fontFamily: "'Libre Caslon Text', serif" }}>{errorMessage}</p>
          <Link to="/me/tickets" className="text-sm text-[#4d4540] hover:text-[#181512] underline">Quay lại Vé của tôi</Link>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-[#fbf9f4] min-h-screen text-[#1b1c19] flex flex-col" style={{ fontFamily: "'Manrope', sans-serif" }}>
      <AudienceHeader active="Vé của tôi" />

      <div className="flex-1 max-w-md mx-auto px-6 py-10 w-full">
        <Link to="/me/tickets" className="inline-block text-sm text-[#4d4540] hover:text-[#181512] transition-colors mb-6">
          ← Vé của tôi
        </Link>
        {!ticket ? (
          <p className="text-center text-[#4d4540] py-16">Đang tải...</p>
        ) : (
          <div className="rounded-[8px] border border-[#1b1c19]/10 bg-white overflow-hidden">
            <div className="bg-[#181512] text-white px-6 py-6 text-center">
              <p className="text-xs tracking-[0.15em] text-[#e9c176] mb-2">{STATUS_LABEL[ticket.status] ?? ticket.status}</p>
              <h1 className="text-2xl" style={{ fontFamily: "'Libre Caslon Text', serif" }}>{ticket.showName}</h1>
            </div>

            <div className="px-6 py-6 space-y-4">
              <div>
                <p className="text-xs text-[#7e756f] mb-0.5">Thời gian</p>
                <p className="text-sm">{formatShowDateTime(ticket.showScheduledStart)}</p>
              </div>
              <div>
                <p className="text-xs text-[#7e756f] mb-0.5">Địa điểm</p>
                <p className="text-sm">{ticket.loungeName} — {ticket.loungeAddress}</p>
              </div>
              <div>
                <p className="text-xs text-[#7e756f] mb-0.5">Hạng vé</p>
                <p className="text-sm">{ticket.tierName} — {ticket.priceName}</p>
              </div>
              <div className="flex items-center justify-between pt-2 border-t border-[#1b1c19]/10">
                <span className="text-sm">Đã thanh toán</span>
                <span className="text-base font-medium">{formatVnd(ticket.pricePaid)}</span>
              </div>

              {ticket.physicalDetail?.checkedInAt && (
                <p className="text-xs text-[#4d4540]">
                  Đã check-in lúc {formatShowDateTime(ticket.physicalDetail.checkedInAt)}.
                </p>
              )}

              {qrSvg && (
                <div className="pt-4 border-t border-[#1b1c19]/10 text-center">
                  <p className="text-xs text-[#7e756f] mb-3">Xuất trình mã này tại cửa để check-in</p>
                  <div
                    className="inline-block w-48 h-48 mx-auto"
                    // Trusted, non-user-controlled: our own backend's QR-generator output
                    // (Net.Codecrete.QrCodeGenerator), never third-party or user-supplied markup.
                    dangerouslySetInnerHTML={{ __html: qrSvg }}
                  />
                </div>
              )}

              {cancelError && <p className="text-sm text-[#ba1a1a]">{cancelError}</p>}

              {ticket.status === "Confirmed" && (
                <button
                  onClick={handleCancel}
                  disabled={cancelling}
                  className="w-full mt-2 px-4 py-3 rounded-[4px] border border-[#cfc4bd] text-sm text-[#4d4540] hover:bg-[#f0eee9] transition-colors disabled:opacity-50"
                >
                  {cancelling ? "Đang xử lý..." : "Huỷ vé & yêu cầu hoàn tiền"}
                </button>
              )}
            </div>
          </div>
        )}
      </div>

      <AudienceFooter />
    </div>
  );
}
