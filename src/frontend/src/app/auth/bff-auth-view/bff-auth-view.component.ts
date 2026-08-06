import { Component, ElementRef, inject, signal, ViewChild } from '@angular/core';
import { BffAuthViewService } from './bff-auth-view.service';

@Component({
  selector: 'app-bff-auth-view',
  templateUrl: './bff-auth-view.component.html',
  styleUrl: './bff-auth-view.component.css',
})
export class BffAuthViewComponent {
  protected readonly service = inject(BffAuthViewService);
  protected readonly isSignInDialogOpen = signal(false);

  @ViewChild('providerDialog')
  private providerDialog?: ElementRef<HTMLDialogElement>;

  @ViewChild('signInButton')
  private signInButton?: ElementRef<HTMLButtonElement>;

  private focusReturnTarget?: HTMLElement;

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
}
