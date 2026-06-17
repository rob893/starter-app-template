import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { handleOAuthCallback, handleOAuthCallbackFromUrl } from '../oauthUtils';

const GITHUB_STATE_KEY = 'github_oauth_state';

describe('handleOAuthCallback', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('returns the code when state round-trips successfully', () => {
    sessionStorage.setItem(GITHUB_STATE_KEY, 'state-123');
    const params = new URLSearchParams({ code: 'auth-code', state: 'state-123' });

    const result = handleOAuthCallback('github', params);

    expect(result).toEqual({ code: 'auth-code' });
  });

  it('clears the stored state after a successful callback', () => {
    sessionStorage.setItem(GITHUB_STATE_KEY, 'state-123');
    const params = new URLSearchParams({ code: 'auth-code', state: 'state-123' });

    handleOAuthCallback('github', params);

    expect(sessionStorage.getItem(GITHUB_STATE_KEY)).toBeNull();
  });

  it('returns missing_code when state matches but no code is present', () => {
    sessionStorage.setItem(GITHUB_STATE_KEY, 'state-123');
    const params = new URLSearchParams({ state: 'state-123' });

    const result = handleOAuthCallback('github', params);

    expect(result?.error).toBe('missing_code');
  });

  it('returns invalid_state when the returned state does not match storage', () => {
    sessionStorage.setItem(GITHUB_STATE_KEY, 'expected-state');
    const params = new URLSearchParams({ code: 'auth-code', state: 'attacker-state' });

    const result = handleOAuthCallback('github', params);

    expect(result?.error).toBe('invalid_state');
  });

  it('returns invalid_state when no state was ever stored', () => {
    const params = new URLSearchParams({ code: 'auth-code', state: 'whatever' });

    const result = handleOAuthCallback('github', params);

    expect(result?.error).toBe('invalid_state');
  });

  it('surfaces the provider error and description before checking state', () => {
    const params = new URLSearchParams({ error: 'access_denied', error_description: 'User denied' });

    const result = handleOAuthCallback('github', params);

    expect(result).toEqual({ error: 'access_denied', errorDescription: 'User denied' });
  });
});

describe('handleOAuthCallbackFromUrl', () => {
  beforeEach(() => {
    sessionStorage.clear();
    window.location.hash = '';
  });

  afterEach(() => {
    sessionStorage.clear();
    window.location.hash = '';
  });

  it('returns null when the hash has no query string', () => {
    window.location.hash = '#/auth/github/callback';

    expect(handleOAuthCallbackFromUrl('github')).toBeNull();
  });

  it('parses the code from the hash query string with a valid state', () => {
    sessionStorage.setItem(GITHUB_STATE_KEY, 'state-xyz');
    window.location.hash = '#/auth/github/callback?code=abc123&state=state-xyz';

    const result = handleOAuthCallbackFromUrl('github');

    expect(result).toEqual({ code: 'abc123' });
  });

  it('returns invalid_state from the hash when state mismatches', () => {
    sessionStorage.setItem(GITHUB_STATE_KEY, 'expected');
    window.location.hash = '#/auth/github/callback?code=abc123&state=tampered';

    const result = handleOAuthCallbackFromUrl('github');

    expect(result?.error).toBe('invalid_state');
  });
});
