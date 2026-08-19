import { Link, useNavigate } from "react-router-dom";
import { MagnifyingGlass } from "@phosphor-icons/react";
import { clearSession, getStoredToken, getStoredUser } from "../api/client";

// Shared shell for the Audience/Guest surface -- extracted 2026-08-18 after 5 pages
// (DiscoverShows, ShowDetail, TicketPurchase, MyTickets, TicketDetail) had each grown their own
// ad-hoc header with no way to navigate between them except typing a URL. Owner/Admin already had
// this (OwnerHeader.tsx, AdminSidebar.tsx); Audience didn't.
//
// Nav item set deliberately does NOT copy either Stitch mock's own nav verbatim -- checked against
// real comparable platforms first (fetched live): Ticketbox.vn (Search/Tạo sự kiện/Vé của tôi/Đăng
// nhập), Eventbrite (Find Events/Find my tickets/Sign in), Songkick (Concerts/Artists/Festivals --
// artist-centric, doesn't fit: Performer has no login/account in this system, nothing to "follow"
// as its own entity). "Livestream" and "Transparency" (donation public feed) are real planned
// features but belong as contextual links once those pages exist, not primary nav slots no
// comparable product elevates that high.
//
// 3rd item ("Sự kiện") added 2026-08-18 once DiscoveryHub.tsx (from
// auralounge_desktop_discovery_hub) and DiscoverShows.tsx (from auralounge_event_directory) became
// two separate real pages instead of one merged page -- user explicitly required them kept distinct
// ("là 2 view rõ ràng cho tôi không gộp"), so both need their own primary nav slot.
const NAV_ITEMS = [
  { label: "Khám phá", to: "/discover" },
  { label: "Sự kiện", to: "/shows" },
  { label: "Vé của tôi", to: "/me/tickets" },
] as const;

export default function AudienceHeader({ active }: { active?: (typeof NAV_ITEMS)[number]["label"] }) {
  const navigate = useNavigate();
  const token = getStoredToken();
  const user = getStoredUser();

  function handleLogout() {
    clearSession();
    navigate("/login");
  }

  return (
    <header
      className="sticky top-0 z-50 bg-[#fbf9f4]/90 backdrop-blur-md border-b border-[#1b1c19]/10"
      style={{ fontFamily: "'Manrope', sans-serif" }}
    >
      <div className="max-w-[1440px] mx-auto flex items-center justify-between px-6 md:px-16 h-20">
        <div className="flex items-center gap-12">
          <Link to="/discover" className="text-2xl tracking-tight text-[#181512]" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
            MusicLounge
          </Link>
          <nav className="hidden md:flex items-center gap-8">
            {NAV_ITEMS.map((item) => (
              <Link
                key={item.label}
                to={item.to}
                className={`text-xs tracking-[0.1em] uppercase pb-1 transition-colors ${
                  item.label === active
                    ? "text-[#181512] border-b-2 border-[#181512] font-semibold"
                    : "text-[#7e756f] hover:text-[#181512]"
                }`}
              >
                {item.label}
              </Link>
            ))}
          </nav>
        </div>

        <div className="flex items-center gap-4">
          <Link to="/shows" aria-label="Tìm kiếm show" className="hidden md:flex text-[#4d4540] hover:text-[#181512] transition-colors">
            <MagnifyingGlass size={20} />
          </Link>
          {token ? (
            <button onClick={handleLogout} className="flex items-center gap-2 group" aria-label="Đăng xuất">
              <span className="text-sm text-[#1b1c19] group-hover:text-[#ba1a1a] transition-colors">
                {user?.fullName ?? "Tài khoản"}
              </span>
            </button>
          ) : (
            <Link
              to="/login"
              className="text-sm px-5 py-2.5 rounded-lg bg-[#181512] text-white hover:bg-[#181512]/90 transition-colors"
            >
              Đăng nhập
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}
