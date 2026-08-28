import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { BffAuthService, type BffLoginProvider } from '@mordalabs/oidc-starter-auth';

@Injectable({ providedIn: 'root' })
export class BffAuthViewService {
  private readonly auth = inject(BffAuthService);

  readonly mode = 'bff';
  readonly isLoading = this.auth.isLoading;
  readonly isLoggingOut = this.auth.isLoggingOut;
  readonly authenticated = this.auth.authenticated;
  readonly currentUser = this.auth.currentUser;
  readonly loginProviders = signal<readonly BffLoginProvider[] | null>(null);
  readonly isDiscoveringLoginProviders = signal(false);
  readonly providerDiscoveryError = signal(false);
  private readonly providerDiscoveryRequested = signal(false);
  readonly statusMessage = computed(() =>
    this.isLoggingOut()
      ? 'Logging out...'
      : this.authenticated()
        ? 'Logged in with backend session.'
        : 'Not logged in yet.',
  );
  readonly loggedOutMessage = 'Not logged in yet. Use the login button to start the backend login flow.';

  constructor() {
    effect(() => {
      if (!this.isLoading() && !this.authenticated() && !this.providerDiscoveryRequested()) {
        this.loadLoginProviders();
      }
    });
  }

  login(providerId?: string): void {
    if (providerId === undefined) {
      this.auth.login();
      return;
    }

    this.auth.login(providerId);
  }

  logout(): void {
    this.auth.logout();
  }

  private loadLoginProviders(): void {
    this.providerDiscoveryRequested.set(true);
    this.isDiscoveringLoginProviders.set(true);

    this.auth.getLoginProviders().subscribe({
      next: (providers) => {
        this.loginProviders.set(providers);
        this.isDiscoveringLoginProviders.set(false);
      },
      error: () => {
        this.providerDiscoveryError.set(true);
        this.isDiscoveringLoginProviders.set(false);
      },
    });
  }
}
