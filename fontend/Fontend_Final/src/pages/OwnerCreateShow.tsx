import { FormEvent, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { animate } from "animejs";
import {
  Armchair,
  ArrowLeft,
  Broadcast,
  Buildings,
  CaretDown,
  Check,
  DotsSixVertical,
  Lock,
  MagnifyingGlass,
  Plus,
  TrashSimple,
} from "@phosphor-icons/react";
import { ApiError, getStoredToken } from "../api/client";
import { getMyLounges, LoungeListItem } from "../api/lounges";
import { getFilterOptions, FilterOptionsDto } from "../api/taxonomy";
import { getMySubscription, MySubscription } from "../api/subscriptions";
import { createLoungeShow, LoungeShowFormat, PerformerRole } from "../api/shows";
import { searchPerformers, Performer } from "../api/performers";
import OwnerHeader from "../components/OwnerHeader";

// Step 1 ported from a Stitch MCP generation (docs/stitch/Stitch-Master-Brief.md "Create / Edit Show",
// step 1 of 3). The original design had 3 format cards (Offline/Online/Hybrid); Hybrid was removed
// via a real Stitch edit_screens call after research (Eventbrite/Luma/Zoom Events) confirmed no
// comparable platform treats "hybrid" as a genuine upfront choice -- it's always composed from two
// binary ticket-access types, matching TicketTier.AccessType (Physical/Livestream) exactly. Backend
// enforces this too now (LoungeShowFormat has only Offline/Online; a ticket tier's AccessType must
// match its show's Format).
//
// Step 2 (Lịch trình & Sức chứa) ported 2026-08-17 from a fresh Stitch generation found in the
// canonical project (screen "Tạo show mới (Lịch trình & Sức chứa)") -- written as one complete
// one-shot prompt per feedback_stitch_one_shot_prompting, not iterated via edit_screens.
//
// Step 3 (Line-up nghệ sĩ) ported 2026-08-17 from the same canonical project (screen "Tạo show mới
// (Line-up nghệ sĩ)"), also a single one-shot generation. Two copies landed in the project because
// the generate call timed out client-side twice while actually succeeding server-side both times
// (a known Stitch MCP quirk -- the tool's own instructions say never retry a timed-out generate,
// poll list_screens/get_screen instead); the richer of the two was kept as canonical, the other is
// an inert leftover in the Stitch project. Real search wired to GET /performers?search= (already
// built for exactly this -- see api/performers.ts), and picking "+ Tạo nghệ sĩ mới" adds a lineup
// row with performerId: null / performerName: <query>, which CreateLoungeShowCommand's validator
// treats as "create a new catalog performer on submit" (exactly one of the two must be set). Drag
// reorder uses plain HTML5 drag-and-drop (draggable + onDragStart/onDragOver/onDrop) rather than a
// library, since sorting one flat list doesn't need one. Submitting this step is what actually calls
// POST /lounge-shows now (CreateShowRequest has everything all three steps collect, with a real
// performances array built from the lineup instead of the placeholder `[]` step 2 used to submit).

const isActiveSubscription = (sub: MySubscription | null): boolean =>
  sub !== null && sub.status === "Active" && new Date(sub.expiresAt) > new Date();

// Default "next Friday at 20:00" -- shows need lead time for VenueApproval + ticket sales (an
// Owner rarely opens a show same-day), and 20:00 on a weekend evening is the most common live-music
// slot. Mirrors the "next round hour" convention Google/Outlook Calendar use for generic events,
// adapted to this domain's actual lead-time and evening-show shape rather than copied verbatim.
function defaultShowStart(): string {
  const d = new Date();
  const daysUntilFriday = (5 - d.getDay() + 7) % 7 || 7; // always the *next* Friday, never today
  d.setDate(d.getDate() + daysUntilFriday);
  d.setHours(20, 0, 0, 0);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function OwnerCreateShow() {
  const navigate = useNavigate();
  const formCardRef = useRef<HTMLDivElement>(null);
  const hasAnimatedEntrance = useRef(false);

  const [lounges, setLounges] = useState<LoungeListItem[] | null>(null);
  const [filterOptions, setFilterOptions] = useState<FilterOptionsDto | null>(null);
  const [subscription, setSubscription] = useState<MySubscription | null | undefined>(undefined);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [wizardStep, setWizardStep] = useState<1 | 2 | 3>(1);
  const [loungeId, setLoungeId] = useState<number | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [format, setFormat] = useState<LoungeShowFormat>("Offline");
  const [categoryId, setCategoryId] = useState<number | null>(null);
  const [genreIds, setGenreIds] = useState<number[]>([]);

  // Step 2 fields. scheduledStart starts pre-filled (see defaultShowStart below) rather than "" --
  // an empty datetime-local input renders the browser's raw "yyyy-mm-dd --:-- --" placeholder,
  // which reads as broken UI rather than an empty required field. scheduledEnd is intentionally
  // left blank (optional, explained by its own helper text below) -- that's a deliberate different
  // state, not the same bug.
  const [scheduledStart, setScheduledStart] = useState(defaultShowStart);
  const [scheduledEnd, setScheduledEnd] = useState("");
  const [offlineQuota, setOfflineQuota] = useState("");
  const [onlineQuota, setOnlineQuota] = useState("");
  const [submitting, setSubmitting] = useState(false);

  // Step 3 fields. Each lineup row carries its own display fields (name/avatar) alongside the
  // PerformanceInput fields the submit payload actually needs -- performerId stays null for a
  // freshly-typed name (backend creates the catalog performer on submit), non-null once picked
  // from search results. orderIndex is derived from array position at submit time, not stored here,
  // since drag-reorder already keeps the array itself in the right order.
  interface LineupRow {
    performerId: number | null;
    performerName: string;
    avatarUrl: string | null;
    role: PerformerRole;
    setTime: string; // "HH:mm" or "" (unset)
    acceptsDonation: boolean;
  }
  const [lineup, setLineup] = useState<LineupRow[]>([]);
  const [performerQuery, setPerformerQuery] = useState("");
  const [performerResults, setPerformerResults] = useState<Performer[]>([]);
  const [searchingPerformers, setSearchingPerformers] = useState(false);
  const dragIndexRef = useRef<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  // Debounced live search against the shared performer catalog -- 300ms matches the settle time
  // used for other search-as-you-type inputs in this codebase, short enough to feel instant without
  // firing a request per keystroke.
  useEffect(() => {
    const query = performerQuery.trim();
    if (query.length === 0) {
      setPerformerResults([]);
      setSearchingPerformers(false);
      return;
    }
    setSearchingPerformers(true);
    const timer = setTimeout(() => {
      searchPerformers(query)
        .then((result) => setPerformerResults(result.items))
        .catch(() => setPerformerResults([]))
        .finally(() => setSearchingPerformers(false));
    }, 300);
    return () => clearTimeout(timer);
  }, [performerQuery]);

  function addExistingPerformer(p: Performer) {
    setLineup((prev) => [
      ...prev,
      { performerId: p.id, performerName: p.name, avatarUrl: p.avatarUrl, role: "Guest", setTime: "", acceptsDonation: false },
    ]);
    setPerformerQuery("");
    setPerformerResults([]);
  }

  function addNewPerformer(name: string) {
    const trimmed = name.trim();
    if (trimmed.length === 0) return;
    setLineup((prev) => [
      ...prev,
      { performerId: null, performerName: trimmed, avatarUrl: null, role: "Guest", setTime: "", acceptsDonation: false },
    ]);
    setPerformerQuery("");
    setPerformerResults([]);
  }

  function removeLineupRow(index: number) {
    setLineup((prev) => prev.filter((_, i) => i !== index));
  }

  function updateLineupRow(index: number, patch: Partial<LineupRow>) {
    setLineup((prev) => prev.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  function handleDrop(targetIndex: number) {
    const sourceIndex = dragIndexRef.current;
    dragIndexRef.current = null;
    setDragOverIndex(null);
    if (sourceIndex === null || sourceIndex === targetIndex) return;
    setLineup((prev) => {
      const next = [...prev];
      const [moved] = next.splice(sourceIndex, 1);
      next.splice(targetIndex, 0, moved);
      return next;
    });
  }

  const [step3Error, setStep3Error] = useState<string | null>(null);

  // Format is strictly Offline XOR Online (no Hybrid -- see LoungeShowFormat comment in api/shows.ts),
  // so only one quota channel is ever sellable per show. Clear the inapplicable one whenever the
  // Owner changes format on step 1, so a stale value never gets silently submitted alongside it.
  useEffect(() => {
    if (format === "Offline") setOnlineQuota("");
    else setOfflineQuota("");
  }, [format]);

  useEffect(() => {
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }
    let cancelled = false;
    Promise.all([getMyLounges(), getFilterOptions(), getMySubscription()])
      .then(([loungeResult, options, mySub]) => {
        if (cancelled) return;
        setLounges(loungeResult.items);
        setFilterOptions(options);
        setSubscription(mySub);
        if (loungeResult.items.length > 0) setLoungeId(loungeResult.items[0].id);
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
    return () => {
      cancelled = true;
    };
  }, [navigate]);

  useEffect(() => {
    if (lounges && filterOptions && formCardRef.current && !hasAnimatedEntrance.current) {
      hasAnimatedEntrance.current = true;
      animate(formCardRef.current, {
        opacity: [0, 1],
        translateY: [16, 0],
        duration: 500,
        ease: "outQuad",
      });
    }
  }, [lounges, filterOptions]);

  function toggleGenre(id: number) {
    setGenreIds((prev) => (prev.includes(id) ? prev.filter((g) => g !== id) : [...prev, id]));
  }

  const canContinue = loungeId !== null && name.trim().length > 0 && description.trim().length > 0;
  const subscriptionActive = subscription === undefined ? true : isActiveSubscription(subscription);

  // datetime-local gives "YYYY-MM-DDTHH:mm" with no offset -- new Date() parses that as local time,
  // which is correct here since the Owner is picking a wall-clock time in their own timezone.
  const canSubmitStep2 = scheduledStart !== "" && new Date(scheduledStart).getTime() > Date.now();

  function handleSubmitStep2(e: FormEvent) {
    e.preventDefault();
    if (!canSubmitStep2) return;
    setWizardStep(3);
  }

  async function handleSubmitStep3(e: FormEvent) {
    e.preventDefault();
    if (!canSubmitStep2 || loungeId === null || submitting) return;
    setStep3Error(null);
    setSubmitting(true);
    try {
      const newShowId = await createLoungeShow({
        loungeId,
        name: name.trim(),
        description: description.trim(),
        format,
        scheduledStart: new Date(scheduledStart).toISOString(),
        scheduledEnd: scheduledEnd ? new Date(scheduledEnd).toISOString() : null,
        categoryId,
        offlineQuota: offlineQuota === "" ? null : Number(offlineQuota),
        onlineQuota: onlineQuota === "" ? null : Number(onlineQuota),
        genreIds,
        performances: lineup.map((row, index) => ({
          performerId: row.performerId,
          performerName: row.performerId === null ? row.performerName : null,
          role: row.role,
          orderIndex: index,
          setTime: row.setTime === "" ? null : row.setTime,
          acceptsDonation: row.acceptsDonation,
        })),
        customValues: [],
      });
      navigate(`/owner/shows?created=${newShowId}`);
    } catch (err) {
      setStep3Error(err instanceof ApiError ? err.message : "Không thể tạo show, vui lòng thử lại.");
    } finally {
      setSubmitting(false);
    }
  }

  const isLoading = lounges === null || filterOptions === null;

  return (
    <div className="bg-surface text-on-background min-h-screen flex flex-col">
      <OwnerHeader active="Shows" />

      <main className="flex-grow flex flex-col items-center py-xl px-margin-mobile md:px-lg max-w-[800px] mx-auto w-full">
        <div className="w-full mb-lg text-center">
          <div className="inline-flex items-center justify-center gap-sm mb-md">
            <h1 className="font-headline-md text-headline-md md:font-display-lg md:text-display-lg text-on-background">
              Tạo show mới
            </h1>
            {wizardStep === 1 && (
              <span className="bg-surface-container-high text-on-surface-variant font-label-md text-label-md px-3 py-1 rounded-full border border-outline/20">
                Đã lưu nháp
              </span>
            )}
          </div>

          <div className="flex items-center justify-center w-full max-w-2xl mx-auto mt-lg">
            <div className="flex flex-col items-center relative z-10">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center font-label-md text-label-md mb-xs ring-4 ring-background ${
                  wizardStep === 1
                    ? "bg-primary text-on-primary shadow-md"
                    : "bg-surface-container-highest text-on-surface-variant shadow-sm"
                }`}
              >
                1
              </div>
              <span
                className={`font-label-md text-label-md whitespace-nowrap hidden sm:block ${
                  wizardStep === 1 ? "text-primary" : "text-on-surface-variant opacity-60"
                }`}
              >
                Thông tin cơ bản
              </span>
            </div>
            <div className={`flex-grow h-[2px] mx-xs sm:mx-md relative -top-3 ${wizardStep >= 2 ? "bg-primary" : "bg-surface-container-highest"}`} />
            <div className="flex flex-col items-center relative z-10">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center font-label-md text-label-md mb-xs ring-4 ring-background ${
                  wizardStep >= 2
                    ? "bg-primary text-on-primary shadow-md"
                    : "bg-surface-container-highest text-on-surface-variant shadow-sm"
                }`}
              >
                {wizardStep > 2 ? <Check weight="bold" size={16} /> : 2}
              </div>
              <span className={`font-label-md text-label-md whitespace-nowrap hidden sm:block ${wizardStep >= 2 ? "text-primary" : "text-on-surface-variant opacity-60"}`}>
                Lịch trình &amp; Sức chứa
              </span>
            </div>
            <div className={`flex-grow h-[2px] mx-xs sm:mx-md relative -top-3 ${wizardStep === 3 ? "bg-primary" : "bg-surface-container-highest"}`} />
            <div className="flex flex-col items-center relative z-10">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center font-label-md text-label-md mb-xs ring-4 ring-background ${
                  wizardStep === 3
                    ? "bg-primary text-on-primary shadow-md"
                    : "bg-surface-container-highest text-on-surface-variant shadow-sm"
                }`}
              >
                3
              </div>
              <span className={`font-label-md text-label-md whitespace-nowrap hidden sm:block ${wizardStep === 3 ? "text-primary" : "text-on-surface-variant opacity-60"}`}>
                Line-up nghệ sĩ
              </span>
            </div>
          </div>
        </div>

        {errorMessage && (
          <div role="alert" aria-live="assertive" className="w-full rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 mb-lg">
            {errorMessage}
          </div>
        )}

        {subscription !== undefined && !subscriptionActive && (
          <div className="w-full rounded-xl bg-secondary-container text-on-secondary-container text-body-md font-body-md px-4 py-3 mb-lg flex items-center gap-3">
            <Lock weight="bold" size={18} className="shrink-0" />
            Bạn cần có gói subscription đang hoạt động để tạo show mới.{" "}
            <button onClick={() => navigate("/owner/subscription")} className="underline font-semibold shrink-0">
              Đăng ký ngay
            </button>
          </div>
        )}

        {isLoading && !errorMessage && (
          <p className="font-body-md text-body-md text-on-surface-variant py-24 text-center">Đang tải...</p>
        )}

        {!isLoading && wizardStep === 1 && (
          <div
            ref={formCardRef}
            className="w-full bg-surface-container-lowest rounded-xl shadow-[0_8px_32px_rgba(43,42,39,0.06)] p-md md:p-xl border border-outline/10 relative overflow-hidden opacity-0"
          >
            <div className="absolute inset-0 bg-gradient-to-br from-surface-bright to-transparent opacity-50 pointer-events-none" />
            <div className="flex flex-col gap-xl relative z-10">
              <div className="flex flex-col gap-md">
                <div className="flex flex-col gap-xs">
                  <label className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest" htmlFor="venue">
                    Phòng trà
                  </label>
                  <div className="relative">
                    <select
                      id="venue"
                      value={loungeId ?? ""}
                      onChange={(e) => setLoungeId(Number(e.target.value))}
                      className="w-full bg-surface-container-low border border-outline-variant text-on-surface font-body-md text-body-md rounded-lg px-md py-3 appearance-none focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all cursor-pointer"
                    >
                      {lounges!.length === 0 && <option value="">Bạn chưa có phòng trà nào</option>}
                      {lounges!.map((l) => (
                        <option key={l.id} value={l.id}>
                          {l.name} — {l.district}, {l.city}
                        </option>
                      ))}
                    </select>
                    <CaretDown weight="bold" size={16} className="absolute right-md top-1/2 -translate-y-1/2 text-on-surface-variant pointer-events-none" />
                  </div>
                </div>
                <div className="flex flex-col gap-xs">
                  <label className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest" htmlFor="show_name">
                    Tên show
                  </label>
                  <input
                    id="show_name"
                    type="text"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="Nhập tên show diễn..."
                    className="w-full bg-surface-container-low border border-outline-variant text-on-surface font-body-md text-body-md rounded-lg px-md py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all placeholder:text-on-surface-variant/50"
                  />
                </div>
              </div>

              <div className="flex flex-col gap-xs">
                <label className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest" htmlFor="description">
                  Mô tả
                </label>
                <textarea
                  id="description"
                  rows={4}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Giới thiệu về đêm diễn, không gian, cảm hứng..."
                  className="w-full bg-surface-container-low border border-outline-variant text-on-surface font-body-md text-body-md rounded-lg px-md py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all resize-none placeholder:text-on-surface-variant/50"
                />
              </div>

              <div className="flex flex-col gap-md">
                <label className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest">Hình thức</label>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-sm">
                  <button
                    type="button"
                    onClick={() => setFormat("Offline")}
                    className={`relative flex flex-col items-center justify-center p-md rounded-xl cursor-pointer transition-all ${
                      format === "Offline"
                        ? "border-2 border-primary bg-surface-container-low shadow-[0_4px_12px_rgba(140,74,30,0.1)]"
                        : "border border-outline-variant bg-surface-container-lowest hover:bg-surface-container-low"
                    }`}
                  >
                    <Buildings weight={format === "Offline" ? "fill" : "light"} size={32} className={format === "Offline" ? "text-primary mb-xs" : "text-on-surface-variant mb-xs"} />
                    <span className="font-title-lg text-title-lg text-on-surface">Trực tiếp</span>
                    <span className="font-body-md text-body-md text-on-surface-variant text-center mt-1 text-sm">Tổ chức tại phòng trà</span>
                    {format === "Offline" && (
                      <div className="absolute top-sm right-sm w-5 h-5 rounded-full bg-primary flex items-center justify-center text-on-primary">
                        <Check weight="bold" size={12} />
                      </div>
                    )}
                  </button>
                  <button
                    type="button"
                    onClick={() => setFormat("Online")}
                    className={`relative flex flex-col items-center justify-center p-md rounded-xl cursor-pointer transition-all ${
                      format === "Online"
                        ? "border-2 border-primary bg-surface-container-low shadow-[0_4px_12px_rgba(140,74,30,0.1)]"
                        : "border border-outline-variant bg-surface-container-lowest hover:bg-surface-container-low"
                    }`}
                  >
                    <Broadcast weight={format === "Online" ? "fill" : "light"} size={32} className={format === "Online" ? "text-primary mb-xs" : "text-on-surface-variant mb-xs"} />
                    <span className="font-title-lg text-title-lg text-on-surface">Trực tuyến</span>
                    <span className="font-body-md text-body-md text-on-surface-variant text-center mt-1 text-sm">Livestream độc quyền</span>
                    {format === "Online" && (
                      <div className="absolute top-sm right-sm w-5 h-5 rounded-full bg-primary flex items-center justify-center text-on-primary">
                        <Check weight="bold" size={12} />
                      </div>
                    )}
                  </button>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-xl">
                <div className="flex flex-col gap-xs">
                  <label className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest" htmlFor="category">
                    Danh mục
                  </label>
                  <div className="relative">
                    <select
                      id="category"
                      value={categoryId ?? ""}
                      onChange={(e) => setCategoryId(e.target.value ? Number(e.target.value) : null)}
                      className="w-full bg-surface-container-low border border-outline-variant text-on-surface font-body-md text-body-md rounded-lg px-md py-3 appearance-none focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all cursor-pointer"
                    >
                      <option value="">Chọn danh mục...</option>
                      {filterOptions!.categories.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.name}
                        </option>
                      ))}
                    </select>
                    <CaretDown weight="bold" size={16} className="absolute right-md top-1/2 -translate-y-1/2 text-on-surface-variant pointer-events-none" />
                  </div>
                </div>
                <div className="flex flex-col gap-xs">
                  <label className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest">Thể loại nhạc</label>
                  <div className="flex flex-wrap gap-2">
                    {filterOptions!.genres.map((g) => (
                      <button
                        key={g.id}
                        type="button"
                        onClick={() => toggleGenre(g.id)}
                        className={`px-4 py-2 rounded-full border font-label-md text-label-md transition-colors ${
                          genreIds.includes(g.id)
                            ? "border-primary bg-primary text-on-primary shadow-sm"
                            : "border-outline-variant bg-surface-container-low text-on-surface-variant hover:border-primary hover:text-primary"
                        }`}
                      >
                        {g.name}
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              <div className="flex justify-between items-center mt-md pt-lg border-t border-outline/10">
                <button
                  type="button"
                  onClick={() => navigate("/owner/shows")}
                  className="px-6 py-3 rounded-xl border border-outline text-on-surface font-label-md text-label-md hover:bg-surface-container-low transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  disabled={!canContinue || !subscriptionActive}
                  onClick={() => setWizardStep(2)}
                  className="px-8 py-3 rounded-xl bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors disabled:opacity-40 disabled:cursor-not-allowed shadow-[0_4px_16px_rgba(201,123,74,0.3)]"
                >
                  Tiếp tục
                </button>
              </div>
            </div>
          </div>
        )}

        {!isLoading && wizardStep === 2 && (
          <form
            onSubmit={handleSubmitStep2}
            className="w-full bg-surface-container-lowest rounded-xl shadow-[0_8px_32px_rgba(43,42,39,0.06)] p-md md:p-xl border border-outline/10 flex flex-col gap-lg"
          >
            {/* Schedule */}
            <section className="flex flex-col gap-md">
              <h2 className="font-title-lg text-title-lg text-on-surface">Lịch trình</h2>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-md">
                <div className="flex flex-col gap-xs">
                  <label className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest" htmlFor="start-time">
                    Thời gian bắt đầu <span className="text-error">*</span>
                  </label>
                  <input
                    id="start-time"
                    type="datetime-local"
                    required
                    value={scheduledStart}
                    onChange={(e) => setScheduledStart(e.target.value)}
                    className="w-full bg-surface-container-low border border-outline-variant text-on-surface font-body-md text-body-md rounded-lg px-md py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all"
                  />
                </div>
                <div className="flex flex-col gap-xs">
                  <label className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest" htmlFor="end-time">
                    Thời gian kết thúc
                  </label>
                  <input
                    id="end-time"
                    type="datetime-local"
                    value={scheduledEnd}
                    onChange={(e) => setScheduledEnd(e.target.value)}
                    className="w-full bg-surface-container-low border border-outline-variant text-on-surface font-body-md text-body-md rounded-lg px-md py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all"
                  />
                  <span className="text-[12px] text-on-surface-variant italic">Để trống nếu chưa xác định</span>
                </div>
              </div>
            </section>

            <hr className="border-t border-outline-variant/30" />

            {/* Capacity -- only the channel matching the chosen Format (step 1) can sell tickets
                (TicketTier.AccessType must match LoungeShowFormat on the backend), so only that
                one quota field is shown; the other is cleared via the format-change effect above. */}
            <section className="flex flex-col gap-md">
              <h2 className="font-title-lg text-title-lg text-on-surface">Sức chứa (Quota)</h2>
              <div className="grid grid-cols-1 gap-md">
                {format === "Offline" && (
                  <div className="flex flex-col gap-xs">
                    <label className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest" htmlFor="offline-quota">
                      Sức chứa tại phòng trà
                    </label>
                    <div className="relative">
                      <Armchair weight="light" size={18} className="absolute left-md top-1/2 -translate-y-1/2 text-on-surface-variant" />
                      <input
                        id="offline-quota"
                        type="number"
                        min={0}
                        placeholder="0"
                        value={offlineQuota}
                        onChange={(e) => setOfflineQuota(e.target.value)}
                        className="w-full bg-surface-container-low border border-outline-variant text-on-surface font-body-md text-body-md rounded-lg pl-11 pr-md py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all"
                      />
                    </div>
                    <span className="text-[12px] text-on-surface-variant">Số lượng khách tối đa dựa trên mặt bằng thực tế.</span>
                  </div>
                )}
                {format === "Online" && (
                  <div className="flex flex-col gap-xs">
                    <label className="font-label-md text-label-md text-on-surface-variant uppercase tracking-widest" htmlFor="online-quota">
                      Sức chứa online (Livestream)
                    </label>
                    <div className="relative">
                      <Broadcast weight="light" size={18} className="absolute left-md top-1/2 -translate-y-1/2 text-on-surface-variant" />
                      <input
                        id="online-quota"
                        type="number"
                        min={0}
                        placeholder="0"
                        value={onlineQuota}
                        onChange={(e) => setOnlineQuota(e.target.value)}
                        className="w-full bg-surface-container-low border border-outline-variant text-on-surface font-body-md text-body-md rounded-lg pl-11 pr-md py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all"
                      />
                    </div>
                    <span className="text-[12px] text-on-surface-variant">Số lượng người xem tối đa cho luồng livestream.</span>
                  </div>
                )}
              </div>
            </section>

            <div className="flex justify-between items-center mt-md pt-lg border-t border-outline/10">
              <button
                type="button"
                onClick={() => setWizardStep(1)}
                className="inline-flex items-center gap-xs px-6 py-3 rounded-xl border border-outline text-on-surface font-label-md text-label-md hover:bg-surface-container-low transition-colors"
              >
                <ArrowLeft weight="bold" size={16} />
                Quay lại
              </button>
              <button
                type="submit"
                disabled={!canSubmitStep2}
                className="px-8 py-3 rounded-xl bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors disabled:opacity-40 disabled:cursor-not-allowed shadow-[0_4px_16px_rgba(201,123,74,0.3)]"
              >
                Tiếp tục
              </button>
            </div>
          </form>
        )}

        {!isLoading && wizardStep === 3 && (
          <form
            onSubmit={handleSubmitStep3}
            className="w-full bg-surface-container-lowest rounded-xl shadow-[0_8px_32px_rgba(43,42,39,0.06)] p-md md:p-xl border border-outline/10 flex flex-col gap-lg"
          >
            {step3Error && (
              <div role="alert" aria-live="assertive" className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3">
                {step3Error}
              </div>
            )}

            <p className="font-body-md text-body-md text-on-surface-variant">
              Thêm nghệ sĩ biểu diễn cho show của bạn. Bạn có thể bỏ qua bước này và thêm sau.
            </p>

            <div className="grid grid-cols-1 lg:grid-cols-12 gap-lg">
              {/* Search & add */}
              <div className="lg:col-span-5">
                <div className="relative">
                  <MagnifyingGlass weight="regular" size={18} className="absolute left-md top-1/2 -translate-y-1/2 text-on-surface-variant" />
                  <input
                    type="text"
                    value={performerQuery}
                    onChange={(e) => setPerformerQuery(e.target.value)}
                    placeholder="Tìm nghệ sĩ theo tên..."
                    className="w-full bg-surface-container-low border border-outline-variant text-on-surface font-body-md text-body-md rounded-lg pl-11 pr-md py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all placeholder:text-on-surface-variant/50"
                  />
                </div>
                {performerQuery.trim() !== "" && (
                  <div className="mt-xs bg-surface-container-lowest border border-outline-variant rounded-xl shadow-[0_12px_32px_rgba(43,42,39,0.12)] overflow-hidden">
                    {searchingPerformers && (
                      <p className="px-md py-sm font-body-md text-body-md text-on-surface-variant text-sm">Đang tìm...</p>
                    )}
                    {!searchingPerformers && (
                      <ul>
                        {performerResults.map((p) => (
                          <li key={p.id}>
                            <button
                              type="button"
                              onClick={() => addExistingPerformer(p)}
                              className="w-full px-md py-sm flex items-center gap-3 hover:bg-surface-container-low transition-colors text-left"
                            >
                              <div className="w-10 h-10 rounded-full bg-surface-container overflow-hidden shrink-0">
                                {p.avatarUrl && <img src={p.avatarUrl} alt={p.name} className="w-full h-full object-cover" />}
                              </div>
                              <div className="flex-grow min-w-0">
                                <p className="font-body-md text-body-md font-medium text-on-background truncate">{p.name}</p>
                                {p.genreNames.length > 0 && (
                                  <p className="font-body-md text-sm text-on-surface-variant truncate">{p.genreNames.join(", ")}</p>
                                )}
                              </div>
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}
                    <div className="border-t border-outline-variant p-2">
                      <button
                        type="button"
                        onClick={() => addNewPerformer(performerQuery)}
                        className="w-full px-md py-sm flex items-center gap-2 text-primary font-label-md text-label-md hover:bg-surface-container-low rounded-lg transition-colors"
                      >
                        <Plus weight="bold" size={16} />
                        Tạo nghệ sĩ mới "{performerQuery.trim()}"
                      </button>
                    </div>
                  </div>
                )}
              </div>

              {/* Lineup list */}
              <div className="lg:col-span-7 flex flex-col">
                <div className="flex items-center justify-between mb-xs">
                  <h2 className="font-title-lg text-title-lg text-on-surface">Danh sách biểu diễn</h2>
                  <span className="bg-surface-container-high text-on-surface-variant font-label-md text-sm px-3 py-1 rounded-full">
                    {lineup.length} nghệ sĩ
                  </span>
                </div>
                {lineup.length > 0 && (
                  <p className="font-body-md text-sm text-on-surface-variant italic mb-sm">Kéo thả để sắp xếp thứ tự biểu diễn.</p>
                )}
                {lineup.length === 0 && (
                  <p className="font-body-md text-body-md text-on-surface-variant py-lg text-center border border-dashed border-outline-variant rounded-xl">
                    Chưa có nghệ sĩ nào. Tìm và thêm nghệ sĩ ở bên trái.
                  </p>
                )}
                <div className="flex flex-col gap-xs">
                  {lineup.map((row, index) => (
                    <div
                      key={index}
                      draggable
                      onDragStart={() => {
                        dragIndexRef.current = index;
                      }}
                      onDragOver={(e) => {
                        e.preventDefault();
                        setDragOverIndex(index);
                      }}
                      onDragLeave={() => setDragOverIndex((prev) => (prev === index ? null : prev))}
                      onDrop={() => handleDrop(index)}
                      className={`bg-surface-container-lowest p-3 rounded-xl border shadow-sm flex items-center gap-3 transition-colors ${
                        dragOverIndex === index ? "border-primary" : "border-outline-variant"
                      }`}
                    >
                      <span className="text-on-surface-variant/50 cursor-grab active:cursor-grabbing">
                        <DotsSixVertical weight="bold" size={18} />
                      </span>
                      <div className="w-10 h-10 rounded-full bg-surface-container overflow-hidden shrink-0">
                        {row.avatarUrl && <img src={row.avatarUrl} alt={row.performerName} className="w-full h-full object-cover" />}
                      </div>
                      <div className="flex-grow min-w-0 flex flex-col gap-1">
                        <div className="flex items-center gap-2 flex-wrap">
                          <p className="font-body-md text-body-md font-semibold text-on-background truncate">{row.performerName}</p>
                          <select
                            value={row.role}
                            onChange={(e) => updateLineupRow(index, { role: e.target.value as PerformerRole })}
                            className="bg-surface-container-low border border-outline-variant text-on-surface-variant font-label-md text-xs px-2 py-0.5 rounded-sm focus:outline-none focus:ring-1 focus:ring-primary"
                          >
                            <option value="Main">Nghệ sĩ chính</option>
                            <option value="Guest">Khách mời</option>
                            <option value="Host">MC / Host</option>
                          </select>
                        </div>
                        <div className="flex items-center gap-md text-sm text-on-surface-variant flex-wrap">
                          <input
                            type="time"
                            value={row.setTime}
                            onChange={(e) => updateLineupRow(index, { setTime: e.target.value })}
                            className="bg-transparent border-none p-0 focus:ring-0 font-body-md text-sm cursor-pointer hover:text-primary transition-colors"
                          />
                          <label className="flex items-center gap-2 cursor-pointer">
                            <input
                              type="checkbox"
                              checked={row.acceptsDonation}
                              onChange={(e) => updateLineupRow(index, { acceptsDonation: e.target.checked })}
                              className="sr-only peer"
                            />
                            <span className="block w-9 h-5 rounded-full bg-surface-variant border border-outline peer-checked:bg-primary peer-checked:border-primary transition-colors relative">
                              <span className="absolute left-0.5 top-0.5 bg-white w-3.5 h-3.5 rounded-full transition-transform peer-checked:translate-x-4" />
                            </span>
                            <span className="font-body-md text-sm">Nhận donation</span>
                          </label>
                        </div>
                      </div>
                      <button
                        type="button"
                        onClick={() => removeLineupRow(index)}
                        className="p-2 text-on-surface-variant/50 hover:text-error hover:bg-error-container rounded-full transition-colors shrink-0"
                      >
                        <TrashSimple weight="regular" size={18} />
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            <div className="flex justify-between items-center mt-md pt-lg border-t border-outline/10">
              <button
                type="button"
                onClick={() => setWizardStep(2)}
                className="inline-flex items-center gap-xs px-6 py-3 rounded-xl border border-outline text-on-surface font-label-md text-label-md hover:bg-surface-container-low transition-colors"
              >
                <ArrowLeft weight="bold" size={16} />
                Quay lại
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="inline-flex items-center gap-xs px-8 py-3 rounded-xl bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors disabled:opacity-40 disabled:cursor-not-allowed shadow-[0_4px_16px_rgba(201,123,74,0.3)]"
              >
                <Check weight="bold" size={16} />
                {submitting ? "Đang tạo show..." : "Tạo show"}
              </button>
            </div>
          </form>
        )}
      </main>
    </div>
  );
}
