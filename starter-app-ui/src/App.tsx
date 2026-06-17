import { Routes, Route, Navigate } from 'react-router';
import { AuthProvider } from './contexts/AuthContext';
import { ProtectedRoute } from './components/ProtectedRoute';
import { AppLayout } from './layouts/AppLayout';
import { HomePage } from './pages/HomePage';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { OAuthCallbackPage } from './pages/OAuthCallbackPage';
import { ForgotPasswordPage } from './pages/ForgotPasswordPage';
import { ResetPasswordPage } from './pages/ResetPasswordPage';
import { routePaths } from './constants/routes';

function App() {
  return (
    <AuthProvider>
      <div className="app min-h-screen bg-background text-foreground">
        <Routes>
          {/* Auth routes (unauthenticated) */}
          <Route path={routePaths.login} element={<LoginPage />} />
          <Route path={routePaths.register} element={<RegisterPage />} />
          <Route path={routePaths.forgotPassword} element={<ForgotPasswordPage />} />
          <Route path={routePaths.resetPassword} element={<ResetPasswordPage />} />
          <Route path={routePaths.gitHubCallback} element={<OAuthCallbackPage provider="github" />} />
          <Route path={routePaths.googleCallback} element={<OAuthCallbackPage provider="google" />} />

          {/* Protected routes */}
          <Route
            path={routePaths.home}
            element={
              <ProtectedRoute>
                <AppLayout>
                  <HomePage />
                </AppLayout>
              </ProtectedRoute>
            }
          />

          {/* Catch-all */}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </div>
    </AuthProvider>
  );
}

export default App;
