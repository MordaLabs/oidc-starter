import { HttpClient } from '@angular/common/http';
import { Component, inject, signal, viewChild } from '@angular/core';
import { BffAuthViewComponent } from './auth/bff-auth-view/bff-auth-view.component';
import { SpaAuthViewComponent } from './auth/spa-auth-view/spa-auth-view.component';
import { environment } from '../environments/environment';
import { LocaleService } from './locale.service';
import { InvitationRequestComponent } from './invitation-request/invitation-request.component';
import { OperatorDashboardComponent } from './operator-dashboard/operator-dashboard.component';

type PingResponse = {
  status: string;
  applicationName: string;
  timestampUtc: string;
  oidcConfigured: boolean;
};

@Component({
  selector: 'app-root',
  imports: [SpaAuthViewComponent, BffAuthViewComponent, InvitationRequestComponent, OperatorDashboardComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly http = inject(HttpClient);
  protected readonly localeService = inject(LocaleService);
  private readonly bffAuthView = viewChild(BffAuthViewComponent);

  protected readonly authMode = environment.authMode;
  protected readonly invitationRequestUiEnabled = environment.invitationRequestUiEnabled;
  protected readonly operatorDashboardUiEnabled = environment.operatorDashboardUiEnabled;
  protected readonly pingResult = signal<PingResponse | null>(null);
  protected readonly backendUnreachable = signal(false);
  protected readonly builtInProviders = [
    { id: 'oidc', displayName: 'OpenID Connect' },
    { id: 'google', displayName: 'Google' },
    { id: 'github', displayName: 'GitHub' },
    { id: 'facebook', displayName: 'Facebook' },
  ] as const;

  protected premiumPageUrl(page: 'overview' | 'sessions'): string {
    const base = this.localeService.locale() === 'pl' ? '/premium/pl/' : '/premium/';
    return page === 'sessions' ? `${base}sessions/` : base;
  }

  constructor() {
    this.loadPing();
  }

  protected hasProviderDiscoveryResult(): boolean {
    return this.bffAuthView()?.hasDiscoveredLoginProviders() ?? false;
  }

  protected isBuiltInProviderConfigured(providerId: string): boolean {
    return this.bffAuthView()?.isLoginProviderConfigured(providerId) ?? false;
  }

  protected formatUtcTimestamp(timestamp: string | null | undefined): string {
    const date = new Date(timestamp ?? '');

    if (Number.isNaN(date.getTime())) {
      return this.localeService.copy().runtimeNotAvailable;
    }

    if (this.localeService.locale() === 'pl') {
      return `${new Intl.DateTimeFormat('pl-PL', {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        hourCycle: 'h23',
        timeZone: 'UTC',
      }).format(date)} UTC`;
    }

    const month = new Intl.DateTimeFormat('en-GB', {
      month: 'short',
      timeZone: 'UTC',
    }).format(date);
    const hours = date.getUTCHours().toString().padStart(2, '0');
    const minutes = date.getUTCMinutes().toString().padStart(2, '0');

    return `${date.getUTCDate()} ${month} ${date.getUTCFullYear()}, ${hours}:${minutes} UTC`;
  }

  private loadPing(): void {
    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');

    this.http
      .get<PingResponse>(`${apiOrigin}/api/public/ping`)
      .subscribe({
        next: (response) => this.pingResult.set(response),
        error: () => this.backendUnreachable.set(true),
      });
  }
}
