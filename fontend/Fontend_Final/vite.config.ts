import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

// Backend mà dev server proxy tới. Mặc định là backend THẬT trên Azure (đang chạy 24/7, xem
// docs/ops/25-deploy-azure.md) để `npm run dev` chạy được ngay mà không cần bật API local.
// Muốn dùng backend local: tạo file `.env.local` với dòng
//   VITE_BACKEND_TARGET=http://localhost:5289
// rồi chạy lại `npm run dev` (Vite chỉ đọc env lúc khởi động, không hot-reload file này).
const DEFAULT_BACKEND = "https://musiclounge-api.azurewebsites.net";

export default defineConfig(({ mode }) => {
  // loadEnv thay vì process.env: nó đọc cả .env/.env.local trong thư mục project, còn process.env
  // chỉ thấy biến của shell.
  const env = loadEnv(mode, process.cwd(), "VITE_");
  const target = env.VITE_BACKEND_TARGET || DEFAULT_BACKEND;

  // In ra lúc khởi động — nếu không có dòng này thì rất dễ debug nhầm "API trả 401/404" trong khi
  // thật ra đang trỏ vào backend khác với mình tưởng.
  console.log(`[vite] proxy /api, /hubs, /uploads -> ${target}`);

  const proxyCommon = {
    target,
    changeOrigin: true,
    secure: true,
  };

  return {
    plugins: [react()],
    server: {
      port: 5173,
      proxy: {
        "/api": proxyCommon,
        "/hubs": { ...proxyCommon, ws: true },
        "/uploads": proxyCommon,
      },
    },
  };
});
