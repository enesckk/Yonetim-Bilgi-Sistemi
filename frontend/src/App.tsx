import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from '@/auth/AuthContext'
import { ProtectedRoute } from '@/auth/ProtectedRoute'
import { AppLayout } from '@/components/AppLayout'
import { ConfirmProvider } from '@/components/ConfirmDialog'
import { SessionSplash } from '@/components/SessionSplash'
import { HomeEntry } from '@/auth/HomeEntry'
import { AdminOnlyRoute } from '@/auth/AdminOnlyRoute'
import { PersonnelRoute } from '@/auth/PersonnelRoute'
import { PermissionRoute } from '@/auth/PermissionRoute'
import { LoginPage } from '@/pages/LoginPage'
import { PermissionCodes } from '@/auth/permissionCodes'

const EmployeesPage = lazy(() => import('@/pages/EmployeesPage').then((m) => ({ default: m.EmployeesPage })))
const EmployeeDetailPage = lazy(() =>
  import('@/pages/EmployeeDetailPage').then((m) => ({ default: m.EmployeeDetailPage })),
)
const EmployeeFormPage = lazy(() =>
  import('@/pages/EmployeeFormPage').then((m) => ({ default: m.EmployeeFormPage })),
)
const EmployeeImportPage = lazy(() =>
  import('@/pages/EmployeeImportPage').then((m) => ({ default: m.EmployeeImportPage })),
)
const OrganizationPage = lazy(() =>
  import('@/pages/OrganizationPage').then((m) => ({ default: m.OrganizationPage })),
)
const SkillsPage = lazy(() => import('@/pages/SkillsPage').then((m) => ({ default: m.SkillsPage })))
const CertificatesPage = lazy(() =>
  import('@/pages/CertificatesPage').then((m) => ({ default: m.CertificatesPage })),
)
const CatalogsPage = lazy(() => import('@/pages/CatalogsPage').then((m) => ({ default: m.CatalogsPage })))
const AuditLogsPage = lazy(() => import('@/pages/AuditLogsPage').then((m) => ({ default: m.AuditLogsPage })))
const ReportsPage = lazy(() => import('@/pages/ReportsPage').then((m) => ({ default: m.ReportsPage })))
const UsersAdminPage = lazy(() => import('@/pages/UsersAdminPage').then((m) => ({ default: m.UsersAdminPage })))
const RolesMatrixPage = lazy(() =>
  import('@/pages/RolesMatrixPage').then((m) => ({ default: m.RolesMatrixPage })),
)
const DataQualityPage = lazy(() =>
  import('@/pages/DataQualityPage').then((m) => ({ default: m.DataQualityPage })),
)
const MovementsPage = lazy(() => import('@/pages/MovementsPage').then((m) => ({ default: m.MovementsPage })))
const NotificationsPage = lazy(() =>
  import('@/pages/NotificationsPage').then((m) => ({ default: m.NotificationsPage })),
)
const SettingsPage = lazy(() => import('@/pages/SettingsPage').then((m) => ({ default: m.SettingsPage })))
const StaffDashboardPage = lazy(() =>
  import('@/pages/StaffDashboardPage').then((m) => ({ default: m.StaffDashboardPage })),
)
const EventsHomePage = lazy(() => import('@/pages/EventsHomePage').then((m) => ({ default: m.EventsHomePage })))
const EventsListPage = lazy(() => import('@/pages/EventsListPage').then((m) => ({ default: m.EventsListPage })))
const EventsCalendarPage = lazy(() =>
  import('@/pages/EventsCalendarPage').then((m) => ({ default: m.EventsCalendarPage })),
)
const EventsFacilitiesPage = lazy(() =>
  import('@/pages/EventsFacilitiesPage').then((m) => ({ default: m.EventsFacilitiesPage })),
)
const EventFormPage = lazy(() => import('@/pages/EventFormPage').then((m) => ({ default: m.EventFormPage })))
const EventDetailPage = lazy(() => import('@/pages/EventFormPage').then((m) => ({ default: m.EventDetailPage })))
const EventsMapPage = lazy(() => import('@/pages/EventsMapPage').then((m) => ({ default: m.EventsMapPage })))
const FacilityLocationsPage = lazy(() =>
  import('@/pages/FacilityLocationsPage').then((m) => ({ default: m.FacilityLocationsPage })),
)
const EventImportPage = lazy(() =>
  import('@/pages/EventImportPage').then((m) => ({ default: m.EventImportPage })),
)
const MessagesPage = lazy(() => import('@/pages/MessagesPage').then((m) => ({ default: m.MessagesPage })))
const SettlementDetailPage = lazy(() =>
  import('@/pages/SettlementDetailPage').then((m) => ({ default: m.SettlementDetailPage })),
)
const StockPage = lazy(() => import('@/pages/StockPage').then((m) => ({ default: m.StockPage })))
const StockLocationPage = lazy(() =>
  import('@/pages/StockLocationPage').then((m) => ({ default: m.StockLocationPage })),
)
const TasksPage = lazy(() => import('@/pages/TasksPage').then((m) => ({ default: m.TasksPage })))

