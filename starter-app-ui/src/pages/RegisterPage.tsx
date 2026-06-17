import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { Card, CardContent, CardHeader, Button, Separator, Spinner } from '@heroui/react';
import { ApiErrorDisplay } from '../components/ApiErrorDisplay';
import { FormField } from '../components/FormField';
import { useAuth } from '../hooks/useAuth';
import { redirectToGitHubOAuth, redirectToGoogleOAuth } from '../utils/oauthUtils';
import {
  validatePassword,
  getPasswordRequirementsDescription,
  type PasswordValidationResult
} from '../utils/passwordValidation';

export function RegisterPage() {
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<Error | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [passwordValidation, setPasswordValidation] = useState<PasswordValidationResult>({ isValid: false, errors: [] });

  const { register } = useAuth();
  const navigate = useNavigate();

  const handlePasswordChange = (value: string) => {
    setPassword(value);
    setPasswordValidation(validatePassword(value));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const result = validatePassword(password);
    if (!result.isValid) {
      setError(new Error(result.errors.join(', ')));
      return;
    }

    if (password !== confirmPassword) {
      setError(new Error('Passwords do not match'));
      return;
    }

    setIsLoading(true);

    try {
      await register({ userName, email, password });
      navigate('/', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Registration failed'));
    } finally {
      setIsLoading(false);
    }
  };

  const handleGitHubLogin = () => {
    try {
      redirectToGitHubOAuth();
    } catch (err) {
      setError(err instanceof Error ? err : new Error('GitHub login setup error'));
    }
  };

  const handleGoogleLogin = () => {
    try {
      redirectToGoogleOAuth();
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Google login setup error'));
    }
  };

  const isDisabled =
    !userName || !email || !password || !confirmPassword || !passwordValidation.isValid || password !== confirmPassword;

  return (
    <div className="min-h-screen bg-linear-to-br from-background to-content1 flex items-center justify-center p-4">
      <Card className="w-full max-w-md shadow-2xl">
        <CardHeader className="flex flex-col items-center pb-6 pt-8">
          <h1 className="text-4xl font-bold text-primary mb-2">StarterApp</h1>
          <h2 className="text-2xl font-semibold text-foreground mb-2">Create Account</h2>
          <p className="text-default-600 text-center">Join StarterApp today!</p>
        </CardHeader>

        <CardContent className="px-8 pb-8">
          <form onSubmit={handleSubmit} className="space-y-4">
            {error && <ApiErrorDisplay error={error} title="Registration Failed" showDetails={true} />}

            <FormField
              label="Username"
              name="userName"
              value={userName}
              onChange={setUserName}
              isRequired
              isDisabled={isLoading}
              placeholder="Choose a username"
              autoComplete="username"
            />

            <FormField
              label="Email"
              name="email"
              type="email"
              value={email}
              onChange={setEmail}
              isRequired
              isDisabled={isLoading}
              placeholder="Enter your email"
              autoComplete="email"
            />

            <FormField
              label="Password"
              name="password"
              type="password"
              value={password}
              onChange={handlePasswordChange}
              isRequired
              isDisabled={isLoading}
              placeholder="Choose a password"
              autoComplete="new-password"
              description={getPasswordRequirementsDescription()}
              isInvalid={password.length > 0 && !passwordValidation.isValid}
              errorMessage={password.length > 0 && !passwordValidation.isValid ? passwordValidation.errors.join(', ') : undefined}
            />

            <FormField
              label="Confirm Password"
              name="confirmPassword"
              type="password"
              value={confirmPassword}
              onChange={setConfirmPassword}
              isRequired
              isDisabled={isLoading}
              placeholder="Confirm your password"
              autoComplete="new-password"
              isInvalid={confirmPassword.length > 0 && password !== confirmPassword}
              errorMessage={confirmPassword.length > 0 && password !== confirmPassword ? 'Passwords do not match' : undefined}
            />

            <Button type="submit" fullWidth className="font-semibold" isPending={isLoading} isDisabled={isDisabled}>
              {({ isPending }) => (
                <>
                  {isPending && <Spinner color="current" size="sm" className="mr-2" />}
                  {isPending ? 'Creating Account...' : 'Create Account'}
                </>
              )}
            </Button>
          </form>

          <Separator className="my-6" />

          <div className="space-y-4">
            <p className="text-sm text-default-500 text-center mb-2">or continue with</p>

            <Button onPress={handleGoogleLogin} variant="outline" fullWidth className="font-semibold" isDisabled={isLoading}>
              <svg className="w-5 h-5 mr-2" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4" />
                <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853" />
                <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05" />
                <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335" />
              </svg>
              Continue with Google
            </Button>

            <Button onPress={handleGitHubLogin} variant="outline" fullWidth className="font-semibold" isDisabled={isLoading}>
              <svg className="w-5 h-5 mr-2" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z" />
              </svg>
              Continue with GitHub
            </Button>
          </div>

          <Separator className="my-6" />

          <div className="text-center">
            <p className="text-default-600">
              Already have an account?{' '}
              <Link to="/login" className="text-primary hover:text-primary-600 font-medium transition-colors">
                Sign in
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
