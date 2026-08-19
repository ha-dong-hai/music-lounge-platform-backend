# docs/ — Index

Tài liệu được nhóm theo chủ đề. Số thứ tự gốc (00-28) được giữ nguyên trong tên file
(không đánh số lại) để các trích dẫn dạng "docs/16", "docs/17-21" trong các tài liệu khác
vẫn còn hiểu được — chỉ có thư mục cha thay đổi.

## architecture/ — Kiến trúc & schema hệ thống
- `00-project-structure.md` — cấu trúc project/solution
- `01-overview.md` — tổng quan, tech stack, actors
- `02-database.md` — schema DB: bảng, nhóm, convention
- `04-design-decisions.md` — các quyết định thiết kế đã chốt
- `05-architecture.md` — kiến trúc Clean Architecture, layering
- `09-tech-knowledge.md` — kiến thức công nghệ nền tảng
- `10-schema-analysis.md` — phân tích schema, đối chiếu nguồn
- `13-data-model.md` — data model chi tiết
- `15-risk-audit.md` — audit rủi ro kỹ thuật
- `platform-architecture.md` — kiến trúc toàn nền tảng (web-first, 30 views + mobile F&B)

## business/ — Nghiệp vụ, compliance, domain
- `03-business-requirements.md` — BR + workflows
- `06-compliance.md` — Nghị định/luật liên quan
- `11-ba-domain-analysis.md` — phân tích domain từ code thực tế
- `12-actors-and-authorization.md` — actor & phân quyền
- `BA-analysis.md` — bản súc tích của 11-15, dùng cho trình bày/bàn giao
- `backend-sop-custom-criteria.md` — SOP tính năng Custom Criteria

## journeys/ — Hành trình người dùng theo actor
- `14-usecase-traces.md` — trace use case xuyên actor
- `17-audience-journey.md`
- `18-owner-journey.md`
- `19-staff-journey.md`
- `20-admin-journey.md`
- `21-anonymous-journey.md`
- `22-performer-presence.md`

## views/ — Danh mục & thiết kế màn hình
- `23-view-catalog.md` — 73+ screens rút ra từ journeys 17-22
- `24-view-flow-diagrams.md` — sơ đồ luồng giữa các view
- `View-Design-Spec.md` — bản đặc tả bàn giao thiết kế đầy đủ (gộp 16-24)
- `design-audit.md` — audit thiết kế UI đã sinh ra

## api/ — Hợp đồng API & chuẩn code
- `16-api-endpoint-catalog.md` — danh mục endpoint
- `26-api-field-reference.md` — field-level reference đầy đủ, verify qua reflection
- `27-api-cheatsheet.md` — bản rút gọn dễ đọc của 26
- `API-STANDARDS.md` — chuẩn response/error format
- `AUTH-SECURITY-STANDARDS.md` — chuẩn mật khẩu/đăng ký (OWASP/NIST)

## stitch/ — Brief cho Stitch (AI UI generation)
- `Stitch-Master-Brief.md` — gộp toàn bộ 5 brief bên dưới thành 1 file cho Stitch
- `stitch-brief-admin-web.md`
- `stitch-brief-audience-mobile-fnb.md`
- `stitch-brief-audience-web.md`
- `stitch-brief-audience.md`
- `stitch-brief-create-show-v2.md`
- `stitch-brief-owner-web.md`
- `stitch-brief-staff-mobile.md`

## ops/ — Vận hành, deploy, changelog
- `07-project-status.md` — pending items
- `08-changelog.md`
- `25-deploy-azure.md` — hướng dẫn deploy Azure App Service
- `28-defence-qa-backend.md` / `.html` — Q&A bảo vệ đồ án (backend)

## secrets/ — **Không commit, đã gitignore (`docs/secrets/`)**
File nhạy cảm dùng local/dev: `Firebase/`, `Google_Sign-In/`, `Mux_Key/`, `VNPAY_Key/`,
`Pass`, `admtok.txt`. Đường dẫn tuyệt đối trong `appsettings.*.Local.json` trỏ vào đây.

---

Tài liệu ngoài `docs/` liên quan:
- `README-SETUP.md` (gốc repo) — hướng dẫn setup local
- `spec/` — báo cáo SEP490 (build system: xem `spec/build/`, template gốc ở `spec/template/`)
- `diagrams/` — sinh sơ đồ kiến trúc/ERD/sequence bằng script (xem `diagrams/STANDARDS.md`)
