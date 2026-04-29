import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, EMPTY } from 'rxjs';
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
  readonly isLoading = signal(true);
  readonly authenticated = computed(() => this.user()?.isAuthenticated === true);
  readonly currentUser = this.user.asReadonly();
  readonly statusMessage = computed(() =>
    this.authenticated() ? 'Logged in with backend session.' : 'Not logged in yet.',
  );
  readonly loggedOutMessage = 'Not logged in yet. Use the login button to start the backend login flow.';

  constructor() {
    this.loadCurrentUser();
  }

  login(): void {
    window.location.href = `${this.authBaseUrl}/login`;
  }

  logout(): void {
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
        catchError(() => {
          this.user.set(null);
          return EMPTY;
        }),
      )
      .subscribe({
        next: (user) => this.user.set(user),
        complete: () => this.isLoading.set(false),
      });
  }

  private getAuthBaseUrl(): string {
    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');

    return `${apiOrigin}/api/auth`;
  }
}
