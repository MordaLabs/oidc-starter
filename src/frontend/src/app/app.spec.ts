import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideBffAuth, provideSpaOidcAuth } from '@mordalabs/oidc-starter-auth';
import { App } from './app';
import { LOCALE_STORAGE_KEY } from './locale.service';
import { environment } from '../environments/environment';

describe('App', () => {
  let httpTestingController: HttpTestingController;
  let testDescription: HTMLMetaElement | null = null;

  beforeEach(async () => {
    localStorage.setItem(LOCALE_STORAGE_KEY, 'en');
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
    localStorage.removeItem(LOCALE_STORAGE_KEY);
    testDescription?.remove();
    testDescription = null;
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

  it('switches the visible copy from the header without navigation or new requests', () => {
    testDescription = document.createElement('meta');
    testDescription.name = 'description';
    document.head.appendChild(testDescription);
    const fixture = TestBed.createComponent(App);
    flushPing();
    fixture.detectChanges();
    flushBffCurrentUserIfRendered();
    flushBffLoginProvidersIfRequested();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const selector = compiled.querySelector<HTMLElement>('.language-selector')!;
    const english = selector.querySelector<HTMLButtonElement>('button:first-of-type')!;
    const polish = selector.querySelector<HTMLButtonElement>('button:last-of-type')!;
    const providersLink = compiled.querySelector<HTMLAnchorElement>('.site-header nav a[href="#providers"]');
    const initialUrl = window.location.href;

    expect(selector.getAttribute('role')).toBe('group');
    expect(document.documentElement.lang).toBe('en');
    expect(document.title).toBe('OIDC Starter — Provider-neutral BFF authentication');
    expect(testDescription.content).toContain('Provider-neutral BFF authentication');
    expect(selector.getAttribute('aria-label')).toBe('Language');
    expect(english.getAttribute('aria-label')).toBe('English');
    expect(english.getAttribute('aria-pressed')).toBe('true');
    expect(polish.getAttribute('aria-pressed')).toBe('false');
    polish.focus();
    expect(document.activeElement).toBe(polish);
    polish.click();
    fixture.detectChanges();

    expect(selector.getAttribute('aria-label')).toBe('Język');
    expect(document.documentElement.lang).toBe('pl');
    expect(document.title).toBe('OIDC Starter — uwierzytelnianie BFF niezależne od dostawcy');
    expect(testDescription.content).toContain('Uwierzytelnianie BFF dla Angular');
    expect(selector.getAttribute('lang')).toBe('pl');
    expect(polish.getAttribute('aria-label')).toBe('Polski');
    expect(polish.getAttribute('aria-pressed')).toBe('true');
    expect(english.getAttribute('aria-pressed')).toBe('false');
    expect(compiled.querySelector('#overview .eyebrow')?.textContent?.trim())
      .toBe('Uwierzytelnianie BFF niezależne od dostawcy');
    expect(localStorage.getItem(LOCALE_STORAGE_KEY)).toBe('pl');
    expect(compiled.querySelector('.site-header nav a[href="#providers"]')).toBe(providersLink);
    expect(providersLink?.getAttribute('href')).toBe('#providers');
    expect(window.location.href).toBe(initialUrl);
    expect(httpTestingController.match(() => true)).toHaveSize(0);

    english.click();
    fixture.detectChanges();
    expect(compiled.querySelector('#overview .eyebrow')?.textContent?.trim())
      .toBe('Provider-neutral BFF authentication');
    expect(english.getAttribute('aria-pressed')).toBe('true');
    expect(localStorage.getItem(LOCALE_STORAGE_KEY)).toBe('en');
    expect(document.documentElement.lang).toBe('en');
  });

  it('renders Polish copy across the root page while preserving anchors and technical content', () => {
    const fixture = TestBed.createComponent(App);
    flushPing();
    fixture.detectChanges();
    flushBffCurrentUserIfRendered();
    flushBffLoginProvidersIfRequested();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const headerLinks = Array.from(compiled.querySelectorAll<HTMLAnchorElement>('.site-header nav a'));
    const originalTargets = headerLinks.map((link) => link.getAttribute('href'));
    const nonLegalLinks = () => Array.from(compiled.querySelectorAll<HTMLAnchorElement>('a'))
      .filter((link) => !link.closest('.footer-legal-navigation'))
      .map((link) => link.getAttribute('href'));
    const originalLinks = nonLegalLinks();
    compiled.querySelector<HTMLButtonElement>('.language-selector button:last-of-type')!.click();
    fixture.detectChanges();

    expect(compiled.querySelector('.skip-link')?.textContent).toContain('Przejdź do głównej treści');
    expect(compiled.querySelector('.site-header nav')?.getAttribute('aria-label')).toBe('Nawigacja główna');
    expect(headerLinks.map((link) => link.getAttribute('href')).filter((href) => !href?.startsWith('/premium/')))
      .toEqual(originalTargets.filter((href) => !href?.startsWith('/premium/')));
    expect(nonLegalLinks().filter((href) => !href?.startsWith('/premium/')))
      .toEqual(originalLinks.filter((href) => !href?.startsWith('/premium/')));
    expect(compiled.querySelector('.site-header nav a[href="/premium/pl/"]')).not.toBeNull();
    expect(compiled.querySelector('#premium-family a[href="/premium/pl/sessions/"]')).not.toBeNull();
    expect(compiled.querySelector('#premium-family a.primary-link[href="/premium/pl/"]')).not.toBeNull();
    expect(compiled.querySelector('.footer-primary-navigation a[href="/premium/pl/"]')).not.toBeNull();
    expect(headerLinks[0].textContent?.trim()).toBe('Przegląd');
    expect(compiled.querySelector('#hero-title')?.textContent).toContain('Jeden proces logowania');
    expect(compiled.querySelector('#overview .hero-demo-note')?.textContent).toContain('Dostępne sposoby logowania');
    expect(compiled.querySelector('#providers-title')?.textContent).toContain('OIDC u podstaw');
    expect(compiled.querySelector('#how-it-works-title')?.textContent).toContain('Od skonfigurowanego dostawcy');
    expect(compiled.querySelector('#demo-title')?.textContent).toContain('Wypróbuj proces');
    expect(compiled.querySelector('#security-title')?.textContent).toContain('Jasne granice');
    expect(compiled.querySelector('#premium-family-title')?.textContent).toContain('Podstawa, która rośnie');
    expect(compiled.querySelector('#get-started-title')?.textContent).toContain('Dodaj OIDC Starter');
    expect(compiled.querySelector('#support-title')?.textContent).toContain('Masz pytanie');
    expect(compiled.querySelector('#github-footer-title')?.textContent).toContain('Zbuduj proces logowania');
    expect(compiled.querySelector('nav.footer-legal-navigation')?.getAttribute('aria-label')).toBe('Nawigacja prawna w stopce');
    expect(compiled.querySelector('nav.footer-legal-navigation a[href="/pl/privacy.html"]')?.textContent).toBe('Prywatność');
    expect(compiled.querySelector('#get-started pre code')?.textContent).toContain('dotnet add package OidcStarter.AspNetCore.Bff');
    expect(compiled.querySelector('#get-started .package-name')?.textContent).toBe('OidcStarter.AspNetCore.Bff');
    expect(compiled.querySelector('a[href="https://github.com/MordaLabs/oidc-starter"]')).not.toBeNull();
    expect(httpTestingController.match(() => true)).toHaveSize(0);
  });

  it('localizes runtime status and UTC timestamps when the language changes', () => {
    const fixture = TestBed.createComponent(App);
    const timestampUtc = '2026-08-06T11:31:19.0631303+00:00';
    httpTestingController.expectOne(`${environment.apiOrigin.replace(/\/$/, '')}/api/public/ping`).flush({
      status: 'ok', applicationName: 'OIDC Starter API', timestampUtc, oidcConfigured: false,
    });
    fixture.detectChanges();
    flushBffCurrentUserIfRendered();
    flushBffLoginProvidersIfRequested();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const time = compiled.querySelector<HTMLTimeElement>('.runtime-details time')!;
    expect(time.textContent?.trim()).toBe('6 Aug 2026, 11:31 UTC');
    expect(compiled.querySelector('.runtime-details')?.textContent).toContain('No');
    compiled.querySelector<HTMLButtonElement>('.language-selector button:last-of-type')!.click();
    fixture.detectChanges();

    expect(Array.from(compiled.querySelectorAll('.runtime-details dt')).map((label) => label.textContent?.trim()))
      .toEqual(['Stan', 'Aplikacja', 'Skonfigurowano OIDC', 'Ostatnio sprawdzono']);
    expect(compiled.querySelector('.runtime-details')?.textContent).toContain('Działa');
    expect(compiled.querySelector('.runtime-details')?.textContent).toContain('Nie');
    expect(time.getAttribute('datetime')).toBe(timestampUtc);
    expect(time.textContent?.trim()).toBe('6 sie 2026, 11:31 UTC');
    expect(httpTestingController.match(() => true)).toHaveSize(0);
  });

  it('uses a saved Polish preference for loading and backend error copy', () => {
    localStorage.setItem(LOCALE_STORAGE_KEY, 'pl');
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Trwa sprawdzanie publicznego punktu końcowego');
    expect(document.documentElement.lang).toBe('pl');
    httpTestingController.expectOne(`${environment.apiOrigin.replace(/\/$/, '')}/api/public/ping`).flush(null, {
      status: 0, statusText: 'Unavailable',
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Interfejs API serwera jest obecnie niedostępny');
    flushBffCurrentUserIfRendered();
    flushBffLoginProvidersIfRequested();
  });

  it('updates all legal links and labels when switching between English and Polish', () => {
    const fixture = TestBed.createComponent(App);
    flushPing();
    fixture.detectChanges();
    flushBffCurrentUserIfRendered();
    flushBffLoginProvidersIfRequested();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const links = Array.from(compiled.querySelectorAll<HTMLAnchorElement>('.footer-legal-navigation a'));
    const targets = () => links.map((link) => link.getAttribute('href'));
    const labels = () => links.map((link) => link.textContent?.trim());
    expect(targets()).toEqual(['/privacy.html', '/terms.html', '/data-deletion.html']);
    expect(labels()).toEqual(['Privacy', 'Terms', 'Data deletion']);

    compiled.querySelector<HTMLButtonElement>('.language-selector button:last-of-type')!.click();
    fixture.detectChanges();
    expect(targets()).toEqual(['/pl/privacy.html', '/pl/terms.html', '/pl/data-deletion.html']);
    expect(labels()).toEqual(['Prywatność', 'Warunki korzystania', 'Usuwanie danych']);
    expect(compiled.querySelector('.footer-legal-navigation')?.getAttribute('aria-label'))
      .toBe('Nawigacja prawna w stopce');

    compiled.querySelector<HTMLButtonElement>('.language-selector button:first-of-type')!.click();
    fixture.detectChanges();
    expect(targets()).toEqual(['/privacy.html', '/terms.html', '/data-deletion.html']);
    expect(labels()).toEqual(['Privacy', 'Terms', 'Data deletion']);
  });

  it('initializes all legal links in Polish from the saved locale', () => {
    localStorage.setItem(LOCALE_STORAGE_KEY, 'pl');
    const fixture = TestBed.createComponent(App);
    flushPing();
    fixture.detectChanges();
    flushBffCurrentUserIfRendered();
    flushBffLoginProvidersIfRequested();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const links = Array.from(compiled.querySelectorAll<HTMLAnchorElement>('.footer-legal-navigation a'));
    expect(links.map((link) => link.getAttribute('href')))
      .toEqual(['/pl/privacy.html', '/pl/terms.html', '/pl/data-deletion.html']);
    expect(links.map((link) => link.textContent?.trim()))
      .toEqual(['Prywatność', 'Warunki korzystania', 'Usuwanie danych']);
    expect(document.documentElement.lang).toBe('pl');
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
    const primaryNavigationLinks = Array.from(compiled.querySelectorAll<HTMLAnchorElement>('nav[aria-label="Primary navigation"] a'));
    expect(primaryNavigationLinks.map((link) => link.getAttribute('href')))
      .toEqual(['#overview', '#providers', '#how-it-works', '#demo', '#security', '#get-started', '/premium/', '#github']);
    expect(primaryNavigationLinks.map((link) => link.textContent?.trim()))
      .toEqual(['Overview', 'Providers', 'How it works', 'Live demo', 'Security', 'Get started', 'Premium', 'GitHub']);
    expect(compiled.querySelector('.site-header .header-action')).toBeNull();
    expect(compiled.querySelectorAll('.site-header .language-selector button')).toHaveSize(2);
    expect(compiled.querySelector('#overview .hero-actions a[href="#demo"]')?.textContent).toContain('Explore live demo');
    expect(compiled.querySelector('#overview .hero-demo-card a[href="#demo"]')?.textContent).toContain('Open live demo');
    expect(compiled.querySelector('#overview .hero-demo-note')?.textContent).toContain('depend on the demo');
    expect(Array.from(compiled.querySelectorAll('.hero-benefits strong')).map((item) => item.textContent?.trim()))
      .toEqual(['Secure by default', 'Multiple providers', 'Ready for production', 'Open source']);
    expect(compiled.querySelectorAll('.hero-benefits li .visual-icon[aria-hidden="true"]')).toHaveSize(4);
    expect(Array.from(compiled.querySelector('main')!.querySelectorAll(':scope > section')).map((section) => section.id))
      .toEqual(['overview', 'providers', 'how-it-works', 'demo', 'security', 'premium-family', 'get-started']);
    const premiumFamily = compiled.querySelector('#premium-family');
    expect(premiumFamily?.textContent).toContain('OSS provides the safe BFF authentication foundation');
    expect(premiumFamily?.textContent).toContain('Premium Sessions');
    expect(premiumFamily?.textContent).toContain('Available now');
    expect(premiumFamily?.textContent).toContain('Next Premium module');
    expect(premiumFamily?.textContent).toContain('Under discovery');
    expect(premiumFamily?.textContent).toContain('No capability or release date is announced.');
    expect(premiumFamily?.querySelector('a[href="/premium/"]')?.textContent).toContain('Explore OIDC Starter Premium');
    expect(premiumFamily?.querySelector('a[href="/premium/sessions/"]')?.textContent).toContain('Explore the module');
    expect(premiumFamily?.querySelectorAll('.premium-family-card .visual-icon[aria-hidden="true"]')).toHaveSize(2);
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
    expect(frontendCard?.textContent).toContain('@mordalabs/oidc-starter-auth');
    expect(frontendCard?.textContent).toContain('Your application owns the buttons, modal, icons, layout, and styling.');
    expect(frontendCard?.querySelector('pre code')?.textContent?.trim()).toBe('npm install @mordalabs/oidc-starter-auth');
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
    expect(providersSection?.querySelector('.section-heading > .eyebrow')?.textContent?.trim()).toBe('Providers');
    expect(providersSection?.querySelector('.provider-model-heading > .eyebrow')?.textContent?.trim()).toBe('External Login Providers');
    expect(providersSection?.querySelector('#providers-title')?.textContent)
      .toContain('OIDC at the core. More ways to sign in.');
    expect(providersSection?.textContent).toContain('OpenID Connect remains the built-in BFF default.');
    expect(providersSection?.textContent).toContain('Add Google, GitHub, Facebook, or a custom provider registration');
    expect(providersSection?.textContent).toContain('only configured providers at runtime');
    expect(Array.from(providersSection?.querySelectorAll('.provider-list li') ?? []).map((provider) => provider.textContent?.trim()))
      .toEqual(['OpenID Connect', 'Google', 'GitHub', 'Facebook', 'Custom provider registration']);
    expect(providersSection?.textContent).not.toContain('Facebook and GitHub are OIDC providers');
    expect(providersSection?.querySelectorAll('button')).toHaveSize(0);

    const securitySection = compiled.querySelector<HTMLElement>('#security');
    expect(securitySection?.querySelector('.eyebrow')?.textContent?.trim()).toBe('Security');
    expect(securitySection?.querySelector('#security-title')?.textContent?.trim()).toBe('Security boundaries that stay legible');
    expect(Array.from(securitySection?.querySelectorAll<HTMLElement>('.capability h3') ?? []).map((heading) => heading.textContent?.trim()))
      .toEqual(['Server-side session', 'Provider-aware identity', 'Application-owned authorization', 'Validated OIDC sign-in metadata']);
    const securityText = securitySection?.textContent ?? '';
    expect(securityText).toContain('backend session instead of receiving provider tokens through the current-user response');
    expect(securityText).toContain('HTTP-only, secure');
    expect(securityText).toContain('__Host-');
    expect(securityText).toContain('return 401');
    expect(securityText).toContain('without turning external profile data into authorization');
    expect(securityText).toContain('business authorization decisions');
    expect(securityText).toContain('authoritative issuer, subject, provider, and session metadata');

    const endpointProtectionPanel = securitySection?.querySelector<HTMLElement>('.endpoint-protection-panel');
    expect(securitySection?.querySelectorAll('.endpoint-protection-panel')).toHaveSize(1);
    expect(endpointProtectionPanel?.querySelector('#endpoint-protection-title')?.textContent)
      .toContain('Public entry points, protected session data, and guarded state changes');
    expect(Array.from(endpointProtectionPanel?.querySelectorAll<HTMLElement>('.endpoint-boundary-group dt') ?? []).map((heading) => heading.textContent?.trim()))
      .toEqual(['Public entry points', 'Authenticated session identity', 'Guarded state change']);
    expect(endpointProtectionPanel?.textContent).toContain('GET /api/auth/providers');
    expect(endpointProtectionPanel?.textContent).toContain('GET /api/auth/login');
    expect(endpointProtectionPanel?.textContent).toContain('GET /api/auth/login/{provider}');
    expect(endpointProtectionPanel?.textContent).toContain('GET /api/auth/csrf');
    expect(endpointProtectionPanel?.textContent).toContain('GET /api/auth/me');
    expect(endpointProtectionPanel?.textContent).toContain('POST /api/auth/logout');
    expect(endpointProtectionPanel?.textContent).toContain('401 Unauthorized');
    expect(endpointProtectionPanel?.textContent).toContain('antiforgery');
    expect(endpointProtectionPanel?.textContent).toContain('Origin');
    expect(endpointProtectionPanel?.textContent).toContain('Referer');
    expect(endpointProtectionPanel?.textContent).toContain('providers registered by the server application');
    expect(endpointProtectionPanel?.textContent).toContain('unknown provider IDs are rejected');
    expect(endpointProtectionPanel?.textContent).toContain('antiforgery validation is the primary CSRF protection');
    expect(endpointProtectionPanel?.textContent).toContain('origin check is defense in depth');
    expect(endpointProtectionPanel?.textContent).toContain('HTTPS');
    expect(endpointProtectionPanel?.textContent).toContain('trusted frontend origins');
    expect(endpointProtectionPanel?.textContent).toContain('reverse proxies');
    expect(endpointProtectionPanel?.textContent).toContain('secret storage');
    expect(endpointProtectionPanel?.textContent).toContain('application-specific authorization');
    expect(endpointProtectionPanel?.textContent).toContain('POST, PUT, PATCH, and DELETE');
    expect(securityText).not.toContain('unhackable');
    expect(securityText).not.toContain('enterprise-grade');
    expect(securityText).not.toContain('fully secure');
    expect(securityText).not.toContain('credentials');
    expect(securityText).not.toContain('session=');
    expect(securityText).not.toContain('access-token');
    expect(securityText).not.toContain('refresh-token');

    const howItWorks = compiled.querySelector<HTMLElement>('main > #how-it-works');
    expect(compiled.querySelectorAll('#how-it-works')).toHaveSize(1);
    expect(providersSection?.querySelector('#how-it-works')).toBeNull();
    expect(howItWorks).not.toBeNull();
    expect(howItWorks?.querySelector('#how-it-works-title')?.textContent)
      .toContain('From configured provider to application session');
    expect(howItWorks?.querySelectorAll('ol')).toHaveSize(1);
    expect(Array.from(howItWorks?.querySelectorAll<HTMLElement>('.flow-step h3') ?? []).map((heading) => heading.textContent?.trim()))
      .toEqual([
        'Discover providers',
        'Render your chooser',
        'Authenticate',
        'Use the session',
      ]);

    const flowText = howItWorks?.textContent ?? '';
    expect(Array.from(howItWorks?.querySelectorAll('.flow-step') ?? [])).toHaveSize(4);
    expect(Array.from(howItWorks?.querySelectorAll('.flow-description') ?? [])).toHaveSize(4);
    expect(Array.from(howItWorks?.querySelectorAll('.flow-technical') ?? [])).toHaveSize(4);
    for (const step of Array.from(howItWorks?.querySelectorAll<HTMLElement>('.flow-step') ?? [])) {
      expect(step.querySelectorAll('.flow-description')).toHaveSize(1);
      expect(step.querySelectorAll('.flow-technical')).toHaveSize(1);
      expect(step.querySelectorAll('.flow-technical code')).toHaveSize(1);
    }
    expect(flowText).toContain('/api/auth/providers');
    expect(flowText).toContain('provider.id');
    expect(flowText).toContain('login(provider.id)');
    expect(flowText).toContain('/api/auth/me');
    expect(flowText).not.toContain('provider.loginUrl');
    expect(flowText).not.toContain('ClientId');
    expect(flowText).not.toContain('ClientSecret');
    expect(flowText).not.toContain('headless');
    expect(flowText).not.toContain('No chooser required');
    expect(flowText).not.toContain('Provider tokens are not included');
    expect(howItWorks?.querySelector('.ownership-boundaries')).toBeNull();

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
      .toBe('https://www.npmjs.com/package/@mordalabs/oidc-starter-auth');
    expect(getStarted?.querySelector('.get-started-guide')?.getAttribute('href'))
      .toBe('https://github.com/MordaLabs/oidc-starter');
    expect(getStarted?.querySelector('.get-started-next')?.textContent)
      .toContain('provider-selection modal is a reference UI built by the sample application');
    expect(getStarted?.querySelector('.get-started-clarification')?.textContent)
      .toContain('in-repository projects for local development');
    expect(httpTestingController.match(() => true)).toHaveSize(0);

    const supportSection = compiled.querySelector<HTMLElement>('#contact');
    expect(supportSection?.querySelector('#support-title')?.textContent)
      .toContain('Questions, feedback, or found an issue?');
    const contactEmail = supportSection?.querySelector<HTMLAnchorElement>('a[href="mailto:contact@mordalabs.com"]');
    expect(contactEmail?.textContent).toContain('contact@mordalabs.com');
    expect(Array.from(supportSection?.querySelectorAll<HTMLElement>('.support-action h3') ?? []).map((heading) => heading.textContent?.trim()))
      .toEqual(['Explore the project', 'Report a bug', 'Request a feature']);
    const supportLinks = Array.from(supportSection?.querySelectorAll<HTMLAnchorElement>('.support-link') ?? []);
    expect(supportLinks.map((link) => link.href))
      .toEqual([
        'https://github.com/MordaLabs/oidc-starter',
        'https://github.com/MordaLabs/oidc-starter/issues',
        'https://github.com/MordaLabs/oidc-starter/issues',
        'https://github.com/MordaLabs/oidc-starter/security/advisories/new',
      ]);
    expect(supportLinks.map((link) => link.target)).toEqual(['_blank', '_blank', '_blank', '_blank']);
    expect(supportLinks.map((link) => link.rel))
      .toEqual(['noopener noreferrer', 'noopener noreferrer', 'noopener noreferrer', 'noopener noreferrer']);
    expect(supportSection?.textContent).toContain("Please don't open a public issue.");
    const privateSecurityReport = supportSection?.querySelector<HTMLAnchorElement>('.support-security .support-link');
    expect(privateSecurityReport?.textContent).toContain('Report privately');
    expect(privateSecurityReport?.href).toBe('https://github.com/MordaLabs/oidc-starter/security/advisories/new');
    expect(privateSecurityReport?.href).not.toBe('https://github.com/MordaLabs/oidc-starter/issues');

    const footer = compiled.querySelector<HTMLElement>('footer#github');
    expect(footer).not.toBeNull();
    expect(footer?.querySelector('#github-footer-title')?.textContent)
      .toContain('Build the sign-in flow once. Keep the rest of your app yours.');
    expect(footer?.textContent?.replace(/\s+/g, ' '))
      .toContain('OIDC Starter is an ASP.NET Core BFF toolkit for OpenID Connect and social login, with runtime provider discovery, application-owned authorization, and an optional headless Angular client.');
    expect(footer?.querySelector('small')?.textContent)
      .toContain('Provider-neutral BFF authentication for Angular + ASP.NET Core');
    expect(footer?.textContent).toContain('Morda Labs · open-source authentication starter');
    expect(footer?.textContent)
      .toContain('Explore the source, run the sample, and adapt only the pieces your product needs.');

    const repositoryLinks = Array.from(footer?.querySelectorAll<HTMLAnchorElement>('.footer-repository a[href="https://github.com/MordaLabs/oidc-starter"]') ?? []);
    expect(repositoryLinks).toHaveSize(1);
    expect(repositoryLinks[0].textContent?.trim()).toBe('View on GitHub');
    expect(repositoryLinks[0].target).toBe('_blank');
    expect(repositoryLinks[0].rel).toBe('noopener noreferrer');
    const primaryFooterLinks = Array.from(compiled.querySelectorAll<HTMLAnchorElement>('nav[aria-label="Footer primary navigation"] a'));
    expect(primaryFooterLinks.map((link) => link.getAttribute('href')))
      .toEqual(['#overview', '#providers', '#how-it-works', '#demo', '#security', '#get-started', '/premium/', '#contact']);
    expect(primaryFooterLinks.map((link) => link.textContent?.trim()))
      .toEqual(['Overview', 'Providers', 'How it works', 'Live demo', 'Security', 'Get started', 'Premium', 'Contact']);

    const legalFooterLinks = Array.from(compiled.querySelectorAll<HTMLAnchorElement>('nav[aria-label="Footer legal navigation"] a'));
    expect(legalFooterLinks.map((link) => link.getAttribute('href')))
      .toEqual(['/privacy.html', '/terms.html', '/data-deletion.html']);
    expect(legalFooterLinks.map((link) => link.textContent?.trim()))
      .toEqual(['Privacy', 'Terms', 'Data deletion']);
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

  it('renders all built-in provider statuses from discovery', () => {
    if (environment.authMode !== 'bff') {
      return;
    }

    const fixture = TestBed.createComponent(App);
    flushPing();
    fixture.detectChanges();
    flushBffCurrentUserIfRendered();

    httpTestingController.expectOne(`${environment.apiOrigin.replace(/\/$/, '')}/api/auth/providers`).flush([
      { id: 'oidc', displayName: 'OpenID Connect', isDefault: true, loginUrl: '/ignored/oidc' },
      { id: 'google', displayName: 'Google', isDefault: false, loginUrl: '/ignored/google' },
      { id: 'github', displayName: 'GitHub', isDefault: false, loginUrl: '/ignored/github' },
      { id: 'facebook', displayName: 'Facebook', isDefault: false, loginUrl: '/ignored/facebook' },
    ]);
    fixture.detectChanges();

    const statuses = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.runtime-provider-statuses li'),
    ).map((item) => item.textContent?.replace(/\s+/g, ' ').trim());
    expect(statuses).toEqual(['OpenID Connect Yes', 'Google Yes', 'GitHub Yes', 'Facebook Yes']);
  });

  it('renders absent built-in providers as not configured', () => {
    if (environment.authMode !== 'bff') {
      return;
    }

    const fixture = TestBed.createComponent(App);
    flushPing();
    fixture.detectChanges();
    flushBffCurrentUserIfRendered();

    httpTestingController.expectOne(`${environment.apiOrigin.replace(/\/$/, '')}/api/auth/providers`).flush([
      { id: 'oidc', displayName: 'OpenID Connect', isDefault: true, loginUrl: '/ignored/oidc' },
      { id: 'google', displayName: 'Google', isDefault: false, loginUrl: '/ignored/google' },
    ]);
    fixture.detectChanges();

    const statuses = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.runtime-provider-statuses li'),
    ).map((item) => item.textContent?.replace(/\s+/g, ' ').trim());
    expect(statuses).toEqual(['OpenID Connect Yes', 'Google Yes', 'GitHub No', 'Facebook No']);
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
    expect(fixture.nativeElement.querySelectorAll('.site-header .language-selector button')).toHaveSize(2);
    expect(fixture.nativeElement.querySelector('#overview .hero-actions a[href="#demo"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sign-in-dialog')).toBeNull();
  });

  it('keeps BFF sign-in in the live demo without duplicating provider discovery', () => {
    if (environment.authMode !== 'bff') {
      return;
    }

    const fixture = TestBed.createComponent(App);
    flushPing();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelectorAll('.site-header .language-selector button')).toHaveSize(2);
    expect(compiled.querySelector('.unauthenticated-sign-in')).toBeNull();
    expect(authenticatedCardHeadings(compiled)).toEqual([]);

    flushBffCurrentUserIfRendered();
    fixture.detectChanges();

    const providerRequest = httpTestingController.expectOne(`${environment.apiOrigin.replace(/\/$/, '')}/api/auth/providers`);
    providerRequest.flush([]);
    fixture.detectChanges();

    expect(compiled.querySelector('.unauthenticated-state')?.textContent).toContain('No active session');
    expect(authenticatedCardHeadings(compiled)).toEqual([]);
    const demoSignIn = compiled.querySelector<HTMLButtonElement>('.unauthenticated-sign-in');
    demoSignIn?.click();
    fixture.detectChanges();

    const dialog = compiled.querySelector<HTMLDialogElement>('.sign-in-dialog');
    expect(dialog?.open).toBeTrue();
    expect(compiled.querySelectorAll('.sign-in-dialog')).toHaveSize(1);
    dialog?.querySelector<HTMLButtonElement>('.dialog-close')?.click();
    expect(document.activeElement).toBe(demoSignIn);
    expect(httpTestingController.match(`${environment.apiOrigin.replace(/\/$/, '')}/api/auth/providers`)).toHaveSize(0);
  });

  it('keeps authenticated session controls in the demo, outside the global header', () => {
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
    expect(compiled.querySelector('.site-header .header-button')).toBeNull();
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

describe('App invitation visibility', () => {
  const originalMode = environment.authMode;
  const originalFlag = environment.invitationRequestUiEnabled;

  afterEach(() => {
    environment.authMode = originalMode;
    environment.invitationRequestUiEnabled = originalFlag;
    localStorage.removeItem(LOCALE_STORAGE_KEY);
  });

  for (const enabled of [false, true]) {
    it(`renders the request form in BFF mode only when the flag is ${enabled}`, async () => {
      environment.authMode = 'bff';
      environment.invitationRequestUiEnabled = enabled;
      localStorage.setItem(LOCALE_STORAGE_KEY, 'en');
      await TestBed.configureTestingModule({
        imports: [App],
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
          provideBffAuth({ apiOrigin: '' }),
        ],
      }).compileComponents();

      const http = TestBed.inject(HttpTestingController);
      const fixture = TestBed.createComponent(App);
      http.expectOne('/api/public/ping').flush({
        status: 'ok', applicationName: 'Demo', timestampUtc: '2026-04-14T00:00:00Z', oidcConfigured: true,
      });
      fixture.detectChanges();
      http.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
      fixture.detectChanges();
      http.match('/api/auth/providers').forEach((request) => request.flush([]));
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('app-bff-auth-view')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('app-invitation-request') !== null).toBe(enabled);
      expect(http.match('/api/demo/invitation-requests')).toHaveSize(0);
      expect(http.match('/api/auth/csrf')).toHaveSize(0);
      http.verify();
    });
  }

  it('hides the request form in SPA mode even when the flag is enabled', async () => {
    environment.authMode = 'spa';
    environment.invitationRequestUiEnabled = true;
    localStorage.setItem(LOCALE_STORAGE_KEY, 'en');
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideSpaOidcAuth(environment.oidc)],
    }).compileComponents();
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(App);
    http.expectOne('/api/public/ping').flush({
      status: 'ok', applicationName: 'Demo', timestampUtc: '2026-04-14T00:00:00Z', oidcConfigured: true,
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-spa-auth-view')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-invitation-request')).toBeNull();
    expect(http.match('/api/demo/invitation-requests')).toHaveSize(0);
    http.verify();
  });
});

describe('App operator dashboard visibility', () => {
  const originalMode = environment.authMode;
  const originalFlag = environment.operatorDashboardUiEnabled;

  afterEach(() => {
    environment.authMode = originalMode;
    environment.operatorDashboardUiEnabled = originalFlag;
    localStorage.removeItem(LOCALE_STORAGE_KEY);
  });

  for (const [enabled, authenticated] of [[false, true], [true, false], [true, true]] as const) {
    it(`gates the BFF entry for flag=${enabled} and authenticated=${authenticated}`, async () => {
      environment.authMode = 'bff';
      environment.operatorDashboardUiEnabled = enabled;
      localStorage.setItem(LOCALE_STORAGE_KEY, 'en');
      await TestBed.configureTestingModule({
        imports: [App],
        providers: [provideHttpClient(), provideHttpClientTesting(), provideBffAuth({ apiOrigin: '' })],
      }).compileComponents();
      const http = TestBed.inject(HttpTestingController);
      const fixture = TestBed.createComponent(App);
      http.expectOne('/api/public/ping').flush({
        status: 'ok', applicationName: 'Demo', timestampUtc: '2026-04-14T00:00:00Z', oidcConfigured: true,
      });
      fixture.detectChanges();
      http.expectOne('/api/auth/me').flush(authenticated
        ? { isAuthenticated: true, sub: 'operator', name: 'Operator', roles: [], externalIdentity: null }
        : null, authenticated ? undefined : { status: 401, statusText: 'Unauthorized' });
      fixture.detectChanges();
      http.match('/api/auth/providers').forEach((request) => request.flush([]));
      fixture.detectChanges();

      const entry = fixture.nativeElement.querySelector('.workspace-entry');
      expect(entry !== null).toBe(enabled && authenticated);
      expect(http.match((request) => request.url.startsWith('/api/demo/operator/'))).toHaveSize(0);
      expect(fixture.nativeElement.querySelector('app-bff-auth-view')).not.toBeNull();
      http.verify();
    });
  }

  it('does not render the dashboard in SPA mode', async () => {
    environment.authMode = 'spa';
    environment.operatorDashboardUiEnabled = true;
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideSpaOidcAuth(environment.oidc)],
    }).compileComponents();
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(App);
    http.expectOne('/api/public/ping').flush({
      status: 'ok', applicationName: 'Demo', timestampUtc: '2026-04-14T00:00:00Z', oidcConfigured: true,
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-operator-dashboard')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-spa-auth-view')).not.toBeNull();
    http.verify();
  });
});
