import { ChangeEvent, FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Camera, CubeTransparent, type Icon, Info, UploadSimple } from "@phosphor-icons/react";
import { ApiError, getStoredToken } from "../api/client";
import {
  createLounge,
  setLoungeBusinessLicense,
  setLoungeImage,
  setLoungeModel3D,
  uploadLoungeImage,
  uploadLoungeModel3D,
} from "../api/lounges";
import { AtmosphereDto, getFilterOptions } from "../api/taxonomy";
import OwnerHeader from "../components/OwnerHeader";

// Ported from a screen generated live via the Stitch MCP connection (prompted with this design
// system + the real form fields CreateLoungeCommand/CreateLoungeCommandValidator.cs actually take),
// not hand-designed here -- see docs/stitch/Stitch-Master-Brief.md "Create / Edit Venue". The generated
// header used a different placeholder brand ("LoungeSync") and nav items that don't exist as real
// routes (Dashboard/Analytics) -- swapped for the real <OwnerHeader> already used by My Venues
// instead of porting a second, inconsistent header.
//
// Real backend gap found while wiring this: CreateLoungeCommand has no photo/license/3D-model
// fields at all -- those only attach via separate PUT /lounges/{id}/image|business-license|model-3d
// calls once the lounge (and its id) already exists, so venue creation is genuinely a multi-step
// sequence, not one atomic submit. If a later upload step fails after the lounge itself was already
// created, this still treats it as an overall success (the venue is real and Pending either way) --
// just flags which attachment didn't make it, since there's no Edit Venue screen yet to retry from.
//
// Also found: POST /uploads/images only accepts jpg/jpeg/png/webp/gif (LocalFileStorageService),
// no PDF -- the Stitch mock's "PDF hoặc Ảnh" copy for the business license uploader was corrected
// to what the backend actually accepts, a real-world gap worth flagging (business licenses are
// commonly PDF) rather than silently fixing by expanding backend file-type support unasked.

const DRAFT_KEY = "musiclounge_owner_create_venue_draft";

interface DraftFields {
  name: string;
  description: string;
  atmosphereId: string;
  street: string;
  ward: string;
  district: string;
  city: string;
}

const EMPTY_DRAFT: DraftFields = {
  name: "",
  description: "",
  atmosphereId: "",
  street: "",
  ward: "",
  district: "",
  city: "",
};

function loadDraft(): DraftFields {
  try {
    const raw = localStorage.getItem(DRAFT_KEY);
    if (!raw) return EMPTY_DRAFT;
    return { ...EMPTY_DRAFT, ...JSON.parse(raw) };
  } catch {
    return EMPTY_DRAFT;
  }
}

function FileDropZone({
  file,
  onChange,
  accept,
  hint,
}: {
  file: File | null;
  onChange: (f: File | null) => void;
  accept: string;
  hint: string;
}) {
  return (
    <label className="border-2 border-dashed border-outline-variant rounded-xl p-8 flex flex-col items-center justify-center gap-4 bg-surface-container-low hover:bg-surface-container-highest transition-colors cursor-pointer group">
      <input
        type="file"
        accept={accept}
        className="hidden"
        onChange={(e: ChangeEvent<HTMLInputElement>) => onChange(e.target.files?.[0] ?? null)}
      />
      <div className="w-16 h-16 rounded-full bg-surface flex items-center justify-center text-primary group-hover:scale-110 transition-transform">
        <Camera weight="light" size={32} />
      </div>
      <div className="text-center">
        <p className="font-body-md text-body-md text-on-surface">
          {file ? file.name : "Kéo thả hoặc bấm để chọn ảnh"}
        </p>
        <p className="text-label-md font-label-md text-on-surface-variant mt-1">{hint}</p>
      </div>
    </label>
  );
}

function FileRow({
  file,
  onChange,
  accept,
  icon: RowIcon,
  title,
  hint,
}: {
  file: File | null;
  onChange: (f: File | null) => void;
  accept: string;
  icon: Icon;
  title: string;
  hint: string;
}) {
  return (
    <label className="border border-outline-variant rounded-xl p-4 flex items-center gap-4 bg-surface hover:bg-surface-container transition-colors cursor-pointer">
      <input
        type="file"
        accept={accept}
        className="hidden"
        onChange={(e: ChangeEvent<HTMLInputElement>) => onChange(e.target.files?.[0] ?? null)}
      />
      <RowIcon weight="light" size={20} className="text-outline shrink-0" />
      <div className="flex-grow min-w-0">
        <p className="font-label-md text-label-md text-on-surface truncate">{file ? file.name : title}</p>
        <p className="text-[12px] text-on-surface-variant">{hint}</p>
      </div>
    </label>
  );
}

