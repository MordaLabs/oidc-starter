import { Component, ElementRef, inject, signal, ViewChild } from '@angular/core';
import type { BffCurrentUser } from '@flying-bee/oidc-starter-auth';
import { BFF_CLIPBOARD_WRITER } from './bff-clipboard-writer';
import { BffAuthViewService } from './bff-auth-view.service';

@Component({
  selector: 'app-bff-auth-view',
  templateUrl: './bff-auth-view.component.html',
  styleUrl: './bff-auth-view.component.css',
})
export class BffAuthViewComponent {
  protected readonly service = inject(BffAuthViewService);
  protected readonly isSignInDialogOpen = signal(false);
  protected readonly copyStatus = signal<string | null>(null);
  private readonly clipboardWriter = inject(BFF_CLIPBOARD_WRITER);

  @ViewChild('providerDialog')
  private providerDialog?: ElementRef<HTMLDialogElement>;

  @ViewChild('signInButton')
  private signInButton?: ElementRef<HTMLButtonElement>;

  private focusReturnTarget?: HTMLElement;
  private copyAttempt = 0;

  public isSessionLoading(): boolean {
    return this.service.isLoading();
  }

  public isSessionAuthenticated(): boolean {
    return this.service.authenticated();
  }

  public isSessionLoggingOut(): boolean {
    return this.service.isLoggingOut();
  }

  public openSignInDialog(focusReturnTarget?: HTMLElement): void {
    if (this.service.isLoading() || this.service.isLoggingOut() || this.service.authenticated()) {
      return;
    }

    const dialog = this.providerDialog?.nativeElement;
    if (dialog && !dialog.open) {
      this.focusReturnTarget = focusReturnTarget ?? this.signInButton?.nativeElement;
      dialog.showModal();
      this.isSignInDialogOpen.set(true);
    }
  }

  protected closeSignInDialog(): void {
    const dialog = this.providerDialog?.nativeElement;
    if (dialog?.open) {
      dialog.close();
      this.restoreSignInFocus();
    }
  }

  protected closeDialogOnCancel(event: Event): void {
    event.preventDefault();
    this.closeSignInDialog();
  }

  protected closeDialogOnBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.closeSignInDialog();
    }
  }

  protected restoreSignInFocus(): void {
    this.isSignInDialogOpen.set(false);
    const focusReturnTarget = this.focusReturnTarget ?? this.signInButton?.nativeElement;
    this.focusReturnTarget = undefined;
    focusReturnTarget?.focus();
  }

  protected providerIcon(providerId: string): 'google' | 'facebook' | 'github' | 'oidc' | 'generic' {
    switch (providerId.trim().toLowerCase()) {
      case 'google':
        return 'google';
      case 'facebook':
        return 'facebook';
      case 'github':
        return 'github';
      case 'oidc':
        return 'oidc';
      default:
        return 'generic';
    }
  }

  protected providerDisplayName(providerId: string | null | undefined, missingLabel = 'Not reported'): string {
    if (!providerId?.trim()) {
      return missingLabel;
    }

    switch (providerId.trim().toLowerCase()) {
      case 'google':
        return 'Google';
      case 'facebook':
        return 'Facebook';
      case 'github':
        return 'GitHub';
      case 'oidc':
        return 'OpenID Connect';
      default:
        return providerId;
    }
  }

  protected emailVerificationStatus(emailVerified: boolean | null | undefined): string {
    if (emailVerified === true) {
      return 'Verified';
    }

    if (emailVerified === false) {
      return 'Not verified';
    }

    return 'Not reported';
  }

  protected pictureUrl(pictureUrl: string | null | undefined): string | null {
    const normalizedPictureUrl = pictureUrl?.trim();

    return normalizedPictureUrl || null;
  }

  protected currentUserJson(user: BffCurrentUser): string {
    return JSON.stringify(user, null, 2);
  }

  protected copyCurrentUserJson(user: BffCurrentUser): void {
    const attempt = ++this.copyAttempt;
    const json = this.currentUserJson(user);
    this.copyStatus.set(null);

    void this.clipboardWriter(json).then(
      () => {
        if (attempt === this.copyAttempt) {
          this.copyStatus.set('Copied');
        }
      },
      () => {
        if (attempt === this.copyAttempt) {
          this.copyStatus.set('Copy unavailable');
        }
      },
    );
  }
}
