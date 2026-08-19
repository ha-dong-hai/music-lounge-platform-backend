// Shared footer for DiscoverShows/MyTickets -- both auralounge_event_directory and
// auralounge_my_resonances_tickets end in a real footer (brand + tagline + copyright + a link
// row: About/Privacy/Terms/Support/Press/Careers), but none of those link targets exist as real
// pages in this app. Porting the brand/tagline/copyright block literally and dropping the link
// row is a deliberate choice, not an oversight -- fabricating nav to pages that 404 would be worse
// than a shorter footer.
export default function AudienceFooter() {
  return (
    <footer className="bg-[#f0eee9] border-t border-[#cfc4bd]/40 w-full">
      <div className="max-w-[1440px] mx-auto px-6 md:px-16 py-10 flex flex-col md:flex-row md:items-center md:justify-between gap-3">
        <span className="text-2xl tracking-tight text-[#181512]" style={{ fontFamily: "'Libre Caslon Text', serif" }}>
          MusicLounge
        </span>
        <p className="text-sm text-[#1b1c19]">Không gian âm nhạc sang trọng, tĩnh lặng. © 2026 MusicLounge.</p>
      </div>
    </footer>
  );
}
