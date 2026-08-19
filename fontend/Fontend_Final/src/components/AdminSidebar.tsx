import { Link, useNavigate } from "react-router-dom";
import {
  Bank,
  Gavel,
  GearSix,
  type Icon,
  Money,
  MusicNotes,
  SignOut,
  Storefront,
  UsersThree,
  Warning,
} from "@phosphor-icons/react";
import { clearSession } from "../api/client";
import BrandMark from "./BrandMark";

// Shared shell for every Admin Console screen, ported from the Stitch-generated "Pending Venues"
// screen's sidebar (the first Admin screen built) -- every later Admin screen should render this
// instead of re-pasting the sidebar markup, same pattern as Owner's <OwnerHeader>.
//
// Icons are Phosphor components, not Material Symbols string names -- swapped 2026-08-17 (see
// docs/stitch/Stitch-Master-Brief.md Design Execution Principles #8): Material Symbols is Stitch's own
// default icon set, an instant "AI-generated" tell regardless of how the rest of the screen looks.
const NAV_GROUPS = [
  {
    label: "Duyệt nội dung",
    items: [
      { label: "Phòng trà", icon: Storefront, to: "/admin/venues/pending" },
      { label: "Kiểm duyệt Show", icon: MusicNotes, to: "/admin/shows/moderation" },
    ],
  },
  {
    label: "Xử phạt",
    items: [{ label: "Xử phạt & Kháng cáo", icon: Gavel, to: "/admin/penalties" }],
  },
  {
    label: "Tài chính",
    items: [
      { label: "Hoàn tiền", icon: Money, to: "/admin/refunds" },
      { label: "Xác minh tài khoản ngân hàng", icon: Bank, to: "/admin/bank-accounts" },
    ],
  },
  {
    label: "Khác",
    items: [
      { label: "Khiếu nại", icon: Warning, to: "/admin/complaints" },
      { label: "Người dùng", icon: UsersThree, to: "/admin/users" },
      { label: "Cấu hình nền tảng", icon: GearSix, to: "/admin/platform-settings" },
    ],
  },
] as const satisfies { label: string; items: { label: string; icon: Icon; to: string }[] }[];

export default function AdminSidebar({ active }: { active: string }) {
  const navigate = useNavigate();

  function handleLogout() {
    clearSession();
    navigate("/login");
  }

  return (
    <aside className="w-72 fixed left-0 top-0 bg-surface-container-low h-screen flex flex-col py-md px-sm border-r border-outline-variant/20 z-50">
      <div className="px-4 mb-8">
        <h1 className="flex items-center gap-2 font-headline-sm text-headline-sm font-semibold text-primary">
          <BrandMark className="text-primary/70 shrink-0" />
          MusicLounge{" "}
          <span className="text-xs uppercase tracking-wider text-on-surface-variant ml-1 font-body-md">Admin</span>
        </h1>
      </div>
      <nav className="flex-1 overflow-y-auto pr-2 space-y-6">
        {NAV_GROUPS.map((group) => (
          <div key={group.label}>
            <h2 className="px-4 text-xs font-semibold text-on-surface-variant uppercase tracking-wider mb-2 font-label-md">
              {group.label}
            </h2>
            <ul className="space-y-1">
              {group.items.map((item) => {
                const ItemIcon = item.icon;
                const isActive = item.label === active;
                return (
                  <li key={item.label}>
                    <Link
                      className={
                        isActive
                          ? "flex items-center gap-3 px-4 py-3 rounded-lg bg-primary-container text-on-primary-container font-semibold transition-transform duration-200"
                          : "flex items-center gap-3 px-4 py-3 rounded-lg text-on-surface-variant hover:bg-surface-variant/50 transition-colors"
                      }
                      to={item.to}
                    >
                      <ItemIcon weight={isActive ? "fill" : "light"} size={20} />
                      {item.label}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </nav>
      <div className="pt-4 border-t border-outline-variant/20 mt-4">
        <button
          onClick={handleLogout}
          className="w-full flex items-center gap-3 px-4 py-3 rounded-lg text-on-surface-variant hover:bg-surface-variant/50 transition-colors"
        >
          <SignOut weight="light" size={20} />
          Đăng xuất
        </button>
      </div>
    </aside>
  );
}
