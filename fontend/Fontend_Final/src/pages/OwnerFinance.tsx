import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { DownloadSimple } from "@phosphor-icons/react";
import { ApiError, clearSession, getStoredToken } from "../api/client";
import { EarningsSummary, OwnerAnalytics, getMyEarnings, getOwnerAnalytics } from "../api/analytics";
import { getMyLounges, LoungeListItem } from "../api/lounges";
import { formatVnd } from "../lib/currency";
import OwnerHeader from "../components/OwnerHeader";

// Ported from
// stitch/stitch_ai_powered_hybrid_music_lounge_platform (4)/.../owner_financial_earnings_settlements
// (screen.png read in full). The mock's own left sidebar shell ("Aura Admin") is NOT ported --
// every other Owner screen in this app already uses the shared OwnerHeader top-nav
// (docs/architecture/platform-architecture.md: Owner/Admin=web with a top-nav shell, confirmed 2026-08-17), so
// only the content region is built here, on that same shell, for consistency with
// OwnerMyVenues/OwnerCreateVenue/OwnerMyShows/OwnerSubscription.
//
// Serves 2 routes with one component: /owner/finance (top-nav "Finance" link, no venue
// preselected) and /owner/venues/:id/analytics (the ChartLine button on each venue card in
// OwnerMyVenues.tsx) -- same data, the only difference is which venue is selected on load.
//
// Two real backend sources, both RequireOwner: GET /analytics/my-lounge?loungeId= (per-venue
// revenue/tickets/trend) and GET /me/earnings (owner-wide settlement ledger). The mock's "Platform
// Fees" panel (Ticketing Fee 3% / Processing Fee / Marketing Boost, itemized) has NO real backing
// field anywhere -- Payment.GatewayFee is a permanent unused 0 and there is no per-category fee
// breakdown on any DTO. Rather than invent 3 fake percentages for a money screen (exactly the class
// of bug multiple prior BLOCKERs in this codebase were about), that panel is replaced with a real
// "Đối Soát" (Settlement) summary from EarningsSummaryDto instead -- same visual slot, real numbers.
// "Export Report" is a real client-side CSV export of the data already on screen, not a fake button.

const MONTH_NAMES = [
  "Th1", "Th2", "Th3", "Th4", "Th5", "Th6", "Th7", "Th8", "Th9", "Th10", "Th11", "Th12",
];

const SETTLEMENT_STATUS_LABEL: Record<string, string> = {
  Scheduled: "Đã lên lịch",
  Released: "Đã giải ngân",
  Cancelled: "Đã huỷ",
  PendingReview: "Chờ Admin xét duyệt",
};

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
}

