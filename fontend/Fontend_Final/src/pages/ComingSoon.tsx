import { Link, useLocation } from "react-router-dom";

// Shared placeholder for routes that exist in the nav/link graph but don't have a real screen
// yet (Create/Edit Venue, Venue Analytics, Shows, Finance, Settings...) -- same "chưa được thiết
// kế" honesty as Login.tsx's inline post-login placeholder, just reusable across many entry
// points instead of duplicating that block. Never silently 404 or leave a dead link.
export default function ComingSoon() {
  const location = useLocation();

  return (
    <div className="bg-surface min-h-screen flex items-center justify-center px-margin-mobile text-on-surface">
      <div className="max-w-[420px] w-full text-center">
        <span className="font-display-lg text-headline-sm text-primary tracking-tight">MusicLounge</span>
        <h1 className="font-headline-md text-headline-md text-on-surface mt-lg mb-2">Sắp ra mắt</h1>
        <p className="font-body-md text-body-md text-on-surface-variant mb-8">
          Màn hình này ({location.pathname}) chưa được thiết kế trong lượt này.
        </p>
        <Link
          to="/owner/venues"
          className="inline-block px-6 py-3 bg-primary text-on-primary font-label-md text-label-md rounded-xl hover:opacity-90 transition-opacity"
        >
          Quay lại Venue của tôi
        </Link>
      </div>
    </div>
  );
}
