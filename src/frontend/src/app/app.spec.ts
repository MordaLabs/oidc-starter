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

  it('should render title', () => {
    const fixture = TestBed.createComponent(App);

    flushPing();

    fixture.detectChanges();
    flushBffCurrentUserIfRendered();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('OIDC Starter UI');
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
});
