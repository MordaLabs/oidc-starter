import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { catchError, EMPTY, finalize, of } from 'rxjs';
import { BFF_AUTH_CONFIG } from './internal/bff-auth-token';
import type { BffCurrentUser } from './bff-current-user';

@Injectable({ providedIn: 'root' })
export class BffAuthService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(BFF_AUTH_CONFIG);
  private readonly authBaseUrl = this.getAuthBaseUrl();
  private readonly antiforgeryCookieName = this.config.antiforgeryCookieName ?? 'XSRF-TOKEN';
  private readonly antiforgeryFormFieldName =
    this.config.antiforgeryFormFieldName ?? '__RequestVerificationToken';
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

    this.http
      .get(`${this.authBaseUrl}/csrf`, {
        observe: 'response',
        responseType: 'text',
        withCredentials: true,
      })
      .pipe(
        catchError((error: unknown) => {
          if (error instanceof HttpErrorResponse && error.status === 404) {
            return of(null);
          }

          this.isLoggingOut.set(false);
          return EMPTY;
        }),
      )
      .subscribe({
        next: () => this.submitLogoutForm(this.getCookieValue(this.antiforgeryCookieName)),
      });
  }

  refreshCurrentUser(): void {
    this.loadCurrentUser();
  }

  private submitLogoutForm(antiforgeryToken: string | null): void {
    const form = document.createElement('form');
    form.method = 'post';
    form.action = `${this.authBaseUrl}/logout`;
    form.style.display = 'none';

    if (antiforgeryToken) {
      const tokenInput = document.createElement('input');
      tokenInput.type = 'hidden';
      tokenInput.name = this.antiforgeryFormFieldName;
      tokenInput.value = antiforgeryToken;
      form.appendChild(tokenInput);
    }

    document.body.appendChild(form);
    form.submit();
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
    const normalizedAuthPath = authPath.replace(/^\/+|\/+$/g, '');

    return `${apiOrigin}${normalizedAuthPath ? `/${normalizedAuthPath}` : ''}`;
  }

  private getCookieValue(name: string): string | null {
    const encodedName = `${encodeURIComponent(name)}=`;
    const cookie = document.cookie
      .split(';')
      .map((part) => part.trim())
      .find((part) => part.startsWith(encodedName));

    return cookie ? decodeURIComponent(cookie.slice(encodedName.length)) : null;
  }
}
