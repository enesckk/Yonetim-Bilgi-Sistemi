import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from '@/auth/AuthContext'
import { ProtectedRoute } from '@/auth/ProtectedRoute'
import { AppLayout } from '@/components/AppLayout'
import { ConfirmProvider } from '@/components/ConfirmDialog'
import { EmployeesPage } from '@/pages/EmployeesPage'
import { EmployeeDetailPage } from '@/pages/EmployeeDetailPage'
import { EmployeeFormPage } from '@/pages/EmployeeFormPage'
import { EmployeeImportPage } from '@/pages/EmployeeImportPage'
import { OrganizationPage } from '@/pages/OrganizationPage'
import { SkillsPage } from '@/pages/SkillsPage'
import { CertificatesPage } from '@/pages/CertificatesPage'
import { CatalogsPage } from '@/pages/CatalogsPage'
import { AuditLogsPage } from '@/pages/AuditLogsPage'
import { ReportsPage } from '@/pages/ReportsPage'
import { UsersAdminPage } from '@/pages/UsersAdminPage'
import { RolesMatrixPage } from '@/pages/RolesMatrixPage'
import { DataQualityPage } from '@/pages/DataQualityPage'
import { MovementsPage } from '@/pages/MovementsPage'
import { NotificationsPage } from '@/pages/NotificationsPage'
import { SettingsPage } from '@/pages/SettingsPage'
import { HomePage } from '@/pages/HomePage'
import { LoginPage } from '@/pages/LoginPage'

export default function App() {
  return (
    <AuthProvider>
      <ConfirmProvider>
        <BrowserRouter>
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
              <Route index element={<HomePage />} />
              <Route path="employees" element={<EmployeesPage />} />
              <Route path="employees/import" element={<EmployeeImportPage />} />
              <Route path="employees/new" element={<EmployeeFormPage />} />
              <Route path="employees/:id/edit" element={<EmployeeFormPage />} />
              <Route path="employees/:id" element={<EmployeeDetailPage />} />
              <Route path="movements" element={<MovementsPage />} />
              <Route path="units" element={<OrganizationPage section="units" />} />
              <Route path="facilities" element={<OrganizationPage section="facilities" />} />
              <Route path="organization" element={<OrganizationPage section="chart" />} />
              <Route path="skills" element={<SkillsPage />} />
              <Route path="certificates" element={<CertificatesPage />} />
              <Route path="catalogs" element={<CatalogsPage />} />
              <Route path="audit-logs" element={<AuditLogsPage />} />
              <Route path="reports" element={<ReportsPage />} />
              <Route path="data-quality" element={<DataQualityPage />} />
              <Route path="notifications" element={<NotificationsPage />} />
              <Route path="settings" element={<SettingsPage />} />
              <Route path="users" element={<UsersAdminPage />} />
              <Route path="roles" element={<RolesMatrixPage />} />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
      </ConfirmProvider>
    </AuthProvider>
  )
}
