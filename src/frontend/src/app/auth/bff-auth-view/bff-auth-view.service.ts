import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { catchError, EMPTY, finalize } from 'rxjs';
import { environment } from '../../../environments/environment';

type BffUser = {
  isAuthenticated: boolean;
  sub?: string | null;
  name?: string | null;
  username?: string | null;
  email?: string | null;
};

@Injectable({ providedIn: 'root' })
export class BffAuthViewService {
  private readonly http = inject(HttpClient);
  private readonly authBaseUrl = this.getAuthBaseUrl();
  private readonly user = signal<BffUser | null>(null);

  readonly mode = 'bff';
  readonly isLoading = signal(false);
  readonly isLoggingOut = signal(false);
  readonly authenticated = computed(() => this.user()?.isAuthenticated === true);
  readonly currentUser = this.user.asReadonly();
  readonly statusMessage = computed(() =>
    this.isLoggingOut()
      ? 'Logging out...'
      : this.authenticated()
        ? 'Logged in with backend session.'
        : 'Not logged in yet.',
  );
  readonly loggedOutMessage = 'Not logged in yet. Use the login button to start the backend login flow.';

  constructor() {
    this.loadCurrentUser();
  }

  login(): void {
    this.isLoading.set(true);
    window.location.href = `${this.authBaseUrl}/login`;
  }

  logout(): void {
    this.clearSessionState();
    this.isLoggingOut.set(true);

    const form = document.createElement('form');
    form.method = 'post';
    form.action = `${this.authBaseUrl}/logout`;
    form.style.display = 'none';
    document.body.appendChild(form);
    form.submit();
  }

  private loadCurrentUser(): void {
    this.isLoading.set(true);

    this.http
      .get<BffUser>(`${this.authBaseUrl}/me`, { withCredentials: true })
      .pipe(
        catchError((error: unknown) => {
          this.handleCurrentUserError(error);
          return EMPTY;
        }),
        finalize(() => this.isLoading.set(false)),
      )
      .subscribe({
        next: (user) => this.setCurrentUser(user),
      });
  }

  private setCurrentUser(user: BffUser): void {
    this.isLoggingOut.set(false);
    this.user.set(user.isAuthenticated ? user : null);
  }

  private handleCurrentUserError(error: unknown): void {
    if (error instanceof HttpErrorResponse && error.status === 401) {
      this.clearSessionState();
      return;
    }

    this.clearSessionState();
  }

  private clearSessionState(): void {
    this.user.set(null);
  }

  private getAuthBaseUrl(): string {
    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');

    return `${apiOrigin}/api/auth`;
  }
}
