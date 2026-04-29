import { computed, inject, Injectable } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SpaAuthViewService {
  private readonly oidcSecurityService = inject(OidcSecurityService);

  readonly mode = 'spa';
  readonly oidcAuthority = environment.oidc.authority;
  readonly oidcClientId = environment.oidc.clientId;
  readonly redirectUrl = environment.oidc.redirectUrl;
  readonly authState = this.oidcSecurityService.authenticated;
  readonly userData = this.oidcSecurityService.userData;
  readonly userClaims = computed(() => this.userData().userData ?? null);
  readonly loggedOutMessage = 'Not logged in yet. Use the login button to sign in with Keycloak.';

  login(): void {
    this.oidcSecurityService.authorize();
  }

  logout(): void {
    this.oidcSecurityService.logoff().subscribe();
  }
}
