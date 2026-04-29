export const environment = {
  production: false,
  appName: 'OIDC Starter UI',
  // Switch between 'bff' for backend session auth and 'spa' for direct browser OIDC.
  authMode: 'bff' as 'bff' | 'spa',
  // Empty uses the Angular dev-server proxy. Use an origin such as 'https://localhost:7233'
  // when calling the backend directly.
  apiOrigin: '',
  oidc: {
    authority: 'http://localhost:8080/realms/oidc-starter',
    clientId: 'oidc-starter-spa',
    redirectUrl: 'http://localhost:4200',
    postLogoutRedirectUri: 'http://localhost:4200',
    scope: 'openid profile email',
  },
};
