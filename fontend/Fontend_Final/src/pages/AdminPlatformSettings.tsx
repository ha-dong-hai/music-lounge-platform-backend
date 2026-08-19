import { FormEvent, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { animate, stagger } from "animejs";
import { Bell, Check, PencilSimple, Plus, TrashSimple, User, WarningCircle, X } from "@phosphor-icons/react";
import { ApiError, getStoredToken, getStoredUser } from "../api/client";
import {
  createTaxonomyTerm,
  deleteTaxonomyTerm,
  FilterOptionsDto,
  getFilterOptions,
  TaxonomyKind,
  updateTaxonomyTerm,
} from "../api/taxonomy";
import AdminSidebar from "../components/AdminSidebar";

// Ported 2026-08-18 from `admin_platform_taxonomy_management` in the AuraLounge Stitch export,
// restyled onto this project's tokens.
//
// Two honest limitations are surfaced in the UI instead of being papered over, both caused by the
// read endpoint (GET /lounge-shows/filter-options) returning less than the write endpoints accept:
//
//   * Renaming a GENRE also submits `nameEn`, but GenreDto has no nameEn to pre-fill from -- so the
//     English name is shown as an empty field with an explicit "will be overwritten" warning rather
//     than silently blanked.
//   * Renaming a CATEGORY also submits `description`, with the same problem. `isActive` is NOT a
//     guess though: GetFilterOptionsQueryHandler filters `c => c.IsActive`, so every category
//     reachable from this screen is active by construction, and true is the correct value.
//
// A consequence worth knowing: because that same filter hides inactive categories, a category
// deactivated by any other means becomes invisible here and cannot be reactivated from this screen.
// Deactivation is therefore deliberately not offered as an action -- only create, rename, delete.
//
// Delete answers 409 when the term is still in use; that message is surfaced verbatim rather than
// being reworded, since it is the single most useful thing an Admin can be told at that moment.

const TABS: { kind: TaxonomyKind; label: string; hasSecondary: boolean; secondaryLabel?: string }[] = [
  { kind: "categories", label: "Danh mục", hasSecondary: true, secondaryLabel: "Mô tả" },
  { kind: "genres", label: "Thể loại nhạc", hasSecondary: true, secondaryLabel: "Tên tiếng Anh" },
  { kind: "moods", label: "Tâm trạng", hasSecondary: false },
  { kind: "atmospheres", label: "Không khí", hasSecondary: false },
];

interface Term {
  id: number;
  name: string;
}

export default function AdminPlatformSettings() {
  const navigate = useNavigate();
  const [options, setOptions] = useState<FilterOptionsDto | null>(null);
  const [activeKind, setActiveKind] = useState<TaxonomyKind>("categories");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [newName, setNewName] = useState("");
  const [newSecondary, setNewSecondary] = useState("");
  const [creating, setCreating] = useState(false);

  const [editingId, setEditingId] = useState<number | null>(null);
  const [editName, setEditName] = useState("");
  const [editSecondary, setEditSecondary] = useState("");
  const [busyId, setBusyId] = useState<number | null>(null);
  const animatedTabs = useRef<Set<string>>(new Set());

  const tab = TABS.find((t) => t.kind === activeKind)!;

  function reload(): Promise<void> {
    return getFilterOptions()
      .then((result) => setOptions(result))
      .catch((err) => {
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
  }

  useEffect(() => {
    const token = getStoredToken();
    const user = getStoredUser();
    if (!token || user?.role !== "Admin") {
      navigate("/login");
      return;
    }
    let cancelled = false;
    getFilterOptions()
      .then((result) => {
        if (!cancelled) setOptions(result);
      })
      .catch((err) => {
        if (cancelled) return;
        setErrorMessage(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
      });
    return () => {
      cancelled = true;
    };
  }, [navigate]);

  const terms: Term[] = options
    ? activeKind === "categories"
      ? options.categories
      : activeKind === "genres"
        ? options.genres
        : activeKind === "moods"
          ? options.moods
          : options.atmospheres
    : [];

  useEffect(() => {
    if (options && terms.length > 0 && !animatedTabs.current.has(activeKind)) {
      animatedTabs.current.add(activeKind);
      animate(".taxonomy-row", {
        opacity: [0, 1],
        translateY: [10, 0],
        delay: stagger(35),
        duration: 350,
        ease: "outQuad",
      });
    }
  }, [options, activeKind, terms.length]);

  function switchTab(kind: TaxonomyKind) {
    setActiveKind(kind);
    setEditingId(null);
    setNewName("");
    setNewSecondary("");
    setActionError(null);
    setNotice(null);
  }

  // Categories and genres carry a second field the read endpoint cannot give back, so submitting a
  // rename necessarily rewrites it. Only send it for the kinds whose contract actually has one.
  function secondaryPayload(value: string): { description?: string | null; nameEn?: string | null } {
    const trimmed = value.trim();
    if (activeKind === "categories") return { description: trimmed === "" ? null : trimmed };
    if (activeKind === "genres") return { nameEn: trimmed === "" ? null : trimmed };
    return {};
  }

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    const name = newName.trim();
    if (name === "" || creating) return;
    setCreating(true);
    setActionError(null);
    setNotice(null);
    try {
      await createTaxonomyTerm(activeKind, { name, ...secondaryPayload(newSecondary) });
      setNewName("");
      setNewSecondary("");
      await reload();
      setNotice(`Đã thêm "${name}".`);
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setCreating(false);
    }
  }

  function startEdit(t: Term) {
    setEditingId(t.id);
    setEditName(t.name);
    // Intentionally blank: the read endpoint returns no secondary value to restore, and pre-filling
    // it with the primary name would be worse than showing it empty.
    setEditSecondary("");
    setActionError(null);
    setNotice(null);
  }

  async function handleUpdate(t: Term) {
    const name = editName.trim();
    if (name === "") {
      setActionError("Tên không được để trống.");
      return;
    }
    setBusyId(t.id);
    setActionError(null);
    try {
      await updateTaxonomyTerm(activeKind, t.id, {
        name,
        ...secondaryPayload(editSecondary),
        // Every category visible here is active by construction (see the file header), so this is
        // a derived fact, not an assumption.
        ...(activeKind === "categories" ? { isActive: true } : {}),
      });
      setEditingId(null);
      await reload();
      setNotice(`Đã cập nhật "${name}".`);
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyId(null);
    }
  }

  async function handleDelete(t: Term) {
    setBusyId(t.id);
    setActionError(null);
    setNotice(null);
    try {
      await deleteTaxonomyTerm(activeKind, t.id);
      await reload();
      setNotice(`Đã xóa "${t.name}".`);
    } catch (err) {
      // A 409 here means "still in use" -- the server's own wording is more specific than anything
      // this screen could invent, so it is shown as-is.
      setActionError(err instanceof ApiError ? err.message : "Không thể kết nối tới máy chủ.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="bg-surface text-on-surface font-body-md min-h-screen flex">
      <AdminSidebar active="Cấu hình nền tảng" />

      <main className="ml-72 flex-1 flex flex-col min-h-screen">
        <header className="bg-surface border-b border-outline-variant/20 h-20 px-xl flex justify-between items-center sticky top-0 z-40">
          <div className="flex-1" />
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
            <h1 className="font-headline-md text-headline-md text-on-surface mb-2">Cấu hình nền tảng</h1>
            <p className="font-body-md text-body-md text-on-surface-variant">
              Danh mục, thể loại, tâm trạng và không khí dùng chung cho toàn hệ thống.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2 mb-lg border-b border-outline-variant/20 pb-4">
            {TABS.map((t) => (
              <button
                key={t.kind}
                onClick={() => switchTab(t.kind)}
                className={`px-5 py-2.5 rounded-full font-label-md text-label-md border transition-colors ${
                  activeKind === t.kind
                    ? "border-primary bg-primary text-on-primary"
                    : "border-outline-variant bg-surface-container-low text-on-surface-variant hover:border-primary hover:text-primary"
                }`}
              >
                {t.label}
                {options && (
                  <span className="ml-2 opacity-70">
                    {t.kind === "categories"
                      ? options.categories.length
                      : t.kind === "genres"
                        ? options.genres.length
                        : t.kind === "moods"
                          ? options.moods.length
                          : options.atmospheres.length}
                  </span>
                )}
              </button>
            ))}
          </div>

          {errorMessage && (
            <div role="alert" aria-live="assertive" className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 mb-6">
              {errorMessage}
            </div>
          )}
          {actionError && (
            <div role="alert" aria-live="assertive" className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3 mb-6">
              {actionError}
            </div>
          )}
          {notice && (
            <div role="status" aria-live="polite" className="rounded-xl bg-secondary-container text-on-secondary-container text-body-md font-body-md px-4 py-3 mb-6">
              {notice}
            </div>
          )}

          {/* Create */}
          <form
            onSubmit={handleCreate}
            className="bg-surface-container-low border border-outline-variant/30 rounded-xl p-md mb-lg flex flex-col md:flex-row md:items-end gap-md"
          >
            <div className="flex-1">
              <label className="block font-label-md text-label-md text-on-surface-variant uppercase tracking-widest mb-2" htmlFor="new-name">
                Thêm {tab.label.toLowerCase()}
              </label>
              <input
                id="new-name"
                type="text"
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder={`Tên ${tab.label.toLowerCase()}...`}
                className="w-full bg-surface border border-outline-variant rounded-lg px-4 py-3 text-on-surface font-body-md focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all placeholder:text-on-surface-variant/50"
              />
            </div>
            {tab.hasSecondary && (
              <div className="flex-1">
                <label className="block font-label-md text-label-md text-on-surface-variant uppercase tracking-widest mb-2" htmlFor="new-secondary">
                  {tab.secondaryLabel} (tùy chọn)
                </label>
                <input
                  id="new-secondary"
                  type="text"
                  value={newSecondary}
                  onChange={(e) => setNewSecondary(e.target.value)}
                  className="w-full bg-surface border border-outline-variant rounded-lg px-4 py-3 text-on-surface font-body-md focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all"
                />
              </div>
            )}
            <button
              type="submit"
              disabled={newName.trim() === "" || creating}
              className="inline-flex items-center justify-center gap-2 px-6 py-3 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-40 disabled:cursor-not-allowed shrink-0"
            >
              <Plus weight="bold" size={16} />
              {creating ? "Đang thêm..." : "Thêm"}
            </button>
          </form>

          {options === null && !errorMessage && (
            <p className="font-body-md text-body-md text-on-surface-variant text-center py-24">Đang tải...</p>
          )}

          {options !== null && terms.length === 0 && (
            <p className="font-body-md text-body-md text-on-surface-variant text-center py-24">
              Chưa có {tab.label.toLowerCase()} nào.
            </p>
          )}

          <div className="border-t border-outline-variant/20">
            {terms.map((t) => (
              <div key={t.id} className="taxonomy-row border-b border-outline-variant/20 py-4">
                {editingId === t.id ? (
                  <div className="flex flex-col gap-3">
                    <div className="flex flex-col md:flex-row md:items-center gap-3">
                      <input
                        type="text"
                        value={editName}
                        onChange={(e) => setEditName(e.target.value)}
                        className="flex-1 bg-surface border border-outline-variant rounded-lg px-4 py-2.5 text-on-surface font-body-md focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all"
                      />
                      {tab.hasSecondary && (
                        <input
                          type="text"
                          value={editSecondary}
                          onChange={(e) => setEditSecondary(e.target.value)}
                          placeholder={tab.secondaryLabel}
                          className="flex-1 bg-surface border border-outline-variant rounded-lg px-4 py-2.5 text-on-surface font-body-md focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all"
                        />
                      )}
                      <div className="flex items-center gap-2 shrink-0">
                        <button
                          onClick={() => handleUpdate(t)}
                          disabled={busyId === t.id}
                          className="inline-flex items-center gap-1.5 px-4 py-2.5 rounded-lg bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors disabled:opacity-50"
                        >
                          <Check weight="bold" size={15} />
                          Lưu
                        </button>
                        <button
                          onClick={() => setEditingId(null)}
                          className="inline-flex items-center gap-1.5 px-4 py-2.5 rounded-lg border border-outline text-on-surface-variant font-label-md text-label-md hover:bg-surface-variant transition-colors"
                        >
                          <X weight="bold" size={15} />
                          Hủy
                        </button>
                      </div>
                    </div>
                    {tab.hasSecondary && (
                      <p className="flex items-start gap-2 text-[12px] text-on-surface-variant">
                        <WarningCircle weight="fill" size={14} className="shrink-0 mt-0.5 text-secondary" />
                        <span>
                          API không trả về “{tab.secondaryLabel?.toLowerCase()}” hiện tại nên không thể điền sẵn — lưu
                          sẽ ghi đè giá trị cũ bằng nội dung ô này (để trống = xóa).
                        </span>
                      </p>
                    )}
                  </div>
                ) : (
                  <div className="flex items-center justify-between gap-4">
                    <span className="font-body-md text-body-md text-on-surface">{t.name}</span>
                    <div className="flex items-center gap-1 shrink-0">
                      <button
                        onClick={() => startEdit(t)}
                        disabled={busyId === t.id}
                        aria-label={`Sửa ${t.name}`}
                        className="p-2 rounded-full text-on-surface-variant hover:text-primary hover:bg-surface-variant/60 transition-colors disabled:opacity-50"
                      >
                        <PencilSimple weight="light" size={18} />
                      </button>
                      <button
                        onClick={() => handleDelete(t)}
                        disabled={busyId === t.id}
                        aria-label={`Xóa ${t.name}`}
                        className="p-2 rounded-full text-on-surface-variant hover:text-error hover:bg-error-container transition-colors disabled:opacity-50"
                      >
                        <TrashSimple weight="light" size={18} />
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      </main>
    </div>
  );
}
