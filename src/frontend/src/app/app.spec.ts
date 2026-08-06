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

  it('renders the landing page shell, live demo, runtime status, and footer', () => {
    const fixture = TestBed.createComponent(App);

    flushPing();

    fixture.detectChanges();
    flushBffCurrentUserIfRendered();
    flushBffLoginProvidersIfRequested();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('OIDC Starter');
    expect(compiled.querySelectorAll('h1')).toHaveSize(1);
    expect(compiled.querySelector('h1')?.textContent).toContain('One sign-in flow.');
    expect(compiled.querySelector('#overview')).not.toBeNull();
    expect(compiled.querySelector('#providers')).not.toBeNull();
    expect(compiled.querySelector('#security')).not.toBeNull();
    expect(compiled.querySelector('#demo')).not.toBeNull();
    expect(compiled.querySelector('app-spa-auth-view, app-bff-auth-view')).not.toBeNull();
    expect(compiled.querySelector('#runtime-title')?.textContent).toContain('Runtime status');
    expect(compiled.textContent).toContain('OIDC Starter API');
    expect(compiled.querySelector('footer a[href="https://github.com/jszyduk/oidc-starter"]')).not.toBeNull();
    expect(Array.from(compiled.querySelectorAll('nav[aria-label="Primary navigation"] a')).map((link) => link.getAttribute('href')))
      .toEqual(['#overview', '#providers', '#demo', '#security']);
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

    flushBffCurrentUserIfRendered();
    fixture.detectChanges();

    const providerRequest = httpTestingController.expectOne(`${environment.apiOrigin.replace(/\/$/, '')}/api/auth/providers`);
    providerRequest.flush([]);
    fixture.detectChanges();

    expect(headerSignIn?.disabled).toBeFalse();
    headerSignIn?.click();
    fixture.detectChanges();

    const dialog = compiled.querySelector<HTMLDialogElement>('.sign-in-dialog');
    expect(dialog?.open).toBeTrue();
    expect(compiled.querySelectorAll('.sign-in-dialog')).toHaveSize(1);
    expect(compiled.querySelector('.site-header .provider-action')).toBeNull();
    dialog?.querySelector<HTMLButtonElement>('.dialog-close')?.click();

    expect(document.activeElement).toBe(headerSignIn);
    compiled.querySelector<HTMLButtonElement>('.sign-in-button')?.click();
    fixture.detectChanges();
    expect(dialog?.open).toBeTrue();
    dialog?.querySelector<HTMLButtonElement>('.dialog-close')?.click();
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
});
