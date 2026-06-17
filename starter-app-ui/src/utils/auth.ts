import { jwtDecode } from 'jwt-decode';
import type { User } from '../types/auth';

interface JwtPayload {
  nameid: number;
  unique_name: string;
  email: string;
  email_verified: boolean;
  role?: string | string[];
  exp: number;
  iat: number;
  nbf: number;
  iss: string;
  aud: string;
}

export function decodeJwtToken(token: string): User | null {
  try {
    const decoded = jwtDecode<JwtPayload>(token);

    let roles: string[] = [];
    if (decoded.role) {
      roles = Array.isArray(decoded.role) ? decoded.role : [decoded.role];
    }

    return {
      id: decoded.nameid,
      userName: decoded.unique_name,
      email: decoded.email,
      roles
    };
  } catch (error) {
    console.error('Failed to decode JWT token:', error);
    return null;
  }
}

export function hasAdminRole(user: User | null): boolean {
  return user?.roles?.includes('Admin') ?? false;
}

export function isTokenExpired(token: string): boolean {
  try {
    const decoded = jwtDecode<JwtPayload>(token);
    const currentTime = Date.now() / 1000;
    return decoded.exp < currentTime;
  } catch {
    return true;
  }
}
