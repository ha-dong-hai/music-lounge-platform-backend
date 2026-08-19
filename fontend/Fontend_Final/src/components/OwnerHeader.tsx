import { Link, useNavigate } from "react-router-dom";
import { List, SignOut, User } from "@phosphor-icons/react";
import { clearSession, getStoredUser } from "../api/client";
import BrandMark from "./BrandMark";

// Extracted from OwnerMyVenues.tsx (the first Owner screen ported from Stitch) once a second
// Owner screen needed the exact same header -- every subsequent Owner Dashboard screen should
// render this instead of re-pasting the header markup.
//
// "Settings" points at /owner/subscription, not the /owner/settings ComingSoon stub -- Subscription
// is the one real Settings-adjacent screen built so far (billing/plan management conventionally
// lives under Settings in SaaS products), so it gets the real nav slot instead of a dead end.
const NAV_ITEMS = [
  { label: "Venues", to: "/owner/venues" },
  { label: "Shows", to: "/owner/shows" },
  { label: "Finance", to: "/owner/finance" },
  { label: "Settings", to: "/owner/subscription" },
] as const;

export default function OwnerHeader({ active }: { active: (typeof NAV_ITEMS)[number]["label"] }) {
  const navigate = useNavigate();
  const user = getStoredUser();

  function handleLogout() {
    clearSession();
    navigate("/login");
  }

  return (
    <header className="w-full bg-surface/80 backdrop-blur-md sticky top-0 z-50 border-b border-outline-variant/10 px-margin-mobile md:px-lg py-4 flex justify-between items-center">
      <div className="flex items-center gap-12">
        <h1 className="flex items-center gap-2.5 text-headline-sm text-primary tracking-tight m-0 font-display-lg">
          <BrandMark className="text-primary/70 shrink-0" />
          MusicLounge{" "}
          <span className="font-label-md text-[10px] text-secondary tracking-[0.4em] uppercase ml-2 hidden md:inline-block relative top-0 opacity-60">
            Atelier
          </span>
        </h1>
        <nav className="hidden md:flex gap-8 items-center pt-1">
          {NAV_ITEMS.map((item) =>
            item.label === active ? (
              <Link
                key={item.label}
                className="relative font-label-md text-[11px] text-primary uppercase tracking-[0.4em] font-semibold py-2"
                to={item.to}
              >
                {item.label}
                <span className="absolute bottom-0 left-0 w-full h-[1px] bg-primary" />
              </Link>
            ) : (
              <Link
                key={item.label}
                className="relative font-label-md text-[11px] text-on-surface-variant hover:text-on-surface uppercase tracking-[0.4em] py-2 transition-colors"
                to={item.to}
              >
                {item.label}
              </Link>
            )
          )}
        </nav>
      </div>
      <button className="md:hidden text-on-surface-variant" aria-label="Menu">
        <List weight="light" size={24} />
      </button>
      <button
        onClick={handleLogout}
        className="hidden md:flex items-center gap-4 cursor-pointer group"
        aria-label="Đăng xuất"
      >
        <div className="text-right">
          <p className="font-body-md text-sm text-on-surface group-hover:text-primary transition-colors">
            {user?.fullName ?? "Chủ phòng trà"}
          </p>
          <p className="font-label-md text-[10px] text-on-surface-variant uppercase tracking-widest">
            {user?.role ?? "Owner"}
          </p>
        </div>
        <div className="w-10 h-10 rounded-full border border-outline-variant/30 bg-surface-container flex items-center justify-center">
          <User weight="light" size={20} className="text-on-surface-variant" />
        </div>
        <SignOut weight="light" size={20} className="text-on-surface-variant group-hover:text-primary transition-colors" />
      </button>
    </header>
  );
}
