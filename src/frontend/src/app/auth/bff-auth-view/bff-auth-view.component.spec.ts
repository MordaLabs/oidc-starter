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

  it('renders one sign-in action after the session check and keeps provider actions out of the card', async () => {
    await createComponent();

    const rendered = fixture.nativeElement as HTMLElement;

    expect(signInButton()).not.toBeNull();
    expect(rendered.querySelectorAll('button.sign-in-button')).toHaveSize(1);
    expect(dialog().open).toBeFalse();
    expect(providerActions()).toHaveSize(0);
    expect(rendered.querySelector('.card .provider-action')).toBeNull();
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('keeps sign-in disabled during the initial backend session check without discovering or invoking login', async () => {
    auth.isLoading.set(true);
    await createComponent();

    const loadingButton = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button:disabled');
    loadingButton?.click();

    expect(loadingButton?.textContent).toContain('Loading login options...');
    expect(loadingButton?.disabled).toBeTrue();
    expect(signInButton()).toBeNull();
    expect(dialog().open).toBeFalse();
    expect(auth.getLoginProviders).not.toHaveBeenCalled();
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('opens the dialog without automatically selecting a provider and shows discovery loading inside it', async () => {
    await createComponent();

    signInButton().click();
    fixture.detectChanges();

    expect(dialog().open).toBeTrue();
    expect(dialog().textContent).toContain('Welcome back');
    expect(dialog().textContent).toContain('Choose a provider to continue');
    expect(dialog().textContent).toContain('Loading sign-in options...');
    expect(providerActions()).toHaveSize(0);
    expect(auth.login).not.toHaveBeenCalled();
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
    fallbackAction().click();

    expect(auth.login.calls.mostRecent().args).toEqual([]);
  });

  it('does not render sign-in or the provider dialog for authenticated users and keeps logout available', async () => {
    auth.authenticated.set(true);
    await createComponent();

    expect(signInButton()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('.sign-in-dialog')).toBeNull();
    expect(auth.getLoginProviders).not.toHaveBeenCalled();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button.button-secondary')?.click();
    expect(auth.logout).toHaveBeenCalledTimes(1);
  });

  async function createComponent(): Promise<void> {
    fixture = TestBed.createComponent(BffAuthViewComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function signInButton(): HTMLButtonElement {
    return (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button.sign-in-button')!;
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
});
