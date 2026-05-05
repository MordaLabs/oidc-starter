import { computed, inject, Injectable } from '@angular/core';
import { BffAuthService } from '@jszyduk/oidc-starter-auth';

@Injectable({ providedIn: 'root' })
export class BffAuthViewService {
  private readonly auth = inject(BffAuthService);

  readonly mode = 'bff';
  readonly isLoading = this.auth.isLoading;
  readonly isLoggingOut = this.auth.isLoggingOut;
  readonly authenticated = this.auth.authenticated;
  readonly currentUser = this.auth.currentUser;
  readonly statusMessage = computed(() =>
    this.isLoggingOut()
      ? 'Logging out...'
      : this.authenticated()
        ? 'Logged in with backend session.'
        : 'Not logged in yet.',
  );
  readonly loggedOutMessage = 'Not logged in yet. Use the login button to start the backend login flow.';

  login(): void {
    this.auth.login();
  }

  logout(): void {
    this.auth.logout();
  }
}
