import { Route, Routes } from "react-router-dom";
import SignUp from "./pages/SignUp";
import VerifyEmail from "./pages/VerifyEmail";
import Login from "./pages/Login";
import ForgotPassword from "./pages/ForgotPassword";
import ResetPassword from "./pages/ResetPassword";
import DiscoverShows from "./pages/DiscoverShows";
import DiscoveryHub from "./pages/DiscoveryHub";
import ShowDetail from "./pages/ShowDetail";
import TicketPurchase from "./pages/TicketPurchase";
import MyTickets from "./pages/MyTickets";
import TicketDetail from "./pages/TicketDetail";
import OwnerMyVenues from "./pages/OwnerMyVenues";
import OwnerCreateVenue from "./pages/OwnerCreateVenue";
import OwnerSubscription from "./pages/OwnerSubscription";
import OwnerMyShows from "./pages/OwnerMyShows";
import OwnerCreateShow from "./pages/OwnerCreateShow";
import OwnerFinance from "./pages/OwnerFinance";
import StaffBoxOffice from "./pages/StaffBoxOffice";
import AdminPendingVenues from "./pages/AdminPendingVenues";
import AdminRefunds from "./pages/AdminRefunds";
import AdminUsers from "./pages/AdminUsers";
import AdminComplaints from "./pages/AdminComplaints";
import AdminPlatformSettings from "./pages/AdminPlatformSettings";
import AdminShowModeration from "./pages/AdminShowModeration";
import AdminBankAccounts from "./pages/AdminBankAccounts";
import AdminPenalties from "./pages/AdminPenalties";
import PaymentResult from "./pages/PaymentResult";
import ComingSoon from "./pages/ComingSoon";

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<SignUp />} />
      <Route path="/verify-email" element={<VerifyEmail />} />
      <Route path="/login" element={<Login />} />
      <Route path="/forgot-password" element={<ForgotPassword />} />
      <Route path="/reset-password" element={<ResetPassword />} />

      {/* Audience/Guest surface -- see docs/views/23-view-catalog.md Group C. Public, AllowAnonymous
          backend, works with zero token (see docs/journeys/21-anonymous-journey.md Journey 1). Uses the
          AuraLounge Stitch visual system by explicit user decision (distinct from Owner/Admin/Auth's
          "Warm Luxury Lounge"), same choice as ForgotPassword.tsx/ResetPassword.tsx. */}
      <Route path="/discover" element={<DiscoveryHub />} />
      <Route path="/shows" element={<DiscoverShows />} />
      <Route path="/shows/:id" element={<ShowDetail />} />
      <Route path="/shows/:id/tickets" element={<TicketPurchase />} />
      <Route path="/me/tickets" element={<MyTickets />} />
      <Route path="/me/tickets/:id" element={<TicketDetail />} />

      {/* Owner surface -- see docs/architecture/platform-architecture.md. Only "My Venues" is a real screen so
          far; the rest of the nav/action graph points at ComingSoon rather than dead links. */}
      <Route path="/owner/venues" element={<OwnerMyVenues />} />
      <Route path="/owner/venues/new" element={<OwnerCreateVenue />} />
      <Route path="/owner/venues/:id/edit" element={<ComingSoon />} />
      <Route path="/owner/venues/:id/analytics" element={<OwnerFinance />} />
      <Route path="/owner/shows" element={<OwnerMyShows />} />
      <Route path="/owner/shows/new" element={<OwnerCreateShow />} />
      <Route path="/owner/shows/:id" element={<ComingSoon />} />
      <Route path="/owner/bookings" element={<ComingSoon />} />
      <Route path="/owner/finance" element={<OwnerFinance />} />
      <Route path="/owner/settings" element={<ComingSoon />} />
      <Route path="/owner/subscription" element={<OwnerSubscription />} />
      <Route path="/owner/penalties" element={<ComingSoon />} />

      {/* Staff surface -- first Staff screens, added 2026-08-18. RequireVenueOperator backend
          policy also allows Owner/Admin, but the frontend routes here are Staff-only in practice
          (Owner/Admin have their own venue-management screens for the same underlying data). */}
      <Route path="/staff/box-office" element={<StaffBoxOffice />} />

      {/* Shared VNPay return landing -- see PaymentResult.tsx for why it can't show transaction
          details. Used by subscriptions today; tickets/donations will land here too later. */}
      <Route path="/payment/success" element={<PaymentResult status="success" />} />
      <Route path="/payment/failed" element={<PaymentResult status="failed" />} />

      {/* Admin surface -- see docs/architecture/platform-architecture.md. All 8 sidebar entries are real
          screens as of 2026-08-18; none point at ComingSoon any more. Two of them needed a backend
          read endpoint added the same day, both the same shape of gap (actions existed, no queue to
          find work in): GET /admin/bank-accounts/pending and GET /venue-penalties. */}
      <Route path="/admin/venues/pending" element={<AdminPendingVenues />} />
      <Route path="/admin/shows/moderation" element={<AdminShowModeration />} />
      <Route path="/admin/penalties" element={<AdminPenalties />} />
      <Route path="/admin/refunds" element={<AdminRefunds />} />
      <Route path="/admin/bank-accounts" element={<AdminBankAccounts />} />
      <Route path="/admin/complaints" element={<AdminComplaints />} />
      <Route path="/admin/users" element={<AdminUsers />} />
      <Route path="/admin/platform-settings" element={<AdminPlatformSettings />} />
    </Routes>
  );
}
