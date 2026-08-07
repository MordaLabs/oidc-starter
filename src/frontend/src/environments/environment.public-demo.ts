export const environment = {
  production: true,
  appName: 'OIDC Starter UI',
  authMode: 'bff' as 'bff' | 'spa',
  apiOrigin: '',
  oidc: {
    authority: '',
    clientId: '',
    redirectUrl: '',
    postLogoutRedirectUri: '',
    scope: '',
  },
};
