import { useNavigate } from "react-router-dom";
import { SignOut, User } from "@phosphor-icons/react";
import { clearSession, getStoredUser } from "../api/client";
import BrandMark from "./BrandMark";

// First Staff-facing screen in this app -- no shell existed yet. Reuses the same "Warm Luxury
// Lounge" system as Owner/Admin (tailwind.config.ts) rather than the Audience/Guest AuraLounge
// system, since Staff is an internal-operations role like Owner/Admin, not a guest-facing one.
//
// Only 1 real nav destination exists so far (Box Office) -- not inventing "Venue Operations" /
// "Guest Relations" tabs from the mock's nav since neither has a page behind it yet, same
// discipline as AudienceHeader's nav research earlier this session.
export default function StaffHeader({ active }: { active: string }) {
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
          MusicLounge
          <span className="font-label-md text-[10px] text-secondary tracking-[0.4em] uppercase ml-2 hidden md:inline-block relative top-0 opacity-60">
            Box Office
          </span>
        </h1>
        <nav className="hidden md:flex gap-8 items-center pt-1">
          <span className="relative font-label-md text-[11px] text-primary uppercase tracking-[0.4em] font-semibold py-2">
            {active}
            <span className="absolute bottom-0 left-0 w-full h-[1px] bg-primary" />
          </span>
        </nav>
      </div>
      <button
        onClick={handleLogout}
        className="flex items-center gap-4 cursor-pointer group"
        aria-label="Đăng xuất"
      >
        <div className="text-right hidden sm:block">
          <p className="font-body-md text-sm text-on-surface group-hover:text-primary transition-colors">
            {user?.fullName ?? "Nhân viên"}
          </p>
          <p className="font-label-md text-[10px] text-on-surface-variant uppercase tracking-widest">Staff</p>
        </div>
        <div className="w-10 h-10 rounded-full border border-outline-variant/30 bg-surface-container flex items-center justify-center">
          <User weight="light" size={20} className="text-on-surface-variant" />
        </div>
        <SignOut weight="light" size={20} className="text-on-surface-variant group-hover:text-primary transition-colors" />
      </button>
    </header>
  );
}
