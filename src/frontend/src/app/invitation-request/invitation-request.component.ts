import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { LocaleService } from '../locale.service';
import type { Translation } from '../locale.catalog';
import { InvitationRequestSecurityError, InvitationRequestService } from './invitation-request.service';

type FeedbackKey =
  | 'invitationRequestSuccess'
  | 'invitationRequestInvalid'
  | 'invitationRequestSecurity'
  | 'invitationRequestUnavailable'
  | 'invitationRequestRateLimited'
  | 'invitationRequestServiceError';

@Component({
  selector: 'app-invitation-request',
  templateUrl: './invitation-request.component.html',
  styleUrl: './invitation-request.component.css',
})
export class InvitationRequestComponent {
  private readonly requests = inject(InvitationRequestService);
  protected readonly locale = inject(LocaleService);
  protected readonly email = signal('');
  protected readonly submitting = signal(false);
  protected readonly invalid = signal(false);
  protected readonly feedback = signal<FeedbackKey | null>(null);
  protected readonly status = computed(() => {
    const key = this.feedback();
    return key ? this.locale.copy()[key as keyof Translation] : '';
  });
  protected readonly succeeded = computed(() => this.feedback() === 'invitationRequestSuccess');

  onEmailInput(event: Event): void {
    this.email.set((event.target as HTMLInputElement).value);
    this.invalid.set(false);
    this.feedback.set(null);
  }

  submit(event: Event): void {
    event.preventDefault();
    if (this.submitting()) {
      return;
    }

    const email = this.email().trim();
    if (email.length > 254 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.invalid.set(true);
      this.feedback.set('invitationRequestInvalid');
      return;
    }

    this.invalid.set(false);
    this.feedback.set(null);
    this.submitting.set(true);
    this.requests.submit(email).pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: () => {
        this.email.set('');
        this.feedback.set('invitationRequestSuccess');
      },
      error: (error: unknown) => this.feedback.set(this.errorKey(error)),
    });
  }

  private errorKey(error: unknown): FeedbackKey {
    if (error instanceof InvitationRequestSecurityError) {
      return 'invitationRequestSecurity';
    }
    if (error instanceof HttpErrorResponse) {
      switch (error.status) {
        case 400: return 'invitationRequestInvalid';
        case 401:
        case 403: return 'invitationRequestSecurity';
        case 404: return 'invitationRequestUnavailable';
        case 429: return 'invitationRequestRateLimited';
      }
    }
    return 'invitationRequestServiceError';
  }
}
