import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { catchError, EMPTY, finalize } from 'rxjs';
import { BFF_AUTH_CONFIG } from './internal/bff-auth-token';
import type { BffCurrentUser } from './bff-current-user';

@Injectable({ providedIn: 'root' })
export class BffAuthService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(BFF_AUTH_CONFIG);
  private readonly authBaseUrl = this.getAuthBaseUrl();
  private readonly user = signal<BffCurrentUser | null>(null);

  readonly isLoading = signal(false);
  readonly isLoggingOut = signal(false);
  readonly authenticated = computed(() => this.user()?.isAuthenticated === true);
  readonly currentUser = this.user.asReadonly();

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

  refreshCurrentUser(): void {
    this.loadCurrentUser();
  }

  private loadCurrentUser(): void {
    this.isLoading.set(true);

    this.http
      .get<BffCurrentUser>(`${this.authBaseUrl}/me`, { withCredentials: true })
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

  private setCurrentUser(user: BffCurrentUser): void {
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
    const apiOrigin = this.config.apiOrigin?.replace(/\/$/, '') ?? '';
    const authPath = this.config.authPath ?? '/api/auth';

    return `${apiOrigin}${authPath.startsWith('/') ? authPath : `/${authPath}`}`;
  }
}
