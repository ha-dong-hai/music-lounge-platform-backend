import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { animate, stagger } from "animejs";
import { Bell, CheckCircle, Envelope, MagnifyingGlass, Phone, User, XCircle } from "@phosphor-icons/react";
import { ApiError, getStoredToken, getStoredUser } from "../api/client";
import { AdminUser, deactivateUser, getUsers, reactivateUser, UserRole } from "../api/admin";
import { formatRelativeTimeVi } from "../lib/relativeTime";
import AdminSidebar from "../components/AdminSidebar";

// Ported 2026-08-18 from `admin_packages_user_directory` in the AuraLounge Stitch export, restyled
// onto this project's tokens (that library ships Libre Caslon + Manrope and its own palette; only
// the layout and information architecture were reused).
//
// The mock bundled subscription-package management into the same screen as the user directory. Split
// here: packages are a different resource with a different endpoint family
// (POST /subscriptions/packages) and belong on the platform-settings screen, not in a people list.
//
// Filters are all real GetUsersQuery parameters (searchText / role / isActive), so unlike the
// refunds queue this screen genuinely can filter -- the toolbar is not decorative. Search is
// debounced and folded into the same request rather than filtering client-side, because the list is
// paginated server-side and client-side filtering would only ever search the current page.

const ROLE_LABELS: Record<UserRole, string> = {
  Audience: "Khán giả",
  Staff: "Nhân viên",
  Owner: "Chủ phòng trà",
  Admin: "Quản trị viên",
};

const ROLE_ORDER: UserRole[] = ["Audience", "Staff", "Owner", "Admin"];

