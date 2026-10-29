import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { BffAuthService, type BffLoginProvider } from '@mordalabs/oidc-starter-auth';
import { LocaleService } from '../../locale.service';

@Injectable({ providedIn: 'root' })
export class BffAuthViewService {
  private readonly auth = inject(BffAuthService);
  private readonly localeService = inject(LocaleService);

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
      ? this.localeService.copy().bffLoggingOutStatus
      : this.authenticated()
        ? this.localeService.copy().bffLoggedInStatus
        : this.localeService.copy().bffNotLoggedInStatus,
  );
  readonly loggedOutMessage = computed(() => this.localeService.copy().bffLoggedOutMessage);

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