export default function OwnerCreateVenue() {
  const navigate = useNavigate();
  const [fields, setFields] = useState<DraftFields>(loadDraft);
  const [atmospheres, setAtmospheres] = useState<AtmosphereDto[]>([]);
  const [primaryImageFile, setPrimaryImageFile] = useState<File | null>(null);
  const [businessLicenseFile, setBusinessLicenseFile] = useState<File | null>(null);
  const [model3dFile, setModel3dFile] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [generalError, setGeneralError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [draftSavedAt, setDraftSavedAt] = useState<number | null>(null);
  const [attachmentWarning, setAttachmentWarning] = useState<string | null>(null);

  useEffect(() => {
    if (!getStoredToken()) {
      navigate("/login");
      return;
    }
    getFilterOptions()
      .then((opts) => setAtmospheres(opts.atmospheres))
      .catch(() => {
        // Non-critical: the form still works with the atmosphere dropdown just empty/optional.
      });
  }, [navigate]);

  function setField<K extends keyof DraftFields>(key: K, value: DraftFields[K]) {
    setFields((f) => ({ ...f, [key]: value }));
  }

  function fieldError(name: string): string | null {
    const key = Object.keys(fieldErrors).find((k) => k.toLowerCase() === name.toLowerCase());
    return key ? fieldErrors[key][0] : null;
  }

  function handleSaveDraft() {
    localStorage.setItem(DRAFT_KEY, JSON.stringify(fields));
    setDraftSavedAt(Date.now());
    setTimeout(() => setDraftSavedAt(null), 3000);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setGeneralError(null);
    setFieldErrors({});
    setAttachmentWarning(null);
    setSubmitting(true);

    try {
      const id = await createLounge({
        name: fields.name,
        description: fields.description || null,
        atmosphereId: fields.atmosphereId ? Number(fields.atmosphereId) : null,
        street: fields.street,
        ward: fields.ward,
        district: fields.district,
        city: fields.city,
        latitude: null,
        longitude: null,
      });

      // Venue exists from here on -- any failure below is an attachment problem, not a creation
      // failure. Best-effort, sequential (each needs its own upload first).
      const failedAttachments: string[] = [];
      if (primaryImageFile) {
        try {
          const { url } = await uploadLoungeImage(primaryImageFile);
          await setLoungeImage(id, url);
        } catch {
          failedAttachments.push("ảnh đại diện");
        }
      }
      if (businessLicenseFile) {
        try {
          const { url } = await uploadLoungeImage(businessLicenseFile);
          await setLoungeBusinessLicense(id, url);
        } catch {
          failedAttachments.push("giấy phép kinh doanh");
        }
      }
      if (model3dFile) {
        try {
          const { url } = await uploadLoungeModel3D(model3dFile);
          await setLoungeModel3D(id, url);
        } catch {
          failedAttachments.push("mô hình 3D");
        }
      }

      localStorage.removeItem(DRAFT_KEY);
      if (failedAttachments.length > 0) {
        setAttachmentWarning(
          `Phòng trà đã được tạo, nhưng tải lên thất bại: ${failedAttachments.join(", ")}. Bạn có thể thử lại sau khi có màn hình chỉnh sửa.`
        );
        // Give the user a moment to read the warning before leaving.
        setTimeout(() => navigate("/owner/venues"), 3500);
      } else {
        navigate("/owner/venues");
      }
    } catch (err) {
      if (err instanceof ApiError) {
        setGeneralError(err.fieldErrors ? null : err.message);
        setFieldErrors(err.fieldErrors ?? {});
      } else {
        setGeneralError("Không thể kết nối tới máy chủ. Vui lòng thử lại.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="text-on-background min-h-screen flex flex-col selection:bg-primary selection:text-on-primary font-body-md text-body-md bg-surface-container-low">
      <OwnerHeader active="Venues" />

      <main className="flex-grow w-full py-xl px-margin-mobile md:px-gutter flex justify-center">
        <div className="w-full max-w-[800px] flex flex-col gap-10">
          <div className="text-center md:text-left flex flex-col gap-3">
            <span className="text-label-md font-label-md text-secondary tracking-widest uppercase">
              Curated Spaces
            </span>
            <h1 className="font-display-lg text-display-lg-mobile md:text-display-lg text-on-surface">
              Tạo phòng trà mới
            </h1>
            <div className="mt-4 flex items-start gap-3 p-4 bg-surface-container rounded-xl border border-outline-variant/30">
              <Info weight="light" size={20} className="text-primary mt-0.5 shrink-0" />
              <p className="text-body-md font-body-md text-on-surface-variant">
                Sau khi tạo, phòng trà sẽ ở trạng thái <strong className="text-on-surface">Chờ duyệt</strong> — bạn
                cần đợi Admin phê duyệt trước khi có thể tạo show.
              </p>
            </div>
          </div>

          {generalError && (
            <div
              role="alert"
              aria-live="assertive"
              className="rounded-xl bg-error-container text-on-error-container text-body-md font-body-md px-4 py-3"
            >
              {generalError}
            </div>
          )}
          {attachmentWarning && (
            <div
              role="status"
              className="rounded-xl bg-secondary-container text-on-secondary-container text-body-md font-body-md px-4 py-3"
            >
              {attachmentWarning}
            </div>
          )}

          <form className="flex flex-col gap-10" onSubmit={handleSubmit} noValidate>
            <section className="bg-white rounded-2xl p-6 md:p-8 shadow-[0_8px_24px_rgba(43,42,39,0.04)] border border-outline-variant/20">
              <h2 className="font-headline-sm text-title-lg text-on-surface mb-6 border-b border-surface-container-highest pb-4">
                1. Thông tin cơ bản
              </h2>
              <div className="flex flex-col gap-6">
                <div className="flex flex-col gap-2">
                  <label className="font-label-md text-label-md text-on-surface" htmlFor="venue-name">
                    Tên phòng trà <span className="text-error">*</span>
                  </label>
                  <input
                    id="venue-name"
                    type="text"
                    required
                    maxLength={255}
                    placeholder="VD: The Velvet Room"
                    className="w-full bg-surface text-on-surface border border-outline-variant rounded-xl px-4 py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-colors text-body-md"
                    value={fields.name}
                    onChange={(e) => setField("name", e.target.value)}
                  />
                  {fieldError("name") && <p className="text-body-md text-error">{fieldError("name")}</p>}
                </div>
                <div className="flex flex-col gap-2">
                  <label className="font-label-md text-label-md text-on-surface" htmlFor="venue-desc">
                    Mô tả
                  </label>
                  <textarea
                    id="venue-desc"
                    rows={4}
                    maxLength={2000}
                    placeholder="Giới thiệu không gian, phong cách âm nhạc..."
                    className="w-full bg-surface text-on-surface border border-outline-variant rounded-xl px-4 py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-colors text-body-md resize-none"
                    value={fields.description}
                    onChange={(e) => setField("description", e.target.value)}
                  />
                </div>
                <div className="flex flex-col gap-2">
                  <label className="font-label-md text-label-md text-on-surface" htmlFor="venue-vibe">
                    Không khí
                  </label>
                  <select
                    id="venue-vibe"
                    className="w-full bg-surface text-on-surface border border-outline-variant rounded-xl px-4 py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-colors text-body-md"
                    value={fields.atmosphereId}
                    onChange={(e) => setField("atmosphereId", e.target.value)}
                  >
                    <option value="">Chọn không khí phù hợp (tuỳ chọn)</option>
                    {atmospheres.map((a) => (
                      <option key={a.id} value={a.id}>
                        {a.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </section>

            {/* Deliberately borderless, unlike sections 1 & 3 -- a repeated white-card-per-section
                is the exact "AI dashboard template" pattern being avoided here (see
                docs/stitch/Stitch-Master-Brief.md Design Execution Principles #8). A top-rule divider is
                enough grouping for a form section; not every block needs card chrome. */}
            <section className="border-t border-outline-variant/40 pt-10">
              <h2 className="font-headline-sm text-title-lg text-on-surface mb-6">2. Địa chỉ</h2>
              <div className="flex flex-col gap-6">
                <div className="flex flex-col gap-2">
                  <label className="font-label-md text-label-md text-on-surface" htmlFor="venue-street">
                    Số nhà, đường <span className="text-error">*</span>
                  </label>
                  <input
                    id="venue-street"
                    type="text"
                    required
                    maxLength={255}
                    placeholder="VD: 123 Đường Nam Kỳ Khởi Nghĩa"
                    className="w-full bg-surface text-on-surface border border-outline-variant rounded-xl px-4 py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-colors text-body-md"
                    value={fields.street}
                    onChange={(e) => setField("street", e.target.value)}
                  />
                  {fieldError("street") && <p className="text-body-md text-error">{fieldError("street")}</p>}
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div className="flex flex-col gap-2">
                    <label className="font-label-md text-label-md text-on-surface" htmlFor="venue-ward">
                      Phường/Xã
                    </label>
                    <input
                      id="venue-ward"
                      type="text"
                      maxLength={100}
                      placeholder="VD: Phường Bến Nghé"
                      className="w-full bg-surface text-on-surface border border-outline-variant rounded-xl px-4 py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-colors text-body-md"
                      value={fields.ward}
                      onChange={(e) => setField("ward", e.target.value)}
                    />
                  </div>
                  <div className="flex flex-col gap-2">
                    <label className="font-label-md text-label-md text-on-surface" htmlFor="venue-district">
                      Quận/Huyện
                    </label>
                    <input
                      id="venue-district"
                      type="text"
                      maxLength={100}
                      placeholder="VD: Quận 1"
                      className="w-full bg-surface text-on-surface border border-outline-variant rounded-xl px-4 py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-colors text-body-md"
                      value={fields.district}
                      onChange={(e) => setField("district", e.target.value)}
                    />
                  </div>
                  <div className="flex flex-col gap-2 md:col-span-2">
                    <label className="font-label-md text-label-md text-on-surface" htmlFor="venue-city">
                      Thành phố <span className="text-error">*</span>
                    </label>
                    <input
                      id="venue-city"
                      type="text"
                      required
                      maxLength={100}
                      placeholder="VD: TP. Hồ Chí Minh"
                      className="w-full bg-surface text-on-surface border border-outline-variant rounded-xl px-4 py-3 focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-colors text-body-md"
                      value={fields.city}
                      onChange={(e) => setField("city", e.target.value)}
                    />
                    {fieldError("city") && <p className="text-body-md text-error">{fieldError("city")}</p>}
                  </div>
                </div>
              </div>
            </section>

            <section className="bg-white rounded-2xl p-6 md:p-8 shadow-[0_8px_24px_rgba(43,42,39,0.04)] border border-outline-variant/20">
              <h2 className="font-headline-sm text-title-lg text-on-surface mb-6 border-b border-surface-container-highest pb-4">
                3. Hình ảnh &amp; Tài liệu
              </h2>
              <div className="flex flex-col gap-8">
                <div className="flex flex-col gap-3">
                  <label className="font-label-md text-label-md text-on-surface">Ảnh đại diện phòng trà</label>
                  <FileDropZone
                    file={primaryImageFile}
                    onChange={setPrimaryImageFile}
                    accept="image/jpeg,image/png,image/webp,image/gif"
                    hint="JPG, PNG, WebP, GIF (tối đa 5MB)"
                  />
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div className="flex flex-col gap-3">
                    <label className="font-label-md text-label-md text-on-surface">Giấy phép kinh doanh</label>
                    <FileRow
                      file={businessLicenseFile}
                      onChange={setBusinessLicenseFile}
                      accept="image/jpeg,image/png,image/webp,image/gif"
                      icon={UploadSimple}
                      title="Tải lên tài liệu"
                      hint="Ảnh (JPG, PNG, WebP, GIF — tối đa 5MB, chưa hỗ trợ PDF)"
                    />
                  </div>
                  <div className="flex flex-col gap-3">
                    <label className="font-label-md text-label-md text-on-surface">Mô hình 3D (.glb)</label>
                    <FileRow
                      file={model3dFile}
                      onChange={setModel3dFile}
                      accept=".glb"
                      icon={CubeTransparent}
                      title="Tải lên file"
                      hint="Không bắt buộc — dùng cho tour ảo 360°"
                    />
                  </div>
                </div>
              </div>
            </section>

            <div className="flex flex-col-reverse md:flex-row justify-end items-center gap-4 pt-4">
              {draftSavedAt && (
                <span className="text-label-md font-label-md text-tertiary">Đã lưu nháp trên trình duyệt này</span>
              )}
              <button
                type="button"
                onClick={handleSaveDraft}
                disabled={submitting}
                className="px-8 py-3 rounded-xl border border-outline text-on-surface font-label-md text-label-md hover:bg-surface-container-highest transition-colors disabled:opacity-50"
              >
                Lưu nháp
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="px-8 py-3 rounded-xl bg-primary text-on-primary font-label-md text-label-md hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50"
              >
                {submitting ? "Đang gửi..." : "Gửi duyệt"}
              </button>
            </div>
          </form>
        </div>
      </main>
    </div>
  );
}
