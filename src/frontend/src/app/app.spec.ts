import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideAuth } from 'angular-auth-oidc-client';
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
        provideAuth({
          config: {
            authority: environment.oidc.authority,
            clientId: environment.oidc.clientId,
            redirectUrl: environment.oidc.redirectUrl,
            postLogoutRedirectUri: environment.oidc.postLogoutRedirectUri,
            responseType: 'code',
            scope: environment.oidc.scope,
            autoUserInfo: true,
          },
        }),
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
    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');

    httpTestingController
      .expectOne(`${apiOrigin}/api/public/ping`)
      .flush({
        status: 'ok',
        applicationName: 'OIDC Starter API',
        timestampUtc: '2026-04-14T00:00:00Z',
        oidcConfigured: false,
      });

    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('OIDC Starter UI');
  });
});
