import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpXsrfTokenExtractor } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { of, switchMap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

export type OperatorStatus = 'pending' | 'approved' | 'rejected';
export type OperatorAction = 'approve' | 'reject' | 'revoke';

export interface OperatorRequestRecord {
  id: string;
  email: string;
  createdAt: string;
  status: OperatorStatus;
  decidedAt: string | null;
  invitationId: string | null;
}

export class OperatorRequestSecurityError extends Error {}

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isOperatorGuid(value: string | null): value is string {
  return value !== null && guidPattern.test(value) &&
    value.toLowerCase() !== '00000000-0000-0000-0000-000000000000';
}

@Injectable({ providedIn: 'root' })
export class OperatorDashboardService {
  private readonly http = inject(HttpClient);
  private readonly xsrf = inject(HttpXsrfTokenExtractor);
  private readonly document = inject(DOCUMENT);

  list(status: OperatorStatus) {
    if (!this.isSameOrigin()) {
      return throwError(() => new OperatorRequestSecurityError());
    }
    return this.http.get<OperatorRequestRecord[]>(
      `/api/demo/operator/invitation-requests?status=${status}`,
      { withCredentials: true },
    );
  }

  mutate(action: OperatorAction, id: string) {
    if (!this.isSameOrigin() || !isOperatorGuid(id)) {
      return throwError(() => new OperatorRequestSecurityError());
    }

    const url = action === 'revoke'
      ? `/api/demo/operator/invitations/${id}/revoke`
      : `/api/demo/operator/invitation-requests/${id}/${action}`;
    return this.http.get('/api/auth/csrf', {
      responseType: 'text',
      withCredentials: true,
    }).pipe(
      switchMap(() => {
        const token = this.xsrf.getToken();
        if (!token) {
          return throwError(() => new OperatorRequestSecurityError());
        }
        return this.http.post(url, null, {
          headers: { 'X-XSRF-TOKEN': token },
          observe: 'response',
          responseType: 'text',
          withCredentials: true,
        }).pipe(switchMap((response) => response.status === 204
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
