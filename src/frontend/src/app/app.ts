import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { BffAuthViewComponent } from './auth/bff-auth-view/bff-auth-view.component';
import { SpaAuthViewComponent } from './auth/spa-auth-view/spa-auth-view.component';
import { environment } from '../environments/environment';

type PingResponse = {
  status: string;
  applicationName: string;
  timestampUtc: string;
  oidcConfigured: boolean;
};

@Component({
  selector: 'app-root',
  imports: [SpaAuthViewComponent, BffAuthViewComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly http = inject(HttpClient);

  protected readonly appName = environment.appName;
  protected readonly authMode = environment.authMode;
  protected readonly pingResult = signal<PingResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  constructor() {
    this.loadPing();
  }

  private loadPing(): void {
    const apiOrigin = environment.apiOrigin.replace(/\/$/, '');

    this.http
      .get<PingResponse>(`${apiOrigin}/api/public/ping`)
      .subscribe({
        next: (response) => this.pingResult.set(response),
        error: () => this.errorMessage.set('Backend API not reachable yet.'),
      });
  }
}
