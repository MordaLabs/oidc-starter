import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, OnDestroy, signal } from '@angular/core';
import { BffAuthService } from '@mordalabs/oidc-starter-auth';
import { Subscription } from 'rxjs';
import { LocaleService } from '../locale.service';
import {
  isOperatorGuid, OperatorAction, OperatorDashboardService, OperatorRequestRecord,
  OperatorRequestSecurityError, OperatorStatus,
} from './operator-dashboard.service';

type Feedback = 'denied' | 'notFound' | 'conflict' | 'unavailable' | 'invalid'
  | 'approved' | 'rejected' | 'revoked';
type Confirmation = { action: OperatorAction; record: OperatorRequestRecord; id: string };

@Component({
  selector: 'app-operator-dashboard',
  templateUrl: './operator-dashboard.component.html',
  styleUrl: './operator-dashboard.component.css',
})
export class OperatorDashboardComponent implements OnDestroy {
  private readonly auth = inject(BffAuthService);
  private readonly requests = inject(OperatorDashboardService);
  protected readonly locale = inject(LocaleService);
  protected readonly authenticated = this.auth.authenticated;
  protected readonly loggingOut = this.auth.isLoggingOut;
  protected readonly open = signal(false);
  protected readonly status = signal<OperatorStatus>('pending');
  protected readonly records = signal<OperatorRequestRecord[] | null>(null);
  protected readonly loading = signal(false);
  protected readonly mutating = signal(false);
  protected readonly confirmation = signal<Confirmation | null>(null);
  protected readonly feedback = signal<Feedback | null>(null);
  private listSubscription?: Subscription;
  private mutationSubscription?: Subscription;
  private generation = 0;

  constructor() {
    effect(() => {
      if (!this.authenticated() || this.loggingOut()) {
        this.close();
      }
    });
  }

  ngOnDestroy(): void {
    this.close();
  }

  protected openDashboard(): void {
    if (!this.authenticated() || this.loggingOut() || this.open()) {
      return;
    }
    this.open.set(true);
    this.status.set('pending');
    this.feedback.set(null);
    this.load();
  }

  protected close(): void {
    this.generation++;
    this.listSubscription?.unsubscribe();
    this.mutationSubscription?.unsubscribe();
    this.open.set(false);
    this.records.set(null);
    this.confirmation.set(null);
    this.loading.set(false);
    this.mutating.set(false);
    this.feedback.set(null);
  }

  protected selectStatus(event: Event): void {
    if (this.mutating() || !this.open()) {
      return;
    }
    const value = (event.target as HTMLSelectElement).value;
    if (value !== 'pending' && value !== 'approved' && value !== 'rejected') {
      return;
    }
    this.status.set(value);
    this.feedback.set(null);
    this.load();
  }

  protected refresh(): void {
    if (!this.mutating() && this.open()) {
      this.feedback.set(null);
      this.load();
    }
  }

  protected canAct(action: OperatorAction, record: OperatorRequestRecord): boolean {
    return this.open() && this.authenticated() && !this.loggingOut() &&
      !this.loading() && !this.mutating() && this.records()?.includes(record) === true &&
      this.canShowAction(action, record);
  }

  protected canShowAction(action: OperatorAction, record: OperatorRequestRecord): boolean {
    return action === 'revoke'
      ? record.status === 'approved' && isOperatorGuid(record.invitationId)
      : record.status === 'pending' && isOperatorGuid(record.id);
  }

  protected requestConfirmation(action: OperatorAction, record: OperatorRequestRecord): void {
    if (!this.canAct(action, record) || this.confirmation()) {
      return;
    }
    const id = action === 'revoke' ? record.invitationId : record.id;
    if (id === null) {
      return;
    }
    this.confirmation.set({ action, record, id });
    this.feedback.set(null);
  }

  protected cancelConfirmation(): void {
    if (!this.mutating()) {
      this.confirmation.set(null);
    }
  }

  protected confirm(): void {
    const selected = this.confirmation();
    if (!selected || this.mutating() || !this.canAct(selected.action, selected.record) ||
        selected.id !== (selected.action === 'revoke' ? selected.record.invitationId : selected.record.id)) {
      return;
    }

    this.mutating.set(true);
    const generation = ++this.generation;
    this.mutationSubscription = this.requests.mutate(selected.action, selected.id).subscribe({
      next: () => {
        if (!this.isCurrent(generation)) return;
        this.mutating.set(false);
        this.confirmation.set(null);
        this.feedback.set(selected.action === 'approve' ? 'approved'
          : selected.action === 'reject' ? 'rejected' : 'revoked');
        this.load();
      },
      error: (error: unknown) => {
        if (!this.isCurrent(generation)) return;
        this.mutating.set(false);
        this.confirmation.set(null);
        this.records.set(null);
        this.feedback.set(this.errorFeedback(error));
      },
    });
  }

  protected formatTimestamp(value: string | null): string {
    if (!value) return this.locale.copy().operatorNotAvailable;
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return this.locale.copy().operatorNotAvailable;
    return `${new Intl.DateTimeFormat(this.locale.locale() === 'pl' ? 'pl-PL' : 'en-GB', {
      dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC',
    }).format(date)} UTC`;
  }

  protected feedbackText(): string {
    switch (this.feedback()) {
      case 'denied': return this.locale.copy().operatorDenied;
      case 'notFound': return this.locale.copy().operatorNotFound;
      case 'conflict': return this.locale.copy().operatorConflict;
      case 'invalid': return this.locale.copy().operatorInvalid;
      case 'unavailable': return this.locale.copy().operatorUnavailable;
      case 'approved': return this.locale.copy().operatorApprovedSuccess;
      case 'rejected': return this.locale.copy().operatorRejectedSuccess;
      case 'revoked': return this.locale.copy().operatorRevokedSuccess;
      default: return '';
    }
  }

  private load(): void {
    if (!this.open() || !this.authenticated() || this.loggingOut()) return;
    this.listSubscription?.unsubscribe();
    const generation = ++this.generation;
    this.records.set(null);
    this.confirmation.set(null);
    this.loading.set(true);
    this.listSubscription = this.requests.list(this.status()).subscribe({
      next: (records) => {
        if (!this.isCurrent(generation)) return;
        this.loading.set(false);
        if (!Array.isArray(records)) {
          this.feedback.set('unavailable');
          return;
        }
        this.records.set(records.slice(0, 50));
      },
      error: (error: unknown) => {
        if (!this.isCurrent(generation)) return;
        this.loading.set(false);
        this.records.set(null);
        this.confirmation.set(null);
        this.feedback.set(this.errorFeedback(error));
      },
    });
  }

  private isCurrent(generation: number): boolean {
    return generation === this.generation && this.open() &&
      this.authenticated() && !this.loggingOut();
  }

  private errorFeedback(error: unknown): Feedback {
    if (error instanceof OperatorRequestSecurityError) return 'denied';
    if (error instanceof HttpErrorResponse) {
      switch (error.status) {
        case 400: return 'invalid';
        case 401:
        case 403: return 'denied';
        case 404: return 'notFound';
        case 409: return 'conflict';
      }
    }
    return 'unavailable';
  }
}
