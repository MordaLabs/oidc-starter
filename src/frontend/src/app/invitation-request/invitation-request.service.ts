import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpXsrfTokenExtractor } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { of, switchMap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

export class InvitationRequestSecurityError extends Error {}

@Injectable({ providedIn: 'root' })
export class InvitationRequestService {
  private readonly http = inject(HttpClient);
  private readonly xsrf = inject(HttpXsrfTokenExtractor);
  private readonly document = inject(DOCUMENT);

  submit(email: string) {
    if (!this.isSameOrigin()) {
      return throwError(() => new InvitationRequestSecurityError());
    }

    return this.http.get('/api/auth/csrf', {
      responseType: 'text',
      withCredentials: true,
    }).pipe(
      switchMap(() => {
        const token = this.xsrf.getToken();
        if (!token) {
          return throwError(() => new InvitationRequestSecurityError());
        }

        return this.http.post('/api/demo/invitation-requests', { email }, {
          headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token },
          observe: 'response',
          responseType: 'text',
          withCredentials: true,
        }).pipe(switchMap((response) => response.status === 202
          ? of(undefined)
          : throwError(() => new Error())));
      }),
    );
  }

  private isSameOrigin(): boolean {
    if (!environment.apiOrigin) {
      return true;
    }

    try {
      const configured = new URL(environment.apiOrigin);
      return configured.origin === this.document.location?.origin &&
        configured.pathname === '/' && !configured.search && !configured.hash &&
        !configured.username && !configured.password;
    } catch {
      return false;
    }
  }
}
