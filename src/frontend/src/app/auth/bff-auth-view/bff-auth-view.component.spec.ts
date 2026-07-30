import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal, WritableSignal } from '@angular/core';
import { Subject } from 'rxjs';
import { BffAuthService, type BffLoginProvider } from '@flying-bee/oidc-starter-auth';
import { BffAuthViewComponent } from './bff-auth-view.component';

type BffAuthServiceStub = {
  isLoading: WritableSignal<boolean>;
  isLoggingOut: WritableSignal<boolean>;
  authenticated: WritableSignal<boolean>;
  currentUser: WritableSignal<null>;
  getLoginProviders: jasmine.Spy;
  login: jasmine.Spy;
  logout: jasmine.Spy;
};

describe('BffAuthViewComponent', () => {
  let fixture: ComponentFixture<BffAuthViewComponent>;
  let auth: BffAuthServiceStub;
  let providerResponses: Subject<readonly BffLoginProvider[]>;

  beforeEach(async () => {
    providerResponses = new Subject<readonly BffLoginProvider[]>();
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
      providers: [{ provide: BffAuthService, useValue: auth }],
    }).compileComponents();
  });

  afterEach(() => TestBed.resetTestingModule());

  it('discovers and renders backend-ordered providers once for an unauthenticated user', async () => {
    await createComponent();

    expect(auth.getLoginProviders).toHaveBeenCalledTimes(1);
    expect(loginActions()).toHaveSize(0);
    expect((fixture.nativeElement as HTMLElement).querySelector('button:disabled')?.textContent).toContain(
      'Loading login options...',
    );

    providerResponses.next([
      { id: 'oidc', displayName: 'OpenID Connect', isDefault: true, loginUrl: '/ignored/oidc' },
      { id: 'google', displayName: 'Google', isDefault: false, loginUrl: '/ignored/google' },
    ]);
    fixture.detectChanges();

    expect(loginActions().map((button) => button.textContent?.trim())).toEqual([
      'Continue with OpenID Connect',
      'Continue with Google',
    ]);
    fixture.detectChanges();
    expect(auth.getLoginProviders).toHaveBeenCalledTimes(1);
  });

  it('disables login actions while the initial backend session check is loading', async () => {
    auth.isLoading.set(true);
    await createComponent();

    const rendered = fixture.nativeElement as HTMLElement;
    const loadingButton = rendered.querySelector<HTMLButtonElement>('button:disabled');
    loadingButton?.click();

    expect(auth.getLoginProviders).not.toHaveBeenCalled();
    expect(loadingButton?.textContent).toContain('Loading login options...');
    expect(loadingButton?.disabled).toBeTrue();
    expect(rendered.querySelector('.legacy-login')).toBeNull();
    expect(loginActions()).toHaveSize(0);
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('selects every discovered provider through its ID without using loginUrl', async () => {
    await createComponent();
    providerResponses.next([
      { id: 'google', displayName: 'Google', isDefault: false, loginUrl: 'https://untrusted.example/login' },
      { id: 'custom', displayName: 'Custom SSO', isDefault: true, loginUrl: '/ignored/custom' },
    ]);
    fixture.detectChanges();

    loginActions()[0].click();
    loginActions()[1].click();

    expect(auth.login).toHaveBeenCalledWith('google');
    expect(auth.login).toHaveBeenCalledWith('custom');
  });

  it('uses parameterless login when provider discovery returns an empty list', async () => {
    await createComponent();
    providerResponses.next([]);
    fixture.detectChanges();

    legacyLoginButton().click();

    expect(auth.login).toHaveBeenCalledTimes(1);
    expect(auth.login.calls.mostRecent().args).toEqual([]);
  });

  it('shows a safe legacy fallback when provider discovery fails', async () => {
    await createComponent();
    providerResponses.error(new Error('Sensitive backend error details'));
    fixture.detectChanges();

    const rendered = fixture.nativeElement as HTMLElement;
    expect(rendered.textContent).toContain('Login options could not be loaded.');
    expect(rendered.textContent).not.toContain('Sensitive backend error details');

    legacyLoginButton().click();
    expect(auth.login.calls.mostRecent().args).toEqual([]);
  });

  it('does not discover providers or render login actions for an authenticated user', async () => {
    auth.authenticated.set(true);
    await createComponent();

    expect(auth.getLoginProviders).not.toHaveBeenCalled();
    expect(loginActions()).toHaveSize(0);

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button.button-secondary')?.click();
    expect(auth.logout).toHaveBeenCalledTimes(1);
  });

  async function createComponent(): Promise<void> {
    fixture = TestBed.createComponent(BffAuthViewComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function loginActions(): HTMLButtonElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.provider-actions button'));
  }

  function legacyLoginButton(): HTMLButtonElement {
    return (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.legacy-login')!;
  }
});
