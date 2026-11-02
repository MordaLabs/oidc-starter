export const environment = {
  production: true,
  appName: 'OIDC Starter UI',
  authMode: 'bff' as 'bff' | 'spa',
  apiOrigin: '',
  invitationRequestUiEnabled: true,
  operatorDashboardUiEnabled: true,
  oidc: {
    authority: '',
    clientId: '',
    redirectUrl: '',
    postLogoutRedirectUri: '',
    scope: '',
  },
};