export default function App() {
  return (
    <AuthProvider>
      <ConfirmProvider>
        <BrowserRouter>
          <Suspense fallback={<SessionSplash message="Sayfa yükleniyor…" />}>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route
                path="/"
                element={
                  <ProtectedRoute>
                    <AppLayout />
                  </ProtectedRoute>
                }
              >
                <Route index element={<HomeEntry />} />
                <Route path="personnel" element={<PersonnelRoute><StaffDashboardPage /></PersonnelRoute>} />
                <Route path="stock" element={<PersonnelRoute><StockPage /></PersonnelRoute>} />
                <Route path="stock/locations/:id" element={<PersonnelRoute><StockLocationPage /></PersonnelRoute>} />
                <Route path="tasks" element={<PersonnelRoute><TasksPage /></PersonnelRoute>} />
                <Route path="employees" element={<PersonnelRoute><EmployeesPage /></PersonnelRoute>} />
                <Route path="employees/import" element={<AdminOnlyRoute><EmployeeImportPage /></AdminOnlyRoute>} />
                <Route path="employees/new" element={<PersonnelRoute><EmployeeFormPage /></PersonnelRoute>} />
                <Route path="employees/:id/edit" element={<PersonnelRoute><EmployeeFormPage /></PersonnelRoute>} />
                <Route path="employees/:id" element={<PersonnelRoute><EmployeeDetailPage /></PersonnelRoute>} />
                <Route path="movements" element={<PersonnelRoute><MovementsPage /></PersonnelRoute>} />
                <Route path="units" element={<PersonnelRoute><OrganizationPage section="units" /></PersonnelRoute>} />
                <Route path="facilities" element={<PersonnelRoute><OrganizationPage section="facilities" /></PersonnelRoute>} />
                <Route path="organization" element={<PersonnelRoute><OrganizationPage section="chart" /></PersonnelRoute>} />
                <Route path="reports" element={<AdminOnlyRoute><ReportsPage /></AdminOnlyRoute>} />
                <Route path="skills" element={<AdminOnlyRoute><SkillsPage /></AdminOnlyRoute>} />
                <Route path="certificates" element={<AdminOnlyRoute><CertificatesPage /></AdminOnlyRoute>} />
                <Route path="catalogs" element={<AdminOnlyRoute><CatalogsPage /></AdminOnlyRoute>} />
                <Route path="audit-logs" element={<AdminOnlyRoute><AuditLogsPage /></AdminOnlyRoute>} />
                <Route path="data-quality" element={<AdminOnlyRoute><DataQualityPage /></AdminOnlyRoute>} />
                <Route
                  path="notifications"
                  element={
                    <PermissionRoute permission={PermissionCodes.NotificationsView} message="Bildirimleri görüntüleme yetkiniz yok.">
                      <NotificationsPage />
                    </PermissionRoute>
                  }
                />
                <Route path="messages" element={<MessagesPage />} />
                <Route path="settings" element={<AdminOnlyRoute><SettingsPage /></AdminOnlyRoute>} />
                <Route path="users" element={<AdminOnlyRoute><UsersAdminPage /></AdminOnlyRoute>} />
                <Route path="roles" element={<AdminOnlyRoute><RolesMatrixPage /></AdminOnlyRoute>} />
                <Route path="events" element={<EventsHomePage />} />
                <Route path="events/list" element={<EventsListPage />} />
                <Route path="events/calendar" element={<EventsCalendarPage />} />
                <Route path="events/halls" element={<Navigate to="/events/calendar" replace />} />
                <Route path="events/map" element={<EventsMapPage />} />
                <Route path="events/settlements/:code" element={<SettlementDetailPage />} />
                <Route path="events/facilities" element={<EventsFacilitiesPage />} />
                <Route path="events/facilities-locations" element={<PermissionRoute permission={PermissionCodes.OrganizationView} message="Tesis konumlarını görüntüleme yetkiniz yok."><FacilityLocationsPage /></PermissionRoute>} />
                <Route path="events/import" element={<EventImportPage />} />
                <Route path="events/new" element={<EventFormPage />} />
                <Route path="events/:id/edit" element={<EventFormPage />} />
                <Route path="events/:id" element={<EventDetailPage />} />
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </Suspense>
        </BrowserRouter>
      </ConfirmProvider>
    </AuthProvider>
  )
}
