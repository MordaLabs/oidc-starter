export const environment = {
  production: true,
  appName: 'OIDC Starter UI',
  authMode: 'spa' as 'spa' | 'bff',
  apiOrigin: '',
  oidc: {
    authority: 'http://localhost:8080/realms/oidc-starter',
    clientId: 'oidc-starter-spa',
    redirectUrl: 'http://localhost:4200',
    postLogoutRedirectUri: 'http://localhost:4200',
    scope: 'openid profile email',
  },
};
