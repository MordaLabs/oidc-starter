import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal, WritableSignal } from '@angular/core';
import { Subject } from 'rxjs';
import { BffAuthService, type BffCurrentUser, type BffLoginProvider } from '@mordalabs/oidc-starter-auth';
import { BFF_CLIPBOARD_WRITER } from './bff-clipboard-writer';
import { BffAuthViewComponent } from './bff-auth-view.component';
import { BffAuthViewService } from './bff-auth-view.service';
import { LOCALE_STORAGE_KEY, LocaleService } from '../../locale.service';

type BffAuthServiceStub = {
  isLoading: WritableSignal<boolean>;
  isLoggingOut: WritableSignal<boolean>;
  authenticated: WritableSignal<boolean>;
  currentUser: WritableSignal<BffCurrentUser | null>;
  getLoginProviders: jasmine.Spy;
  login: jasmine.Spy;
  logout: jasmine.Spy;
};

describe('BffAuthViewComponent', () => {
  let fixture: ComponentFixture<BffAuthViewComponent>;
  let auth: BffAuthServiceStub;
  let providerResponses: Subject<readonly BffLoginProvider[]>;
  let clipboardWriter: jasmine.Spy;

  beforeEach(async () => {
    localStorage.setItem(LOCALE_STORAGE_KEY, 'en');
    providerResponses = new Subject<readonly BffLoginProvider[]>();
    clipboardWriter = jasmine.createSpy('clipboardWriter').and.resolveTo();
    auth = {
      isLoading: signal(false),
      isLoggingOut: signal(false),
      authenticated: signal(false),
      currentUser: signal(null),
      getLoginProviders: jasmine.createSpy('getLoginProviders').and.returnValue(providerResponses),
      login: jasmine.createSpy('login'),
      logout: jasmine.createSpy('logout'),
    };

    await TestBed.configureTestingModule({
      imports: [BffAuthViewComponent],
      providers: [
        { provide: BffAuthService, useValue: auth },
        { provide: BFF_CLIPBOARD_WRITER, useValue: clipboardWriter },
      ],
    }).compileComponents();
  });

  afterEach(() => {
    localStorage.removeItem(LOCALE_STORAGE_KEY);
    TestBed.resetTestingModule();
  });

  it('renders one unauthenticated sign-in action after the session check without technical cards', async () => {
    await createComponent();

    const rendered = fixture.nativeElement as HTMLElement;

    expect(signInButton()).not.toBeNull();
    expect(rendered.querySelectorAll('button.unauthenticated-sign-in')).toHaveSize(1);
    expect(rendered.querySelector('.unauthenticated-state')?.textContent).toContain('No active session');
    expect(cardHeadings()).toEqual([]);
    expect(rendered.querySelector('.current-user-inspector')).toBeNull();
    expect(dialog().open).toBeFalse();
    expect(providerActions()).toHaveSize(0);
    expect(rendered.querySelector('.card .provider-action')).toBeNull();
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('shows a neutral loading state during the initial backend session check without discovering or invoking login', async () => {
    auth.isLoading.set(true);
    await createComponent();

    const rendered = fixture.nativeElement as HTMLElement;

    expect(rendered.querySelector('.unauthenticated-state')?.textContent).toContain('Checking backend session...');
    expect(signInButton()).toBeNull();
    expect(cardHeadings()).toEqual([]);
    expect(dialog().open).toBeFalse();
    expect(auth.getLoginProviders).not.toHaveBeenCalled();
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('updates loading and signing-out copy without changing session state or invoking auth actions', async () => {
    auth.isLoading.set(true);
    await createComponent();
    const locale = TestBed.inject(LocaleService);
    const rendered = fixture.nativeElement as HTMLElement;

    locale.selectLocale('pl');
    fixture.detectChanges();
    expect(rendered.querySelector('#session-state-title')?.textContent).toBe('Sprawdzanie sesji na serwerze...');
    expect(rendered.querySelector('.unauthenticated-state')?.textContent).toContain('Trwa wczytywanie bieżącej sesji');
    expect(auth.isLoading()).toBeTrue();
    expect(auth.getLoginProviders).not.toHaveBeenCalled();

    auth.isLoading.set(false);
    auth.isLoggingOut.set(true);
    auth.authenticated.set(true);
    fixture.detectChanges();
    expect(rendered.querySelector('#session-state-title')?.textContent).toBe('Wylogowywanie...');
    expect(rendered.querySelector('.unauthenticated-state')?.textContent).toContain('Trwa zamykanie bieżącej sesji');
    expect(TestBed.inject(BffAuthViewService).statusMessage()).toBe('Trwa wylogowywanie...');

    locale.selectLocale('en');
    fixture.detectChanges();
    expect(rendered.querySelector('#session-state-title')?.textContent).toBe('Signing out...');
    expect(TestBed.inject(BffAuthViewService).statusMessage()).toBe('Logging out...');
    expect(auth.logout).not.toHaveBeenCalled();
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('opens the dialog without automatically selecting a provider and shows discovery loading inside it', async () => {
    await createComponent();

    signInButton().click();
    fixture.detectChanges();

    expect(dialog().open).toBeTrue();
    expect(dialog().textContent).toContain('Welcome back');
    expect(dialog().textContent).toContain('Choose a provider to continue');
    expect(dialog().textContent).toContain('Trying OpenID Connect?');
    expect(dialog().textContent).toContain('demo / demo');
    expect(dialog().textContent).toContain('Google, GitHub, and Facebook use your own provider account.');
    expect(dialog().textContent).toContain('Loading sign-in options...');
    expect(providerActions()).toHaveSize(0);
    expect(auth.login).not.toHaveBeenCalled();

    TestBed.inject(LocaleService).selectLocale('pl');
    fixture.detectChanges();
    expect(dialog().querySelector('[role="status"]')?.textContent).toContain('Wczytywanie opcji logowania...');
    expect(auth.getLoginProviders).toHaveBeenCalledTimes(1);
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('switches the open provider chooser EN to PL to EN without auth or navigation side effects', async () => {
    await createComponent();
    providerResponses.next([
      { id: 'Custom-ID', displayName: 'Custom SSO', isDefault: true, loginUrl: '/ignored/custom' },
    ]);
    fixture.detectChanges();
    signInButton().click();
    fixture.detectChanges();

    const locale = TestBed.inject(LocaleService);
    const initialUrl = window.location.href;
    expect(dialog().open).toBeTrue();
    expect(providerActions()[0].textContent?.trim()).toBe('Continue with Custom SSO');
    expect(dialog().querySelector('.dialog-close')?.getAttribute('aria-label')).toBe('Close sign-in dialog');

    locale.selectLocale('pl');
    fixture.detectChanges();
    expect(dialog().open).toBeTrue();
    expect(dialog().querySelector('#sign-in-dialog-title')?.textContent).toBe('Witaj ponownie');
    expect(dialog().querySelector('#sign-in-dialog-description')?.textContent).toBe('Wybierz dostawcę, aby kontynuować');
    expect(dialog().querySelector('.dialog-close')?.getAttribute('aria-label')).toBe('Zamknij okno logowania');
    expect(dialog().textContent).toContain('Użyj konta demonstracyjnego demo / demo');
    expect(providerActions()[0].textContent?.trim()).toBe('Kontynuuj przez Custom SSO');
    expect((fixture.nativeElement as HTMLElement).querySelector('.unauthenticated-state')?.textContent).toContain('Brak aktywnej sesji');
    expect(TestBed.inject(BffAuthViewService).loggedOutMessage()).toContain('Nie zalogowano.');
    expect(TestBed.inject(BffAuthViewService).statusMessage()).toBe('Nie zalogowano.');

    locale.selectLocale('en');
    fixture.detectChanges();
    expect(providerActions()[0].textContent?.trim()).toBe('Continue with Custom SSO');
    expect(dialog().open).toBeTrue();
    expect(auth.getLoginProviders).toHaveBeenCalledTimes(1);
    expect(auth.login).not.toHaveBeenCalled();
    expect(auth.logout).not.toHaveBeenCalled();
    expect(auth.currentUser()).toBeNull();
    expect(window.location.href).toBe(initialUrl);
  });

  it('closes with the close control, cancel, and backdrop while preserving inside clicks and restoring focus', async () => {
    await createComponent();

    signInButton().click();
    fixture.detectChanges();
    dialog().querySelector<HTMLButtonElement>('.dialog-close')?.click();
    await Promise.resolve();
    expect(dialog().open).toBeFalse();
    expect(document.activeElement).toBe(signInButton());

    signInButton().click();
    fixture.detectChanges();
    dialog().querySelector('.sign-in-dialog-content')?.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(dialog().open).toBeTrue();
    dialog().dispatchEvent(new Event('cancel', { cancelable: true }));
    expect(dialog().open).toBeFalse();

    signInButton().click();
    fixture.detectChanges();
    dialog().dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(dialog().open).toBeFalse();
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('renders discovered providers in backend order with known and generic icon variants', async () => {
    await createComponent();
    providerResponses.next([
      { id: 'google', displayName: 'Google', isDefault: false, loginUrl: '/ignored/google' },
      { id: 'facebook', displayName: 'Facebook', isDefault: false, loginUrl: '/ignored/facebook' },
      { id: 'github', displayName: 'GitHub', isDefault: false, loginUrl: '/ignored/github' },
      { id: 'oidc', displayName: 'OpenID Connect', isDefault: true, loginUrl: '/ignored/oidc' },
      { id: 'custom-sso', displayName: 'Custom SSO', isDefault: false, loginUrl: '/ignored/custom' },
    ]);
    fixture.detectChanges();

    expect(providerActions()).toHaveSize(0);
    signInButton().click();
    fixture.detectChanges();

    expect(providerActions().map((button) => button.textContent?.trim())).toEqual([
      'Continue with Google',
      'Continue with Facebook',
      'Continue with GitHub',
      'Continue with OpenID Connect',
      'Continue with Custom SSO',
    ]);
    expect(providerIconVariants()).toEqual(['google', 'facebook', 'github', 'oidc', 'generic']);
  });

  it('passes each original provider ID to login without using loginUrl or triggering duplicate discovery', async () => {
    await createComponent();
    providerResponses.next([
      { id: 'google', displayName: 'Google', isDefault: false, loginUrl: 'https://untrusted.example/login' },
      { id: 'Custom-ID', displayName: 'Custom SSO', isDefault: true, loginUrl: '/ignored/custom' },
    ]);
    fixture.detectChanges();
    fixture.detectChanges();
    signInButton().click();
    fixture.detectChanges();

    providerActions()[0].click();
    providerActions()[1].click();

    expect(auth.login).toHaveBeenCalledWith('google');
    expect(auth.login).toHaveBeenCalledWith('Custom-ID');
    expect(auth.getLoginProviders).toHaveBeenCalledTimes(1);
  });

  it('uses the parameterless fallback when discovery succeeds with an empty list', async () => {
    await createComponent();
    providerResponses.next([]);
    fixture.detectChanges();
    signInButton().click();
    fixture.detectChanges();

    expect(dialog().textContent).toContain('No additional providers are available.');
    TestBed.inject(LocaleService).selectLocale('pl');
    fixture.detectChanges();
    expect(dialog().textContent).toContain('Brak dodatkowych dostawców.');
    expect(fallbackAction().textContent).toContain('Kontynuuj przez domyślnego dostawcę');
    fallbackAction().click();

    expect(auth.login).toHaveBeenCalledTimes(1);
    expect(auth.login.calls.mostRecent().args).toEqual([]);
  });

  it('shows a safe parameterless fallback when provider discovery fails', async () => {
    await createComponent();
    providerResponses.error(new Error('Sensitive backend error details'));
    fixture.detectChanges();
    signInButton().click();
    fixture.detectChanges();

    expect(dialog().textContent).toContain("We couldn't load sign-in options.");
    expect(dialog().textContent).not.toContain('Sensitive backend error details');
    TestBed.inject(LocaleService).selectLocale('pl');
    fixture.detectChanges();
    expect(dialog().querySelector('[role="alert"]')?.textContent).toContain('Nie udało się wczytać opcji logowania.');
    expect(dialog().textContent).not.toContain('Sensitive backend error details');
    expect(auth.getLoginProviders).toHaveBeenCalledTimes(1);
    fallbackAction().click();

    expect(auth.login.calls.mostRecent().args).toEqual([]);
  });

  it('does not render sign-in or the provider dialog for authenticated users and keeps logout available', async () => {
    auth.authenticated.set(true);
    await createComponent();

    expect(signInButton()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('.unauthenticated-state')).toBeNull();
    expect(cardHeadings()).toEqual(['Login', 'User info']);
    expect((fixture.nativeElement as HTMLElement).querySelector('.sign-in-dialog')).toBeNull();
    expect(auth.getLoginProviders).not.toHaveBeenCalled();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button.button-secondary')?.click();
    expect(auth.logout).toHaveBeenCalledTimes(1);
  });

  it('renders authenticated session diagnostics, identity fields, avatar, and roles without provider discovery', async () => {
    setAuthenticatedUser({
      sub: 'subject-123',
      name: 'Test User',
      username: 'testuser',
      email: 'test@example.com',
      roles: ['Administrator', 'Reader'],
      externalIdentity: {
        providerId: 'google',
        emailVerified: true,
        pictureUrl: 'https://images.example.test/profile.png',
      },
    });
    await createComponent();

    const rendered = fixture.nativeElement as HTMLElement;
    expect(rendered.querySelector('.session-details')?.textContent).toContain('Active');
    expect(rendered.querySelector('.session-details')?.textContent).toContain('Backend-for-Frontend');
    expect(rendered.querySelector('.session-details')?.textContent).toContain('Google');
    expect(rendered.querySelector('.session-note')?.textContent)
      .toContain('provider tokens are not included in this current-user response');
    expect(rendered.querySelector('.identity-name')?.textContent).toContain('Test User');
    expect(rendered.querySelector('.provider-badge')?.textContent).toContain('Google');
    expect(rendered.querySelector('.user-details')?.textContent).toContain('subject-123');
    expect(rendered.querySelector('.user-details')?.textContent).toContain('testuser');
    expect(rendered.querySelector('.user-details')?.textContent).toContain('test@example.com');
    expect(rendered.querySelector('.status-badge')?.textContent).toContain('Verified');
    expect(roleLabels()).toEqual(['Administrator', 'Reader']);

    const avatar = rendered.querySelector<HTMLImageElement>('.identity-avatar');
    expect(avatar?.src).toBe('https://images.example.test/profile.png');
    expect(avatar?.getAttribute('alt')).toBe('');
    expect(avatar?.getAttribute('loading')).toBe('lazy');
    expect(avatar?.getAttribute('decoding')).toBe('async');
    expect(avatar?.getAttribute('referrerpolicy')).toBe('no-referrer');
    expect(auth.getLoginProviders).not.toHaveBeenCalled();
  });

  it('switches authenticated labels while preserving identity, roles, and session state', async () => {
    setAuthenticatedUser({
      roles: ['Reader'],
      externalIdentity: { providerId: 'Custom-ID', emailVerified: false, pictureUrl: null },
    });
    await createComponent();
    const user = auth.currentUser();
    const locale = TestBed.inject(LocaleService);
    const rendered = fixture.nativeElement as HTMLElement;

    locale.selectLocale('pl');
    fixture.detectChanges();
    expect(cardHeadings()).toEqual(['Logowanie', 'Dane użytkownika']);
    expect(rendered.querySelector('.session-details')?.textContent).toContain('Aktywna');
    expect(rendered.querySelector('.session-details')?.textContent).toContain('Custom-ID');
    expect(rendered.querySelector('.identity-name')?.textContent).toBe('Test User');
    expect(rendered.querySelector('.provider-badge')?.textContent).toBe('Custom-ID');
    expect(rendered.querySelector('.status-badge')?.textContent).toBe('Niezweryfikowany');
    expect(rendered.querySelector('.role-list')?.getAttribute('aria-label')).toBe('Role w aplikacji');
    expect(roleLabels()).toEqual(['Reader']);
    expect(auth.currentUser()).toBe(user);
    expect(auth.authenticated()).toBeTrue();
    expect(TestBed.inject(BffAuthViewService).statusMessage()).toBe('Zalogowano przy użyciu sesji serwera.');
    expect(auth.getLoginProviders).not.toHaveBeenCalled();
    expect(auth.login).not.toHaveBeenCalled();
    expect(auth.logout).not.toHaveBeenCalled();

    locale.selectLocale('en');
    fixture.detectChanges();
    expect(cardHeadings()).toEqual(['Login', 'User info']);
    expect(rendered.querySelector('.status-badge')?.textContent).toBe('Not verified');
    expect(auth.currentUser()).toBe(user);

    auth.currentUser.set({ ...user!, name: '', username: '', email: '', sub: '', roles: [], externalIdentity: null });
    locale.selectLocale('pl');
    fixture.detectChanges();
    expect(rendered.querySelector('.identity-name')?.textContent).toBe('Nieznany użytkownik');
    expect(rendered.querySelector('.provider-badge')?.textContent).toBe('Nie podano dostawcy');
    expect(rendered.querySelector('.user-details')?.textContent).toContain('Brak danych');
    expect(rendered.querySelector('.roles-detail')?.textContent).toContain('Brak ról w aplikacji');
  });

  it('preserves unknown providers and reports missing provider provenance', async () => {
    setAuthenticatedUser({ externalIdentity: { providerId: 'Custom-ID', emailVerified: null, pictureUrl: null } });
    await createComponent();

    const rendered = fixture.nativeElement as HTMLElement;
    expect(rendered.querySelector('.session-details')?.textContent).toContain('Custom-ID');
    expect(rendered.querySelector('.provider-badge')?.textContent).toContain('Custom-ID');

    auth.currentUser.set({ ...auth.currentUser()!, externalIdentity: null });
    fixture.detectChanges();
    expect(rendered.querySelector('.session-details')?.textContent).toContain('Not reported');
    expect(rendered.querySelector('.provider-badge')?.textContent).toContain('Provider not reported');
  });

  it('uses the local fallback avatar and distinguishes email verification and empty roles', async () => {
    setAuthenticatedUser({ externalIdentity: { providerId: 'github', emailVerified: false, pictureUrl: null }, roles: [] });
    await createComponent();

    const rendered = fixture.nativeElement as HTMLElement;
    expect(rendered.querySelector('img.identity-avatar')).toBeNull();
    expect(rendered.querySelector('.identity-avatar-fallback')).not.toBeNull();
    expect(rendered.querySelector('.status-badge')?.textContent).toContain('Not verified');
    expect(rendered.querySelector('.roles-detail')?.textContent).toContain('No application roles');

    auth.currentUser.set({ ...auth.currentUser()!, externalIdentity: { providerId: 'github', emailVerified: null, pictureUrl: null } });
    fixture.detectChanges();
    expect(rendered.querySelector('.status-badge')?.textContent).toContain('Not reported');

    auth.currentUser.set({ ...auth.currentUser()!, externalIdentity: { providerId: 'github', pictureUrl: null }, roles: undefined });
    fixture.detectChanges();
    expect(rendered.querySelector('.status-badge')?.textContent).toContain('Not reported');
    expect(rendered.querySelector('.roles-detail')?.textContent).toContain('No application roles');
  });

  it('shows exact current-user JSON only when authenticated and copies that JSON on request', async () => {
    setAuthenticatedUser({
      roles: ['Reader'],
      externalIdentity: { providerId: 'google', emailVerified: null, pictureUrl: null },
    });
    await createComponent();

    const rendered = fixture.nativeElement as HTMLElement;
    const inspector = rendered.querySelector<HTMLDetailsElement>('.current-user-inspector');
    const json = rendered.querySelector('.current-user-inspector pre code')?.textContent ?? '';
    expect(inspector).not.toBeNull();
    expect(inspector?.open).toBeFalse();
    expect(json).toBe(JSON.stringify(auth.currentUser(), null, 2));
    expect(json).toContain('"providerId": "google"');
    expect(json).not.toContain('"providerId": "Google"');
    expect(json).not.toContain('token');
    expect(json).not.toContain('cookie');

    rendered.querySelector<HTMLButtonElement>('.inspector-copy-button')?.click();
    await Promise.resolve();
    fixture.detectChanges();

    expect(clipboardWriter).toHaveBeenCalledWith(json);
    expect(rendered.querySelector('.copy-status')?.textContent).toContain('Copied');
    TestBed.inject(LocaleService).selectLocale('pl');
    fixture.detectChanges();
    expect(rendered.querySelector('.copy-status')?.textContent).toContain('Skopiowano');
    expect(rendered.querySelector('.inspector-copy-button')?.textContent).toContain('Kopiuj JSON');
    expect(clipboardWriter).toHaveBeenCalledTimes(1);
  });

  it('keeps the JSON selectable and reports when copying is unavailable', async () => {
    clipboardWriter.and.returnValue(Promise.reject(new Error('Clipboard unavailable')));
    setAuthenticatedUser();
    await createComponent();

    const rendered = fixture.nativeElement as HTMLElement;
    const json = rendered.querySelector('.current-user-inspector pre code')?.textContent ?? '';
    rendered.querySelector<HTMLButtonElement>('.inspector-copy-button')?.click();
    await Promise.resolve();
    fixture.detectChanges();

    expect(rendered.querySelector('.copy-status')?.textContent).toContain('Copy unavailable');
    expect(rendered.querySelector('.current-user-inspector pre code')?.textContent).toBe(json);
    TestBed.inject(LocaleService).selectLocale('pl');
    fixture.detectChanges();
    expect(rendered.querySelector('.copy-status')?.textContent).toContain('Kopiowanie niedostępne');
    expect(rendered.querySelector('.current-user-inspector pre code')?.textContent).toBe(json);
    expect(clipboardWriter).toHaveBeenCalledTimes(1);
  });

  async function createComponent(): Promise<void> {
    fixture = TestBed.createComponent(BffAuthViewComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function signInButton(): HTMLButtonElement {
    return (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button.unauthenticated-sign-in')!;
  }

  function cardHeadings(): string[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.authenticated-card h2')).map(
      (heading) => heading.textContent?.trim() ?? '',
    );
  }

  function dialog(): HTMLDialogElement {
    return (fixture.nativeElement as HTMLElement).querySelector<HTMLDialogElement>('.sign-in-dialog')!;
  }

  function providerActions(): HTMLButtonElement[] {
    return Array.from(dialog().querySelectorAll<HTMLButtonElement>('.provider-action:not(.fallback-action)'));
  }

  function providerIconVariants(): Array<string | null> {
    return Array.from(dialog().querySelectorAll<HTMLElement>('.provider-action .provider-icon')).map((icon) =>
      icon.getAttribute('data-provider-icon'),
    );
  }

  function fallbackAction(): HTMLButtonElement {
    return dialog().querySelector<HTMLButtonElement>('.provider-action')!;
  }

  function roleLabels(): string[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.role-list li')).map(
      (role) => role.textContent?.trim() ?? '',
    );
  }

  function setAuthenticatedUser(overrides: Partial<BffCurrentUser> = {}): void {
    auth.authenticated.set(true);
    auth.currentUser.set({
      isAuthenticated: true,
      sub: 'subject-123',
      name: 'Test User',
      username: 'testuser',
      email: 'test@example.com',
      roles: [],
      externalIdentity: null,
      ...overrides,
    });
  }
});
