import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BffAuthService } from '@mordalabs/oidc-starter-auth';
import { environment } from '../../environments/environment';
import { LOCALE_STORAGE_KEY, LocaleService } from '../locale.service';
import { OperatorDashboardComponent } from './operator-dashboard.component';
import { OperatorDashboardService, OperatorRequestRecord } from './operator-dashboard.service';

const requestId = '11111111-1111-4111-8111-111111111111';
const invitationId = '22222222-2222-4222-8222-222222222222';
const pending: OperatorRequestRecord = {
  id: requestId, email: 'person@example.com', createdAt: '2026-10-30T12:30:00Z',
  status: 'pending', decidedAt: null, invitationId: null,
};
const approved: OperatorRequestRecord = {
  ...pending, status: 'approved', decidedAt: '2026-10-31T13:45:00Z', invitationId,
};
const rejected: OperatorRequestRecord = {
  ...pending, status: 'rejected', decidedAt: '2026-10-31T13:45:00Z', invitationId: null,
};

describe('OperatorDashboardComponent', () => {
  let fixture: ComponentFixture<OperatorDashboardComponent>;
  let http: HttpTestingController;
  let authenticated: ReturnType<typeof signal<boolean>>;
  let loggingOut: ReturnType<typeof signal<boolean>>;
  const originalApiOrigin = environment.apiOrigin;

  beforeEach(async () => {
    environment.apiOrigin = '';
    localStorage.setItem(LOCALE_STORAGE_KEY, 'en');
    authenticated = signal(true);
    loggingOut = signal(false);
    await TestBed.configureTestingModule({
      imports: [OperatorDashboardComponent],
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: BffAuthService, useValue: { authenticated, isLoggingOut: loggingOut } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(OperatorDashboardComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify({ ignoreCancelled: true });
    environment.apiOrigin = originalApiOrigin;
    document.cookie = 'XSRF-TOKEN=; Max-Age=0; path=/';
    localStorage.removeItem(LOCALE_STORAGE_KEY);
  });

  function root(): HTMLElement { return fixture.nativeElement as HTMLElement; }
  function click(label: string): void {
    const button = Array.from(root().querySelectorAll('button'))
      .find((item) => item.textContent?.trim() === label);
    expect(button).withContext(`Missing button: ${label}`).toBeDefined();
    button!.click();
    fixture.detectChanges();
  }
  function open(records: OperatorRequestRecord[] = [pending]): void {
    click('Operator workspace');
    const request = http.expectOne('/api/demo/operator/invitation-requests?status=pending');
    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBeTrue();
    request.flush(records);
    fixture.detectChanges();
  }
  function csrf(): void {
    const request = http.expectOne('/api/auth/csrf');
    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBeTrue();
    expect(http.match((item) => item.method === 'POST')).toHaveSize(0);
    document.cookie = 'XSRF-TOKEN=operator-token; path=/';
    request.flush('');
    fixture.detectChanges();
  }
  function chooseStatus(status: 'approved' | 'rejected', records: OperatorRequestRecord[]): void {
    const select = root().querySelector<HTMLSelectElement>('#operator-status')!;
    select.value = status;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const request = http.expectOne(`/api/demo/operator/invitation-requests?status=${status}`);
    expect(request.request.withCredentials).toBeTrue();
    request.flush(records);
    fixture.detectChanges();
  }

  it('loads nothing until explicit open, then renders a bounded request list and timestamps', () => {
    expect(root().querySelector('.workspace-entry')).not.toBeNull();
    expect(root().querySelector('.workspace-entry')?.getAttribute('aria-expanded')).toBe('false');
    expect(http.match(() => true)).toHaveSize(0);
    open(Array.from({ length: 51 }, (_, index) => ({ ...pending, id: `${index}` })));
    expect(root().querySelector('.workspace-entry')?.getAttribute('aria-expanded')).toBe('true');
    expect(root().querySelectorAll('.request-card')).toHaveSize(50);
    expect(root().textContent).toContain('first 50 requests');
    expect(root().textContent).toContain('person@example.com');
    expect(root().querySelector('time')?.textContent).toContain('UTC');
    expect(root().querySelector('.request-email')?.innerHTML).toBe('person@example.com');
  });

  it('filters only by allowed status, shows decision details, and localizes without requests', () => {
    open();
    chooseStatus('approved', [approved]);
    expect(root().textContent).toContain(invitationId);
    expect(root().querySelectorAll('time')).toHaveSize(2);
    expect(root().textContent).toContain('Revoke invitation');
    expect(root().textContent).not.toContain('Active');
    expect(root().textContent).not.toContain('Revoked');
    const locale = TestBed.inject(LocaleService);
    locale.selectLocale('pl');
    fixture.detectChanges();
    expect(root().textContent).toContain('Cofnij zaproszenie');
    expect(root().textContent).toContain('Rozpatrzono');
    expect(root().textContent).toContain('UTC');
    expect(http.match(() => true)).toHaveSize(0);
    chooseStatus('rejected', [rejected]);
    expect(root().textContent).not.toContain('Cofnij zaproszenie');
    expect(root().textContent).not.toContain('Zatwierdź');
  });

  it('ignores unsupported filter values without requesting data', () => {
    open();
    const select = root().querySelector<HTMLSelectElement>('#operator-status')!;
    select.value = 'unexpected';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(http.match(() => true)).toHaveSize(0);
    expect(root().textContent).toContain('person@example.com');
  });

  for (const status of [401, 403]) {
    it(`clears data and controls on operator GET ${status}`, () => {
      open();
      click('Refresh requests');
      http.expectOne('/api/demo/operator/invitation-requests?status=pending')
        .flush('private detail', { status, statusText: 'Denied' });
      fixture.detectChanges();
      expect(root().textContent).not.toContain('person@example.com');
      expect(root().querySelector('.request-actions')).toBeNull();
      expect(root().textContent).toContain('Operator access could not be verified');
      expect(root().textContent).not.toContain('private detail');
      click('Refresh requests');
      http.expectOne('/api/demo/operator/invitation-requests?status=pending').flush([]);
      fixture.detectChanges();
      expect(root().textContent).toContain('No requests found');
    });
  }

  for (const [action, record, expectedPath, success] of [
    ['approve', pending, `/api/demo/operator/invitation-requests/${requestId}/approve`, 'Request approved'],
    ['reject', pending, `/api/demo/operator/invitation-requests/${requestId}/reject`, 'Request rejected'],
    ['revoke', approved, `/api/demo/operator/invitations/${invitationId}/revoke`, 'Invitation revocation accepted'],
  ] as const) {
    it(`${action} needs confirmation and a bodyless CSRF-protected POST, then refreshes`, () => {
      open(action === 'revoke' ? [] : [record]);
      if (action === 'revoke') chooseStatus('approved', [record]);
      click(action === 'approve' ? 'Approve' : action === 'reject' ? 'Reject' : 'Revoke invitation');
      expect(root().querySelector('.confirmation')).not.toBeNull();
      if (action === 'revoke') {
        expect(root().textContent).toContain('existing enforced Google or GitHub sessions');
      }
      expect(http.match((item) => item.method === 'POST')).toHaveSize(0);
      click('Cancel');
      expect(root().querySelector('.confirmation')).toBeNull();
      expect(http.match(() => true)).toHaveSize(0);
      click(action === 'approve' ? 'Approve' : action === 'reject' ? 'Reject' : 'Revoke invitation');
      click('Confirm action');
      csrf();
      const post = http.expectOne(expectedPath);
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toBeNull();
      expect(post.request.headers.has('Content-Type')).toBeFalse();
      expect(post.request.detectContentTypeHeader()).toBeNull();
      expect(post.request.headers.get('X-XSRF-TOKEN')).toBe('operator-token');
      expect(post.request.withCredentials).toBeTrue();
      expect(post.request.url).not.toContain(record.email);
      expect(root().textContent).not.toContain('operator-token');
      click('Submitting...');
      expect(http.match((item) => item.method === 'POST')).toHaveSize(0);
      post.flush('', { status: 204, statusText: 'No Content' });
      fixture.detectChanges();
      expect(root().textContent).toContain(success);
      expect(root().querySelector('.confirmation')).toBeNull();
      http.expectOne(`/api/demo/operator/invitation-requests?status=${action === 'revoke' ? 'approved' : 'pending'}`)
        .flush(action === 'revoke' ? [approved] : []);
      fixture.detectChanges();
      if (action === 'revoke') {
        expect(root().textContent).toContain(invitationId);
        expect(root().textContent).not.toContain('Revoked');
      }
    });
  }

  for (const [status, message] of [
    [404, 'not found'], [409, 'state may have changed'],
    [503, 'temporarily unavailable'], [0, 'temporarily unavailable'],
  ] as const) {
    it(`handles mutation ${status} without optimistic state`, () => {
      open();
      click('Approve');
      click('Confirm action');
      csrf();
      http.expectOne(`/api/demo/operator/invitation-requests/${requestId}/approve`)
        .flush('server detail', { status, statusText: 'Failure' });
      fixture.detectChanges();
      expect(root().textContent).toContain(message);
      expect(root().textContent).not.toContain('person@example.com');
      expect(root().textContent).not.toContain('server detail');
      expect(root().querySelector('.request-actions')).toBeNull();
    });
  }

  for (const status of [401, 403]) {
    it(`removes operator data on mutation ${status}`, () => {
      open();
      click('Approve');
      click('Confirm action');
      csrf();
      http.expectOne(`/api/demo/operator/invitation-requests/${requestId}/approve`)
        .flush('', { status, statusText: 'Denied' });
      fixture.detectChanges();
      expect(root().textContent).not.toContain('person@example.com');
      expect(root().querySelector('.request-actions')).toBeNull();
      expect(root().textContent).toContain('Operator access could not be verified');
    });
  }

  it('clears operator data when CSRF acquisition is denied', () => {
    open();
    click('Approve');
    click('Confirm action');
    http.expectOne('/api/auth/csrf').flush('', { status: 403, statusText: 'Denied' });
    fixture.detectChanges();
    expect(root().textContent).not.toContain('person@example.com');
    expect(root().textContent).toContain('Operator access could not be verified');
    expect(http.match((request) => request.method === 'POST')).toHaveSize(0);
  });

  it('keeps a pending confirmation across locale changes without sending requests', () => {
    open();
    click('Approve');
    TestBed.inject(LocaleService).selectLocale('pl');
    fixture.detectChanges();
    expect(root().querySelector('.confirmation')?.textContent).toContain('Zatwierdzić');
    expect(root().querySelector('.confirmation')?.textContent).toContain('person@example.com');
    expect(http.match(() => true)).toHaveSize(0);
  });

  it('treats unexpected HTTP 200 on mutation as failure and retains no controls', () => {
    open();
    click('Approve');
    click('Confirm action');
    csrf();
    http.expectOne(`/api/demo/operator/invitation-requests/${requestId}/approve`)
      .flush('', { status: 200, statusText: 'OK' });
    fixture.detectChanges();
    expect(root().textContent).toContain('temporarily unavailable');
    expect(root().querySelector('.request-actions')).toBeNull();
  });

  it('refuses cross-origin configuration before GET or POST', () => {
    environment.apiOrigin = 'https://other.example.test';
    click('Operator workspace');
    expect(http.match(() => true)).toHaveSize(0);
    expect(root().textContent).toContain('Operator access could not be verified');
    const service = TestBed.inject(OperatorDashboardService);
    service.mutate('approve', requestId).subscribe({ error: () => undefined });
    expect(http.match(() => true)).toHaveSize(0);
  });

  it('closes, cancels stale loads, and requires a fresh GET on reopen', () => {
    click('Operator workspace');
    const stale = http.expectOne('/api/demo/operator/invitation-requests?status=pending');
    click('Close workspace');
    expect(stale.cancelled).toBeTrue();
    fixture.detectChanges();
    expect(root().textContent).not.toContain('person@example.com');
    click('Operator workspace');
    http.expectOne('/api/demo/operator/invitation-requests?status=pending').flush([]);
    fixture.detectChanges();
    expect(root().textContent).toContain('No requests found');
  });

  it('cancels an in-flight mutation when the workspace closes', () => {
    open();
    click('Approve');
    click('Confirm action');
    csrf();
    const post = http.expectOne(`/api/demo/operator/invitation-requests/${requestId}/approve`);
    click('Close workspace');
    expect(post.cancelled).toBeTrue();
    expect(root().textContent).not.toContain('person@example.com');
    click('Operator workspace');
    http.expectOne('/api/demo/operator/invitation-requests?status=pending').flush([]);
    fixture.detectChanges();
    expect(root().textContent).toContain('No requests found');
  });

  for (const loss of ['logout', 'session'] as const) {
    it(`clears data and cancels stale responses on ${loss} loss`, () => {
      open();
      click('Refresh requests');
      const stale = http.expectOne('/api/demo/operator/invitation-requests?status=pending');
      if (loss === 'logout') loggingOut.set(true);
      else authenticated.set(false);
      fixture.detectChanges();
      expect(stale.cancelled).toBeTrue();
      fixture.detectChanges();
      expect(root().textContent).not.toContain('person@example.com');
      expect(root().querySelector('.workspace-entry')).toBeNull();
      expect(root().querySelector('.request-actions')).toBeNull();
      if (loss === 'logout') loggingOut.set(false);
      else authenticated.set(true);
      fixture.detectChanges();
      expect(root().querySelector('.workspace-entry')).not.toBeNull();
      expect(root().textContent).not.toContain('person@example.com');
    });
  }
});
