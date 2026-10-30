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
import { PwaInstallPrompt } from '@/components/PwaInstallPrompt'
import { AppUpdateNotice } from '@/components/AppUpdateNotice'

const EventReportsPage = lazy(() => import('@/pages/EventReportsPage').then(m => ({ default: m.EventReportsPage })))
const ActivityReportEditorPage = lazy(() => import('@/pages/EventReportsPage').then(m => ({ default: m.ActivityReportEditorPage })))

const InformationNotesPage = lazy(() => import('@/pages/InformationNotesPage').then(m => ({default:m.InformationNotesPage})))

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
const AdminMessagesPage = lazy(() => import('@/pages/AdminMessagesPage').then(m => ({ default: m.AdminMessagesPage })))
const MessagesPage = lazy(() => import('@/pages/MessagesPage').then((m) => ({ default: m.MessagesPage })))
const SettlementDetailPage = lazy(() =>
  import('@/pages/SettlementDetailPage').then((m) => ({ default: m.SettlementDetailPage })),
)
const StockCustodiesPage = lazy(() => import('@/pages/StockCustodiesPage').then(m => ({ default: m.StockCustodiesPage })))
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
                <Route path="custodies" element={<PersonnelRoute><PermissionRoute permission={PermissionCodes.StockView}><StockCustodiesPage /></PermissionRoute></PersonnelRoute>} />
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
                <Route path="admin/messages" element={<AdminOnlyRoute><AdminMessagesPage /></AdminOnlyRoute>} />
                <Route path="settings" element={<AdminOnlyRoute><SettingsPage /></AdminOnlyRoute>} />
                <Route path="users" element={<AdminOnlyRoute><UsersAdminPage /></AdminOnlyRoute>} />
                <Route path="roles" element={<AdminOnlyRoute><RolesMatrixPage /></AdminOnlyRoute>} />
                <Route path="information-notes" element={<PermissionRoute permission={PermissionCodes.EventsView}><PermissionRoute permission={PermissionCodes.EmployeesViewAllUnits}><InformationNotesPage /></PermissionRoute></PermissionRoute>} />
                <Route path="events" element={<PermissionRoute permission={PermissionCodes.EventsView}><EventsHomePage /></PermissionRoute>} />
                <Route path="events/list" element={<PermissionRoute permission={PermissionCodes.EventsView}><EventsListPage /></PermissionRoute>} />
                <Route path="events/calendar" element={<PermissionRoute permission={PermissionCodes.EventsView}><EventsCalendarPage /></PermissionRoute>} />
                <Route path="events/halls" element={<PermissionRoute permission={PermissionCodes.EventsView}><Navigate to="/events/calendar" replace /></PermissionRoute>} />
                <Route path="events/map" element={<PermissionRoute permission={PermissionCodes.EventsView}><EventsMapPage /></PermissionRoute>} />
                <Route path="events/settlements/:code" element={<PermissionRoute permission={PermissionCodes.EventsView}><SettlementDetailPage /></PermissionRoute>} />
                <Route path="events/facilities" element={<PermissionRoute permission={PermissionCodes.EventsView}><EventsFacilitiesPage /></PermissionRoute>} />
                <Route path="events/facilities-locations" element={<PermissionRoute permission={PermissionCodes.EventsView}><PermissionRoute permission={PermissionCodes.OrganizationView} message="Tesis konumlarını görüntüleme yetkiniz yok."><FacilityLocationsPage /></PermissionRoute></PermissionRoute>} />
                <Route path="events/import" element={<PermissionRoute permission={PermissionCodes.EventsView}><EventImportPage /></PermissionRoute>} />
                <Route path="events/new" element={<PermissionRoute permission={PermissionCodes.EventsView}><EventFormPage /></PermissionRoute>} />
                <Route path="events/reports" element={<PermissionRoute permission={PermissionCodes.EventsView}><PermissionRoute permission={PermissionCodes.EmployeesViewAllUnits}><EventReportsPage /></PermissionRoute></PermissionRoute>} />
                <Route path="events/reports/:reportId" element={<PermissionRoute permission={PermissionCodes.EventsView}><PermissionRoute permission={PermissionCodes.EmployeesViewAllUnits}><ActivityReportEditorPage /></PermissionRoute></PermissionRoute>} />
                <Route path="events/:id/edit" element={<PermissionRoute permission={PermissionCodes.EventsView}><EventFormPage /></PermissionRoute>} />
                <Route path="events/:id" element={<PermissionRoute permission={PermissionCodes.EventsView}><EventDetailPage /></PermissionRoute>} />
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </Suspense>
          <PwaInstallPrompt />
          <AppUpdateNotice />
        </BrowserRouter>
      </ConfirmProvider>
    </AuthProvider>
  )
}
