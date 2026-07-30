import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BffAuthService, provideBffAuth } from '../public-api';
import { BFF_AUTH_NAVIGATOR } from './internal/bff-auth-navigator';
import type { BffAuthConfig, BffLoginProvider } from '../public-api';
import type { BffAuthNavigator } from './internal/bff-auth-navigator';

describe('BffAuthService', () => {
  let httpTesting: HttpTestingController;

  afterEach(() => {
    document.querySelectorAll('form[action$="/logout"]').forEach((form) => form.remove());
    document.cookie = 'XSRF-TOKEN=; Max-Age=0; path=/';
    httpTesting?.verify();
    TestBed.resetTestingModule();
  });

  it('uses the default auth base URL for current-user requests', () => {
    const service = createService();

    const request = expectCurrentUserRequest('/api/auth/me');
    request.flush({ isAuthenticated: false });

    expect(service.currentUser()).toBeNull();
    expect(service.authenticated()).toBeFalse();
  });

  it('normalizes a configured origin and auth path when constructing fixed endpoints', () => {
    const service = createService({
      apiOrigin: 'https://api.example.test/',
      authPath: 'api/auth',
    });

    expectCurrentUserRequest('https://api.example.test/api/auth/me').flush({ isAuthenticated: false });
  });

  it('preserves a leading auth path slash without duplicating it after the configured origin', () => {
    createService({
      apiOrigin: 'https://api.example.test/',
      authPath: '/api/auth',
    });

    expectCurrentUserRequest('https://api.example.test/api/auth/me').flush({ isAuthenticated: false });
  });

  it('navigates to the default login endpoint without making an HTTP request', () => {
    const authNavigator = createAuthNavigator();
    const service = createService({}, authNavigator);
    expectCurrentUserRequest('/api/auth/me').flush({ isAuthenticated: false });

    service.login();

    expect(authNavigator.navigate).toHaveBeenCalledOnceWith('/api/auth/login');
    expect(service.isLoading()).toBeTrue();
    httpTesting.expectNone('/api/auth/login');
  });

  it('uses the same canonical login URL for repeated configured navigation', () => {
    const config: BffAuthConfig = {
      apiOrigin: 'https://api.example.test/',
      authPath: '/api/auth/',
    };
    const authNavigator = createAuthNavigator();
    const service = createService(config, authNavigator);
    expectCurrentUserRequest('https://api.example.test/api/auth/me').flush({ isAuthenticated: false });

    service.login();
    service.login();

    expect(authNavigator.navigate).toHaveBeenCalledTimes(2);
    expect(authNavigator.navigate).toHaveBeenCalledWith('https://api.example.test/api/auth/login');
    expect(config).toEqual({
      apiOrigin: 'https://api.example.test/',
      authPath: '/api/auth/',
    });
    httpTesting.expectNone('https://api.example.test/api/auth/login');
  });

  it('discovers providers from the default credentialed endpoint', () => {
    const service = createService();
    expectCurrentUserRequest('/api/auth/me').flush({ isAuthenticated: false });
    const providers: readonly BffLoginProvider[] = [
      {
        id: 'google',
        displayName: 'Google',
        isDefault: false,
        loginUrl: '/api/auth/login/google',
      },
    ];
    let discoveredProviders: readonly BffLoginProvider[] | undefined;

    service.getLoginProviders().subscribe((result) => (discoveredProviders = result));

    const request = httpTesting.expectOne('/api/auth/providers');
    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBeTrue();
    request.flush(providers);

    expect(discoveredProviders).toEqual(providers);
  });

  it('uses configured normalized paths and does not cache provider discovery', () => {
    const service = createService({
      apiOrigin: 'https://localhost:7233/',
      authPath: '/api/auth/',
    });
    expectCurrentUserRequest('https://localhost:7233/api/auth/me').flush({ isAuthenticated: false });
    const discoveredProviders: Array<readonly BffLoginProvider[]> = [];

    service.getLoginProviders().subscribe((result) => discoveredProviders.push(result));
    service.getLoginProviders().subscribe((result) => discoveredProviders.push(result));

    const requests = httpTesting.match('https://localhost:7233/api/auth/providers');
    expect(requests).toHaveSize(2);
    requests.forEach((request) => expect(request.request.withCredentials).toBeTrue());
    requests[0].flush([]);
    requests[1].flush([]);

    expect(discoveredProviders).toEqual([[], []]);
    expect(httpTesting.match('https://localhost:7233/api/auth//providers')).toHaveSize(0);
  });

  it('propagates provider-discovery errors', () => {
    const service = createService();
    expectCurrentUserRequest('/api/auth/me').flush({ isAuthenticated: false });
    let status: number | undefined;

    service.getLoginProviders().subscribe({
      error: (error: { status: number }) => (status = error.status),
    });
    httpTesting.expectOne('/api/auth/providers').flush('Server Error', {
      status: 500,
      statusText: 'Server Error',
    });

    expect(status).toBe(500);
  });

  it('navigates to an encoded provider-specific login endpoint without an HTTP request', () => {
    const authNavigator = createAuthNavigator();
    const config: BffAuthConfig = {
      apiOrigin: 'https://localhost:7233/',
      authPath: '/api/auth/',
    };
    const service = createService(config, authNavigator);
    expectCurrentUserRequest('https://localhost:7233/api/auth/me').flush({ isAuthenticated: false });

    service.login('GOOGLE');
    service.login('GOOGLE');
    service.login('google/provider');

    expect(authNavigator.navigate).toHaveBeenCalledTimes(3);
    expect(authNavigator.navigate).toHaveBeenCalledWith('https://localhost:7233/api/auth/login/GOOGLE');
    expect(authNavigator.navigate).toHaveBeenCalledWith('https://localhost:7233/api/auth/login/google%2Fprovider');
    expect(config).toEqual({
      apiOrigin: 'https://localhost:7233/',
      authPath: '/api/auth/',
    });
    httpTesting.expectNone('https://localhost:7233/api/auth/login/GOOGLE');
    httpTesting.expectNone('https://localhost:7233/api/auth/login/google%2Fprovider');
  });

  it('rejects blank provider IDs without navigating', () => {
    const authNavigator = createAuthNavigator();
    const service = createService({}, authNavigator);
    expectCurrentUserRequest('/api/auth/me').flush({ isAuthenticated: false });

    expect(() => service.login('')).toThrowError('Provider ID must not be empty.');
    expect(() => service.login('   ')).toThrowError('Provider ID must not be empty.');

    expect(authNavigator.navigate).not.toHaveBeenCalled();
    expect(service.isLoading()).toBeFalse();
    httpTesting.expectNone('/api/auth/login');
  });

  it('normalizes a trailing auth path slash for current-user, CSRF, and logout endpoints', () => {
    const service = createService({
      apiOrigin: 'https://api.example.test',
      authPath: '/api/auth/',
    });
    expectCurrentUserRequest('https://api.example.test/api/auth/me').flush({ isAuthenticated: false });
    const submit = spyOn(HTMLFormElement.prototype, 'submit');

    service.logout();

    const csrfRequest = httpTesting.expectOne('https://api.example.test/api/auth/csrf');
    csrfRequest.flush('');

    expect(httpTesting.match('https://api.example.test/api/auth//csrf')).toHaveSize(0);
    expect(document.querySelector<HTMLFormElement>('form[action="https://api.example.test/api/auth/logout"]')).not.toBeNull();
    expect(submit).toHaveBeenCalledTimes(1);
  });

  it('loads and exposes an authenticated current user with credentialed GET /me', () => {
    const service = createService({ apiOrigin: 'https://api.example.test' });
    const user = {
      isAuthenticated: true,
      sub: 'subject-123',
      name: 'Test User',
      roles: ['reader'],
    };

    const request = expectCurrentUserRequest('https://api.example.test/api/auth/me');
    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBeTrue();
    request.flush(user);

    expect(service.currentUser()).toEqual(user);
    expect(service.authenticated()).toBeTrue();
    expect(service.isLoading()).toBeFalse();
  });

  it('clears current-user state when GET /me is unauthenticated', () => {
    const service = createService();
    const request = expectCurrentUserRequest('/api/auth/me');

    request.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(service.currentUser()).toBeNull();
    expect(service.authenticated()).toBeFalse();
    expect(service.isLoading()).toBeFalse();
  });

  it('refreshes CSRF state before submitting the fixed logout form endpoint', () => {
    const service = createService({ apiOrigin: 'https://api.example.test/' });
    expectCurrentUserRequest('https://api.example.test/api/auth/me').flush({ isAuthenticated: true });
    document.cookie = 'XSRF-TOKEN=csrf-token; path=/';
    const submit = spyOn(HTMLFormElement.prototype, 'submit');

    service.logout();

    const csrfRequest = httpTesting.expectOne('https://api.example.test/api/auth/csrf');
    expect(csrfRequest.request.method).toBe('GET');
    expect(csrfRequest.request.withCredentials).toBeTrue();
    expect(csrfRequest.request.responseType).toBe('text');
    csrfRequest.flush('');

    const form = document.querySelector<HTMLFormElement>('form[action="https://api.example.test/api/auth/logout"]');
    expect(form).not.toBeNull();
    expect(form?.method).toBe('post');
    expect(form?.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]')?.value).toBe(
      'csrf-token',
    );
    expect(submit).toHaveBeenCalledTimes(1);
  });

  it('continues to logout when the CSRF endpoint is unavailable', () => {
    const service = createService();
    expectCurrentUserRequest('/api/auth/me').flush({ isAuthenticated: false });
    const submit = spyOn(HTMLFormElement.prototype, 'submit');

    service.logout();
    httpTesting.expectOne('/api/auth/csrf').flush('Not Found', { status: 404, statusText: 'Not Found' });

    expect(document.querySelector<HTMLFormElement>('form[action="/api/auth/logout"]')).not.toBeNull();
    expect(submit).toHaveBeenCalledTimes(1);
  });

  it('does not submit logout when refreshing CSRF state fails for another reason', () => {
    const service = createService();
    expectCurrentUserRequest('/api/auth/me').flush({ isAuthenticated: false });
    const submit = spyOn(HTMLFormElement.prototype, 'submit');

    service.logout();
    httpTesting.expectOne('/api/auth/csrf').flush('Server Error', { status: 500, statusText: 'Server Error' });

    expect(submit).not.toHaveBeenCalled();
    expect(service.isLoggingOut()).toBeFalse();
  });

  function createService(config: BffAuthConfig = {}, authNavigator?: BffAuthNavigator): BffAuthService {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideBffAuth(config),
        ...(authNavigator ? [{ provide: BFF_AUTH_NAVIGATOR, useValue: authNavigator }] : []),
      ],
    });

    httpTesting = TestBed.inject(HttpTestingController);
    return TestBed.inject(BffAuthService);
  }

  function expectCurrentUserRequest(url: string) {
    return httpTesting.expectOne(url);
  }

  function createAuthNavigator(): BffAuthNavigator {
    return {
      navigate: jasmine.createSpy('navigate'),
    };
  }
});