export default function AdminUsers() {
  const navigate = useNavigate();
  const [users, setUsers] = useState<AdminUser[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [searchText, setSearchText] = useState("");
  const [roleFilter, setRoleFilter] = useState<UserRole | "">("");
  const [activeFilter, setActiveFilter] = useState<"" | "true" | "false">("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);
  const animatedKeys = useRef<Set<string>>(new Set());

  const pageSize = 20;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  // Debounce the text box into the actual query param -- 300ms, same settle time as the performer
  // search in the Create Show wizard.
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearchText(searchInput.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    const token = getStoredToken();
    const user = getStoredUser();
    if (!token || user?.role !== "Admin") {
      navigate("/login");
      return;
    }
    let cancelled = false;
    setUsers(null);
    getUsers({
      searchText: searchText || undefined,
      role: roleFilter || undefined,
      isActive: activeFilter === "" ? undefined : activeFilter === "true",
      page,
      pageSize,
    })
      .then((result) => {
        if (cancelled) return;
        setUsers(result.items);
        setTotalCount(result.totalCount);
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
    return () => {
      cancelled = true;
    };
  }, [navigate, page, searchText, roleFilter, activeFilter]);

  useEffect(() => {
    const key = `${page}|${searchText}|${roleFilter}|${activeFilter}`;
    if (users && users.length > 0 && !animatedKeys.current.has(key)) {
      animatedKeys.current.add(key);
      animate(".user-row", {
        opacity: [0, 1],
        translateY: [12, 0],
        delay: stagger(50),
        duration: 400,
        ease: "outQuad",
      });
    }
  }, [users, page, searchText, roleFilter, activeFilter]);

  // Deactivate/reactivate flip a flag rather than removing the row -- unlike the refund and
  // pending-venue queues, the record stays in this list either way, so the row updates in place.
  async function handleToggleActive(u: AdminUser) {
    setBusyId(u.id);
    setActionError(null);
    try {
      if (u.isActive) await deactivateUser(u.id);
      else await reactivateUser(u.id);
      setUsers((prev) => prev?.map((x) => (x.id === u.id ? { ...x, isActive: !x.isActive } : x)) ?? prev);
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyId(null);
    }
  }

  const filterChipBase =
    "px-4 py-2 rounded-full font-label-md text-label-md border transition-colors whitespace-nowrap";

  return (
    <div className="bg-surface text-on-surface font-body-md min-h-screen flex">
      <AdminSidebar active="Người dùng" />

      <main className="ml-72 flex-1 flex flex-col min-h-screen">
        <header className="bg-surface border-b border-outline-variant/20 h-20 px-xl flex justify-between items-center sticky top-0 z-40">
          <div className="flex-1 max-w-md">
            <div className="relative">
              <MagnifyingGlass
                weight="light"
                size={18}
                className="absolute left-4 top-1/2 -translate-y-1/2 text-on-surface-variant"
              />
              <input
                type="search"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                placeholder="Tìm theo tên hoặc email..."
                className="w-full bg-surface-container-low border border-outline-variant rounded-full pl-11 pr-4 py-2.5 text-on-surface font-body-md text-body-md focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all placeholder:text-on-surface-variant/50"
              />
            </div>
          </div>
          <div className="flex items-center gap-4">
            <button className="text-on-surface-variant hover:text-primary transition-colors p-2 rounded-full hover:bg-surface-variant/50">
              <Bell weight="light" size={22} />
            </button>
            <div className="w-10 h-10 rounded-full border border-outline-variant/30 bg-surface-container flex items-center justify-center">
              <User weight="light" size={20} className="text-on-surface-variant" />
            </div>
          </div>
        </header>

        <div className="px-xl py-lg max-w-container-max mx-auto w-full flex-1">
          <div className="mb-lg">
            <h1 className="font-headline-md text-headline-md text-on-surface mb-2">Người dùng</h1>
            <p className="font-body-md text-body-md text-on-surface-variant">
              {users === null ? "Đang tải..." : `${totalCount} tài khoản`}
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2 mb-lg">
            <button
              onClick={() => {
                setRoleFilter("");
                setPage(1);
              }}
              className={`${filterChipBase} ${
                roleFilter === ""
                  ? "border-primary bg-primary text-on-primary"
                  : "border-outline-variant bg-surface-container-low text-on-surface-variant hover:border-primary hover:text-primary"
              }`}
            >
              Tất cả vai trò
            </button>
            {ROLE_ORDER.map((r) => (
              <button
                key={r}
                onClick={() => {
                  setRoleFilter(r);
                  setPage(1);
                }}
                className={`${filterChipBase} ${
                  roleFilter === r
                    ? "border-primary bg-primary text-on-primary"
                    : "border-outline-variant bg-surface-container-low text-on-surface-variant hover:border-primary hover:text-primary"
                }`}
              >
                {ROLE_LABELS[r]}
              </button>
            ))}

            <span className="w-px h-6 bg-outline-variant/40 mx-2" />

            {(
              [
                ["", "Mọi trạng thái"],
                ["true", "Đang hoạt động"],
                ["false", "Đã khóa"],
              ] as const
            ).map(([value, label]) => (
              <button
                key={label}
                onClick={() => {
                  setActiveFilter(value);
                  setPage(1);
                }}
                className={`${filterChipBase} ${
                  activeFilter === value
                    ? "border-primary bg-primary text-on-primary"
                    : "border-outline-variant bg-surface-container-low text-on-surface-variant hover:border-primary hover:text-primary"
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          {errorMessage && (
            <div
              role="alert"
              aria-live="assertive"
              className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 mb-6"
            >
              {errorMessage}
            </div>
          )}
          {actionError && (
            <div
              role="alert"
              aria-live="assertive"
              className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 mb-6"
            >
              {actionError}
            </div>
          )}

          {users !== null && users.length === 0 && (
            <p className="font-body-md text-body-md text-on-surface-variant text-center py-24">
              Không tìm thấy tài khoản nào khớp bộ lọc.
            </p>
          )}

          <div className="border-t border-outline-variant/20">
            {users?.map((u) => (
              <div
                key={u.id}
                className={`user-row border-b border-outline-variant/20 py-6 flex flex-col lg:flex-row lg:items-center justify-between gap-6 transition-colors ${
                  u.isActive ? "" : "opacity-60"
                }`}
              >
                <div className="flex items-center gap-4 flex-1 min-w-0">
                  {u.avatarUrl ? (
                    <img
                      src={u.avatarUrl}
                      alt={u.fullName}
                      className="w-12 h-12 rounded-full object-cover shrink-0 border border-outline-variant/30"
                    />
                  ) : (
                    <div className="w-12 h-12 rounded-full bg-surface-container flex items-center justify-center shrink-0 border border-outline-variant/30">
                      <User weight="light" size={22} className="text-on-surface-variant" />
                    </div>
                  )}
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap mb-1">
                      <h3 className="font-title-lg text-body-lg font-semibold text-on-surface truncate">{u.fullName}</h3>
                      <span className="bg-surface-container-high text-on-surface-variant font-label-md text-xs px-2.5 py-0.5 rounded-full">
                        {ROLE_LABELS[u.role as UserRole] ?? u.role}
                      </span>
                      {!u.isActive && (
                        <span className="bg-error-container text-on-error-container font-label-md text-xs px-2.5 py-0.5 rounded-full">
                          Đã khóa
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-4 flex-wrap text-sm font-body-md text-on-surface-variant">
                      <span className="flex items-center gap-1.5 min-w-0">
                        <Envelope weight="light" size={15} className="shrink-0" />
                        <span className="truncate">{u.email}</span>
                        {u.isEmailVerified ? (
                          <CheckCircle weight="fill" size={14} className="text-primary shrink-0" aria-label="Email đã xác thực" />
                        ) : (
                          <XCircle weight="light" size={14} className="text-on-surface-variant/60 shrink-0" aria-label="Email chưa xác thực" />
                        )}
                      </span>
                      {u.phone && (
                        <span className="flex items-center gap-1.5">
                          <Phone weight="light" size={15} />
                          {u.phone}
                        </span>
                      )}
                      <span className="opacity-70">Tham gia {formatRelativeTimeVi(u.createdAt)}</span>
                    </div>
                  </div>
                </div>

                <div className="shrink-0">
                  <button
                    onClick={() => handleToggleActive(u)}
                    disabled={busyId === u.id}
                    className={`px-6 py-2.5 rounded-lg font-label-md text-label-md transition-colors disabled:opacity-50 ${
                      u.isActive
                        ? "border border-outline text-on-surface hover:bg-surface-variant"
                        : "bg-primary text-on-primary hover:bg-primary-container shadow-sm"
                    }`}
                  >
                    {busyId === u.id ? "Đang xử lý..." : u.isActive ? "Khóa tài khoản" : "Mở khóa"}
                  </button>
                </div>
              </div>
            ))}
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between mt-lg">
              <p className="font-body-md text-sm text-on-surface-variant">
                Trang {page} / {totalPages}
              </p>
              <div className="flex items-center gap-3">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="px-4 py-2 rounded-lg border border-outline text-on-surface font-label-md text-label-md hover:bg-surface-variant transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  Trước
                </button>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page >= totalPages}
                  className="px-4 py-2 rounded-lg border border-outline text-on-surface font-label-md text-label-md hover:bg-surface-variant transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  Sau
                </button>
              </div>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