function csvCell(value: string | number): string {
  const s = String(value);
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

function downloadCsv(filename: string, rows: (string | number)[][]) {
  const csv = rows.map((row) => row.map(csvCell).join(",")).join("\n");
  const blob = new Blob([`﻿${csv}`], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export default function OwnerFinance() {
  const { id } = useParams<{ id?: string }>();
  const navigate = useNavigate();

  const [venues, setVenues] = useState<LoungeListItem[] | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(id ? Number(id) : null);
  const [analytics, setAnalytics] = useState<OwnerAnalytics | null>(null);
  const [earnings, setEarnings] = useState<EarningsSummary | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }
    let cancelled = false;
    getMyLounges()
      .then((result) => {
        if (cancelled) return;
        setVenues(result.items);
        if (!selectedId && result.items.length > 0) {
          const approved = result.items.find((v) => v.status === "Approved");
          setSelectedId((approved ?? result.items[0]).id);
        }
      })
      .catch((err) => {
        if (cancelled) return;
        if (err instanceof ApiError && err.status === 401) {
          clearSession();
          navigate("/login");
          return;
        }
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
    getMyEarnings()
      .then((result) => {
        if (!cancelled) setEarnings(result);
      })
      .catch(() => {
        if (!cancelled) setEarnings(null);
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [navigate]);

  useEffect(() => {
    if (!selectedId) return;
    let cancelled = false;
    setAnalytics(null);
    getOwnerAnalytics(selectedId)
      .then((result) => {
        if (!cancelled) setAnalytics(result);
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
    return () => {
      cancelled = true;
    };
  }, [selectedId]);

  const selectedVenue = venues?.find((v) => v.id === selectedId) ?? null;

  const maxMonthTotal = useMemo(() => {
    if (!analytics) return 0;
    return Math.max(
      1,
      ...analytics.revenueTrend.map((m) => m.fnbRevenue + m.offlineTicketRevenue + m.onlineTicketRevenue)
    );
  }, [analytics]);

  function handleExport() {
    if (!analytics || !selectedVenue) return;
    const rows: (string | number)[][] = [
      ["Báo cáo doanh thu", selectedVenue.name],
      [],
      ["Tổng doanh thu", analytics.totalRevenue],
      ["Doanh thu vé", analytics.ticketRevenue],
      ["Doanh thu F&B", analytics.fnbRevenue],
      [],
      ["Xu hướng doanh thu theo tháng"],
      ["Năm", "Tháng", "Vé tại venue", "Vé online", "F&B"],
      ...analytics.revenueTrend.map((m) => [m.year, m.month, m.offlineTicketRevenue, m.onlineTicketRevenue, m.fnbRevenue]),
      [],
      ["Lịch sử đối soát"],
      ["Ngày lên lịch", "Số tiền", "Trạng thái", "Ngày giải ngân"],
      ...(earnings?.recentSettlements.map((s) => [
        formatDate(s.scheduledAt),
        s.amount,
        SETTLEMENT_STATUS_LABEL[s.status] ?? s.status,
        s.paidAt ? formatDate(s.paidAt) : "",
      ]) ?? []),
    ];
    downloadCsv(`doanh-thu-${selectedVenue.name.replace(/\s+/g, "-").toLowerCase()}.csv`, rows);
  }

  return (
    <div className="text-on-background min-h-screen flex flex-col bg-surface font-body-md text-body-md">
      <OwnerHeader active="Finance" />

      <main className="flex-1 w-full">
        <div className="max-w-container-max mx-auto px-margin-mobile md:px-lg py-12 md:py-16">
          <div className="flex flex-col md:flex-row justify-between items-start md:items-end gap-6 mb-12">
            <div>
              <h1 className="font-display-lg text-headline-sm md:text-display-lg text-on-surface mb-2">
                Doanh Thu &amp; Đối Soát
              </h1>
              <p className="font-body-md text-body-md text-on-surface-variant">
                Tổng quan tài chính{selectedVenue ? ` — ${selectedVenue.name}` : ""}.
              </p>
            </div>
            <div className="flex items-center gap-4">
              {venues && venues.length > 1 && (
                <select
                  value={selectedId ?? ""}
                  onChange={(e) => setSelectedId(Number(e.target.value))}
                  className="px-4 py-2.5 bg-surface-container-low border border-outline-variant/30 rounded-lg text-sm text-on-surface outline-none focus:border-primary"
                >
                  {venues.map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.name}
                    </option>
                  ))}
                </select>
              )}
              <button
                onClick={handleExport}
                disabled={!analytics}
                className="flex items-center gap-2 px-5 py-2.5 border border-primary/30 hover:border-primary hover:bg-primary/5 transition-colors rounded-lg text-sm text-primary disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <DownloadSimple size={18} weight="light" />
                Xuất báo cáo CSV
              </button>
            </div>
          </div>

          {errorMessage && (
            <div className="rounded-xl bg-error-container text-on-error-container text-sm px-4 py-3 mb-8">
              {errorMessage}
            </div>
          )}

          {venues !== null && venues.length === 0 && (
            <div className="text-center py-24 space-y-4">
              <p className="font-headline-md text-headline-sm text-on-surface">Bạn chưa có venue nào.</p>
              <Link to="/owner/venues/new" className="inline-block px-8 py-3 bg-primary text-on-primary rounded-xl text-sm">
                Tạo venue đầu tiên
              </Link>
            </div>
          )}

          {selectedId !== null && (
            <>
              {!analytics ? (
                <p className="text-center text-on-surface-variant py-24">Đang tải...</p>
              ) : (
                <>
                  {/* Metric cards */}
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
                    <div className="bg-surface-container-lowest border border-outline-variant/15 rounded-xl p-6">
                      <p className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest mb-3">
                        Tổng doanh thu
                      </p>
                      <p className="font-display-lg text-3xl md:text-4xl text-on-surface mb-1">
                        {formatVnd(analytics.totalRevenue)}
                      </p>
                      <p className="text-xs text-on-surface-variant">
                        {analytics.totalTicketsSold} vé · {analytics.totalShows} show
                      </p>
                    </div>
                    <div className="bg-surface-container-lowest border border-outline-variant/15 rounded-xl p-6">
                      <p className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest mb-3">
                        Doanh thu vé
                      </p>
                      <p className="font-display-lg text-3xl md:text-4xl text-on-surface mb-1">
                        {formatVnd(analytics.ticketRevenue)}
                      </p>
                      <p className="text-xs text-on-surface-variant">
                        {analytics.totalRevenue > 0
                          ? `${Math.round((analytics.ticketRevenue / analytics.totalRevenue) * 100)}% tổng doanh thu`
                          : "Chưa có doanh thu"}
                      </p>
                    </div>
                    <div className="bg-surface-container-lowest border border-outline-variant/15 rounded-xl p-6">
                      <p className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest mb-3">
                        Doanh thu F&amp;B
                      </p>
                      <p className="font-display-lg text-3xl md:text-4xl text-on-surface mb-1">
                        {formatVnd(analytics.fnbRevenue)}
                      </p>
                      <p className="text-xs text-on-surface-variant">
                        {analytics.totalRevenue > 0
                          ? `${Math.round((analytics.fnbRevenue / analytics.totalRevenue) * 100)}% tổng doanh thu`
                          : "Chưa có doanh thu"}
                      </p>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
                    {/* Revenue trend chart -- real per-month data, not the mock's placeholder text */}
                    <div className="lg:col-span-2 bg-surface-container-lowest border border-outline-variant/15 rounded-xl p-6">
                      <h2 className="font-headline-md text-headline-sm text-on-surface mb-6">Xu hướng doanh thu</h2>
                      {analytics.revenueTrend.length === 0 ? (
                        <p className="text-sm text-on-surface-variant py-12 text-center">Chưa có dữ liệu doanh thu.</p>
                      ) : (
                        <div className="flex items-end gap-3 h-56">
                          {analytics.revenueTrend.map((m) => {
                            const total = m.fnbRevenue + m.offlineTicketRevenue + m.onlineTicketRevenue;
                            const heightPct = Math.max(2, (total / maxMonthTotal) * 100);
                            return (
                              <div key={`${m.year}-${m.month}`} className="flex-1 flex flex-col items-center justify-end h-full gap-2">
                                <div className="w-full flex flex-col justify-end rounded-t-lg overflow-hidden" style={{ height: "100%" }}>
                                  <div
                                    className="w-full bg-primary/80 rounded-t-lg transition-all"
                                    style={{ height: `${heightPct}%` }}
                                    title={formatVnd(total)}
                                  />
                                </div>
                                <span className="text-[10px] text-on-surface-variant uppercase tracking-wider">
                                  {MONTH_NAMES[m.month - 1]}
                                </span>
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>

                    {/* Settlement summary -- replaces the mock's fabricated "Platform Fees" panel
                        with real EarningsSummaryDto numbers (see file header comment). */}
                    <div className="bg-surface-container-lowest border border-outline-variant/15 rounded-xl p-6">
                      <h2 className="font-headline-md text-headline-sm text-on-surface mb-6">Đối Soát</h2>
                      {earnings ? (
                        <div className="space-y-4">
                          <div className="flex justify-between items-center text-sm">
                            <span className="text-on-surface-variant">Đang chờ đối soát</span>
                            <span className="text-on-surface font-medium">
                              {formatVnd(earnings.pendingSettlement)}
                              {earnings.pendingSettlementCount > 0 && (
                                <span className="text-xs text-on-surface-variant"> ({earnings.pendingSettlementCount})</span>
                              )}
                            </span>
                          </div>
                          <div className="flex justify-between items-center text-sm">
                            <span className="text-on-surface-variant">Đã hoàn tất</span>
                            <span className="text-on-surface font-medium">{formatVnd(earnings.completedSettlement)}</span>
                          </div>
                          {analytics.pendingArtistPayoutCount > 0 && (
                            <div className="flex justify-between items-center text-sm">
                              <span className="text-on-surface-variant">Chờ trả nghệ sĩ ({analytics.pendingArtistPayoutCount})</span>
                              <span className="text-error font-medium">-{formatVnd(analytics.pendingArtistPayoutAmount)}</span>
                            </div>
                          )}
                          <div className="pt-4 border-t border-outline-variant/20">
                            <p className="text-xs text-on-surface-variant uppercase tracking-widest mb-1">Tổng đã kiếm được</p>
                            <p className="font-display-lg text-2xl text-on-surface">{formatVnd(earnings.totalEarned)}</p>
                          </div>
                        </div>
                      ) : (
                        <p className="text-sm text-on-surface-variant">Đang tải...</p>
                      )}
                    </div>
                  </div>

                  {/* Recent Settlements */}
                  <div className="bg-surface-container-lowest border border-outline-variant/15 rounded-xl p-6 mb-8">
                    <h2 className="font-headline-md text-headline-sm text-on-surface mb-6">Lịch sử đối soát gần đây</h2>
                    {!earnings || earnings.recentSettlements.length === 0 ? (
                      <p className="text-sm text-on-surface-variant text-center py-8">Chưa có lịch đối soát nào.</p>
                    ) : (
                      <div className="overflow-x-auto">
                        <table className="w-full text-sm">
                          <thead>
                            <tr className="text-left text-on-surface-variant uppercase text-xs tracking-widest border-b border-outline-variant/20">
                              <th className="pb-3 font-normal">Ngày lên lịch</th>
                              <th className="pb-3 font-normal">Số tiền</th>
                              <th className="pb-3 font-normal">Trạng thái</th>
                              <th className="pb-3 font-normal">Ngày giải ngân</th>
                            </tr>
                          </thead>
                          <tbody>
                            {earnings.recentSettlements.map((s) => (
                              <tr key={s.id} className="border-b border-outline-variant/10 last:border-0">
                                <td className="py-3 text-on-surface">{formatDate(s.scheduledAt)}</td>
                                <td className="py-3 text-on-surface font-medium">{formatVnd(s.amount)}</td>
                                <td className="py-3 text-on-surface-variant">{SETTLEMENT_STATUS_LABEL[s.status] ?? s.status}</td>
                                <td className="py-3 text-on-surface-variant">{s.paidAt ? formatDate(s.paidAt) : "—"}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>

                  {/* Top Shows -- real data (OwnerAnalyticsDto.TopShows), no mock equivalent but a
                      natural fit for a finance/earnings page and genuinely available. */}
                  {analytics.topShows.length > 0 && (
                    <div className="bg-surface-container-lowest border border-outline-variant/15 rounded-xl p-6">
                      <h2 className="font-headline-md text-headline-sm text-on-surface mb-6">Show doanh thu cao nhất</h2>
                      <div className="overflow-x-auto">
                        <table className="w-full text-sm">
                          <thead>
                            <tr className="text-left text-on-surface-variant uppercase text-xs tracking-widest border-b border-outline-variant/20">
                              <th className="pb-3 font-normal">Show</th>
                              <th className="pb-3 font-normal">Ngày</th>
                              <th className="pb-3 font-normal">Vé bán</th>
                              <th className="pb-3 font-normal">Doanh thu</th>
                            </tr>
                          </thead>
                          <tbody>
                            {analytics.topShows.map((s) => (
                              <tr key={s.showId} className="border-b border-outline-variant/10 last:border-0">
                                <td className="py-3 text-on-surface">
                                  <Link to={`/owner/shows/${s.showId}`} className="hover:text-primary transition-colors">
                                    {s.name}
                                  </Link>
                                </td>
                                <td className="py-3 text-on-surface-variant">{formatDate(s.scheduledStart)}</td>
                                <td className="py-3 text-on-surface-variant">
                                  {s.ticketsSold}
                                  {s.totalCapacity !== null && ` / ${s.totalCapacity}`}
                                </td>
                                <td className="py-3 text-on-surface font-medium">{formatVnd(s.revenue)}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  )}
                </>
              )}
            </>
          )}
        </div>
      </main>
    </div>
  );
}
