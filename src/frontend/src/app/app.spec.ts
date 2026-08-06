import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideBffAuth, provideSpaOidcAuth } from '@flying-bee/oidc-starter-auth';
import { App } from './app';
import { environment } from '../environments/environment';

describe('App', () => {
  let httpTestingController: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        environment.authMode === 'spa'
          ? provideSpaOidcAuth(environment.oidc)
          : provideBffAuth({ apiOrigin: environment.apiOrigin }),
      ],
    }).compileComponents();

    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');

    httpTestingController
      .expectOne(`${apiOrigin}/api/public/ping`)
      .flush({
        status: 'ok',
        applicationName: 'OIDC Starter API',
        timestampUtc: '2026-04-14T00:00:00Z',
        oidcConfigured: false,
      });

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the landing page shell, package onboarding, runtime status, and GitHub footer destination', () => {
    const fixture = TestBed.createComponent(App);

    flushPing();

    fixture.detectChanges();
    flushBffCurrentUserIfRendered();
    flushBffLoginProvidersIfRequested();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('OIDC Starter');
    expect(compiled.querySelectorAll('h1')).toHaveSize(1);
    expect(compiled.querySelector('h1')?.textContent).toContain('One sign-in flow.');
    expect(compiled.querySelector('#overview .eyebrow')?.textContent).toContain('Provider-neutral BFF authentication');
    if (environment.authMode === 'bff') {
      expect(compiled.querySelector('#overview .hero-copy')?.textContent).toContain('OpenID Connect');
      expect(compiled.querySelector('#overview .hero-copy')?.textContent).toContain('social login');
      expect(compiled.querySelector('#overview .hero-copy')?.textContent).toContain('Angular + ASP.NET Core');
      expect(compiled.querySelector('#overview .hero-copy')?.textContent).toContain('server-side session');
    }
    expect(compiled.querySelector('#overview')).not.toBeNull();
    expect(compiled.querySelector('#providers')).not.toBeNull();
    expect(compiled.querySelector('#security')).not.toBeNull();
    expect(compiled.querySelector('#demo')).not.toBeNull();
    expect(compiled.querySelector('app-spa-auth-view, app-bff-auth-view')).not.toBeNull();
    expect(compiled.querySelector('#runtime-title')?.textContent).toContain('Runtime status');
    expect(compiled.textContent).toContain('OIDC Starter API');
    expect(Array.from(compiled.querySelectorAll('.runtime-details dd')).map((value) => value.textContent?.trim()))
      .toContain('No');
    expect(Array.from(compiled.querySelectorAll('nav[aria-label="Primary navigation"] a')).map((link) => link.getAttribute('href')))
      .toEqual(['#overview', '#providers', '#demo', '#security', '#get-started', '#github']);
    expect(Array.from(compiled.querySelector('main')!.querySelectorAll(':scope > section')).map((section) => section.id))
      .toEqual(['overview', 'providers', 'demo', 'security', 'get-started']);
    expect(compiled.querySelector('main #github')).toBeNull();

    const getStarted = compiled.querySelector<HTMLElement>('#get-started');
    expect(getStarted?.querySelector('#get-started-title')?.textContent)
      .toContain('Add OIDC Starter to your Angular + ASP.NET Core app');
    expect(getStarted?.textContent).toContain('optionally add the headless auth client');
    expect(getStarted?.textContent).toContain('without imposing a user interface');
    expect(getStarted?.textContent).toContain('OpenID Connect and registered social login providers');

    const backendCard = getStarted?.querySelector<HTMLElement>('.package-card-backend');
    const backendSample = backendCard?.querySelector('.backend-provider-sample')?.textContent ?? '';
    expect(backendCard?.querySelector('.package-label')?.textContent).toContain('Core server package');
    expect(backendCard?.textContent).toContain('OidcStarter.AspNetCore.Bff');
    expect(backendCard?.querySelector('pre code')?.textContent?.trim()).toBe('dotnet add package OidcStarter.AspNetCore.Bff');
    expect(backendSample).toContain('AddOidcStarterBff');
    expect(backendSample).toContain('AddOidcStarterFacebook');
    expect(backendSample).toContain('AddOidcStarterGitHub');
    expect(backendSample).toContain('AddOidcStarterGoogle');
    expect(backendSample).toContain('ExternalLogin:Facebook');
    expect(backendSample).toContain('ExternalLogin:GitHub');
    expect(backendSample).toContain('ExternalLogin:Google');
    expect(backendSample).toContain('Enabled');
    expect(backendSample).toContain('Options');
    expect(backendSample).not.toContain('ClientId');
    expect(backendSample).not.toContain('ClientSecret');
    expect(backendCard?.textContent).toContain('UseOidcStarterBff');
    expect(backendCard?.textContent).toContain('MapControllers');

    const frontendCard = getStarted?.querySelector<HTMLElement>('.package-card-frontend');
    const frontendSample = frontendCard?.querySelector('.frontend-provider-sample')?.textContent ?? '';
    expect(frontendCard?.querySelector('.package-label')?.textContent).toContain('Optional headless Angular client');
    expect(frontendCard?.textContent).toContain('@flying-bee/oidc-starter-auth');
    expect(frontendCard?.textContent).toContain('Your application owns the buttons, modal, icons, layout, and styling.');
    expect(frontendCard?.querySelector('pre code')?.textContent?.trim()).toBe('npm install @flying-bee/oidc-starter-auth');
    expect(frontendSample).toContain('BffAuthService');
    expect(frontendSample).toContain('getLoginProviders()');
    expect(frontendSample).toContain('login(providerId)');
    expect(frontendSample).toContain('provider.id');
    expect(frontendSample).toContain('provider.displayName');
    expect(frontendSample).not.toContain('provider.loginUrl');
    expect(frontendSample).not.toContain('Google');
    expect(frontendSample).not.toContain('Facebook');
    expect(frontendSample).not.toContain('GitHub');
    expect(frontendSample).not.toContain('OIDC');
    expect(frontendCard?.textContent).toContain('Call auth.login() to use the backend\'s default provider.');
    expect(Array.from(getStarted?.querySelectorAll('pre code') ?? [])).not.toHaveSize(0);

    const providersSection = compiled.querySelector<HTMLElement>('#providers');
    expect(providersSection?.textContent).toContain('Register the login providers your application needs.');
    expect(providersSection?.textContent).toContain('configured list at runtime');
    expect(providersSection?.textContent).toContain('frontend renders its own provider-selection experience');
    expect(providersSection?.textContent).toContain('OpenID Connect remains the standards-based core');
    expect(providersSection?.textContent).not.toContain('Facebook and GitHub are OIDC providers');

    for (const selector of ['.package-link-nuget', '.package-link-npm', '.get-started-guide']) {
      const links = Array.from(getStarted?.querySelectorAll<HTMLAnchorElement>(selector) ?? []);
      expect(links).toHaveSize(1);
      expect(links[0].target).toBe('_blank');
      expect(links[0].rel).toContain('noopener');
      expect(links[0].rel).toContain('noreferrer');
    }
    expect(getStarted?.querySelector('.package-link-nuget')?.getAttribute('href'))
      .toBe('https://www.nuget.org/packages/OidcStarter.AspNetCore.Bff');
    expect(getStarted?.querySelector('.package-link-npm')?.getAttribute('href'))
      .toBe('https://www.npmjs.com/package/@flying-bee/oidc-starter-auth');
    expect(getStarted?.querySelector('.get-started-guide')?.getAttribute('href'))
      .toBe('https://github.com/jszyduk/oidc-starter');
    expect(getStarted?.querySelector('.get-started-next')?.textContent)
      .toContain('provider-selection modal is a reference UI built by the sample application');
    expect(getStarted?.querySelector('.get-started-clarification')?.textContent)
      .toContain('in-repository projects for local development');
    expect(httpTestingController.match(() => true)).toHaveSize(0);

    const footer = compiled.querySelector<HTMLElement>('footer#github');
    expect(footer).not.toBeNull();
    expect(footer?.querySelector('#github-footer-title')?.textContent)
      .toContain('Build the sign-in flow once. Keep the rest of your app yours.');
    expect(footer?.textContent?.replace(/\s+/g, ' '))
      .toContain('OIDC Starter is an ASP.NET Core BFF toolkit for OpenID Connect and social login, with runtime provider discovery, application-owned authorization, and an optional headless Angular client.');
    expect(footer?.querySelector('small')?.textContent)
      .toContain('Provider-neutral BFF authentication for Angular + ASP.NET Core');
    expect(footer?.textContent)
      .toContain('Explore the source, run the sample, and adapt only the pieces your product needs.');

    const repositoryLinks = Array.from(footer?.querySelectorAll<HTMLAnchorElement>('a[href="https://github.com/jszyduk/oidc-starter"]') ?? []);
    expect(repositoryLinks).toHaveSize(1);
    expect(repositoryLinks[0].textContent?.trim()).toBe('View on GitHub');
    expect(repositoryLinks[0].target).toBe('_blank');
    expect(repositoryLinks[0].rel).toBe('noopener noreferrer');
    expect(Array.from(compiled.querySelectorAll('nav[aria-label="Footer navigation"] a')).map((link) => link.getAttribute('href')))
      .toEqual(['#overview', '#providers', '#demo', '#security', '#get-started']);
  });

  it('formats the ping timestamp as UTC while preserving the backend value', () => {
    const fixture = TestBed.createComponent(App);
    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');
    const timestampUtc = '2026-08-06T11:31:19.0631303+00:00';

    httpTestingController.expectOne(`${apiOrigin}/api/public/ping`).flush({
      status: 'ok',
      applicationName: 'OIDC Starter API',
      timestampUtc,
      oidcConfigured: true,
    });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const timestamp = compiled.querySelector<HTMLTimeElement>('.runtime-details time');
    expect(Array.from(compiled.querySelectorAll('.runtime-details dt')).map((label) => label.textContent?.trim()))
      .toContain('Last checked');
    expect(timestamp?.getAttribute('datetime')).toBe(timestampUtc);
    expect(timestamp?.textContent?.trim()).toBe('6 Aug 2026, 11:31 UTC');
    expect(timestamp?.textContent).not.toContain(timestampUtc);
    expect(compiled.querySelector('.runtime-details')?.textContent).toContain('Yes');
  });

  it('retains the runtime loading and error states for the unchanged ping endpoint', () => {
    const fixture = TestBed.createComponent(App);
    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');

    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading public ping endpoint...');
    httpTestingController.expectOne(`${apiOrigin}/api/public/ping`).flush(null, {
      status: 0,
      statusText: 'Unavailable',
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Backend API not reachable yet.');
  });

  it('does not request BFF providers when the demo is running in SPA mode', () => {
    if (environment.authMode !== 'spa') {
      return;
    }

    const fixture = TestBed.createComponent(App);
    flushPing();
    fixture.detectChanges();

    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');
    expect(fixture.nativeElement.querySelector('app-spa-auth-view')).not.toBeNull();
    expect(httpTestingController.match(`${apiOrigin}/api/auth/providers`)).toHaveSize(0);
    expect(fixture.nativeElement.querySelector('.header-button')?.getAttribute('href')).toBe('#demo');
    expect(fixture.nativeElement.querySelector('.sign-in-dialog')).toBeNull();
  });

  it('uses the existing BFF dialog from the header without duplicating provider discovery', () => {
    if (environment.authMode !== 'bff') {
      return;
    }

    const fixture = TestBed.createComponent(App);
    flushPing();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const headerSignIn = compiled.querySelector<HTMLButtonElement>('.site-header .header-button');
    expect(headerSignIn?.disabled).toBeTrue();
    expect(compiled.querySelector('.unauthenticated-sign-in')).toBeNull();
    expect(authenticatedCardHeadings(compiled)).toEqual([]);

    flushBffCurrentUserIfRendered();
    fixture.detectChanges();

    const providerRequest = httpTestingController.expectOne(`${environment.apiOrigin.replace(/\/$/, '')}/api/auth/providers`);
    providerRequest.flush([]);
    fixture.detectChanges();

    expect(headerSignIn?.disabled).toBeFalse();
    expect(compiled.querySelector('.unauthenticated-state')?.textContent).toContain('No active session');
    expect(authenticatedCardHeadings(compiled)).toEqual([]);
    headerSignIn?.click();
    fixture.detectChanges();

    const dialog = compiled.querySelector<HTMLDialogElement>('.sign-in-dialog');
    expect(dialog?.open).toBeTrue();
    expect(compiled.querySelectorAll('.sign-in-dialog')).toHaveSize(1);
    expect(compiled.querySelector('.site-header .provider-action')).toBeNull();
    dialog?.querySelector<HTMLButtonElement>('.dialog-close')?.click();

    expect(document.activeElement).toBe(headerSignIn);
    const demoSignIn = compiled.querySelector<HTMLButtonElement>('.unauthenticated-sign-in');
    demoSignIn?.click();
    fixture.detectChanges();
    expect(dialog?.open).toBeTrue();
    dialog?.querySelector<HTMLButtonElement>('.dialog-close')?.click();
    expect(document.activeElement).toBe(demoSignIn);
    expect(httpTestingController.match(`${environment.apiOrigin.replace(/\/$/, '')}/api/auth/providers`)).toHaveSize(0);
  });

  it('replaces BFF header sign-in with a session link for authenticated users', () => {
    if (environment.authMode !== 'bff') {
      return;
    }

    const fixture = TestBed.createComponent(App);
    flushPing();
    fixture.detectChanges();

    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');
    httpTestingController.expectOne(`${apiOrigin}/api/auth/me`).flush({
      isAuthenticated: true,
      sub: 'subject-123',
      name: 'Test User',
      roles: [],
      externalIdentity: null,
    });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const sessionLink = compiled.querySelector<HTMLAnchorElement>('.site-header .header-button-session');
    expect(sessionLink?.textContent).toContain('View session');
    expect(sessionLink?.getAttribute('href')).toBe('#demo');
    expect(compiled.querySelector('.site-header button.header-button')).toBeNull();
    expect(compiled.querySelector('.unauthenticated-state')).toBeNull();
    expect(authenticatedCardHeadings(compiled)).toEqual(['Login', 'User info']);
    expect(compiled.textContent).toContain('subject-123');
    expect(compiled.querySelector('.button-secondary')?.textContent).toContain('Logout');
    expect(httpTestingController.match(`${apiOrigin}/api/auth/providers`)).toHaveSize(0);
  });

  function flushPing(): void {
    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');

    httpTestingController.expectOne(`${apiOrigin}/api/public/ping`).flush({
      status: 'ok',
      applicationName: 'OIDC Starter API',
      timestampUtc: '2026-04-14T00:00:00Z',
      oidcConfigured: false,
    });
  }

  function flushBffCurrentUserIfRendered(): void {
    if (environment.authMode !== 'bff') {
      return;
    }

    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');

    httpTestingController.expectOne(`${apiOrigin}/api/auth/me`).flush(null, {
      status: 401,
      statusText: 'Unauthorized',
    });
  }

  function flushBffLoginProvidersIfRequested(): void {
    if (environment.authMode !== 'bff') {
      return;
    }

    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');
    const requests = httpTestingController.match(`${apiOrigin}/api/auth/providers`);

    for (const request of requests) {
      request.flush([]);
    }
  }

  function authenticatedCardHeadings(compiled: HTMLElement): string[] {
    return Array.from(compiled.querySelectorAll<HTMLElement>('.authenticated-card h2')).map(
      (heading) => heading.textContent?.trim() ?? '',
    );
  }
});
