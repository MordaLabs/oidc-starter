import { environment as defaultEnvironment } from './environment';
import { environment as productionEnvironment } from './environment.production';
import { environment as publicDemoEnvironment } from './environment.public-demo';
import { environment as acceptanceEnvironment } from './environment.public-demo-acceptance';

describe('public demo build environments', () => {
  it('keeps both invitation interfaces disabled in the public demo', () => {
    expect(publicDemoEnvironment.production).toBeTrue();
    expect(publicDemoEnvironment.authMode).toBe('bff');
    expect(publicDemoEnvironment.apiOrigin).toBe('');
    expect(publicDemoEnvironment.invitationRequestUiEnabled).toBeFalse();
    expect(publicDemoEnvironment.operatorDashboardUiEnabled).toBeFalse();
  });

  it('enables both interfaces only in the same-origin BFF acceptance environment', () => {
    expect(acceptanceEnvironment.production).toBeTrue();
    expect(acceptanceEnvironment.authMode).toBe('bff');
    expect(acceptanceEnvironment.apiOrigin).toBe('');
    expect(acceptanceEnvironment.invitationRequestUiEnabled).toBeTrue();
    expect(acceptanceEnvironment.operatorDashboardUiEnabled).toBeTrue();
    expect(acceptanceEnvironment.oidc).toEqual(publicDemoEnvironment.oidc);
  });

  it('keeps the default and production SPA environments disabled', () => {
    for (const environment of [defaultEnvironment, productionEnvironment]) {
      expect(environment.authMode).toBe('spa');
      expect(environment.invitationRequestUiEnabled).toBeFalse();
      expect(environment.operatorDashboardUiEnabled).toBeFalse();
    }
  });
});
