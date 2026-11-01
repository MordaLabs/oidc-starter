import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { LOCALE_STORAGE_KEY, LocaleService } from '../locale.service';
import { InvitationRequestComponent } from './invitation-request.component';

describe('InvitationRequestComponent', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<InvitationRequestComponent>;
  const originalApiOrigin = environment.apiOrigin;

  beforeEach(async () => {
    environment.apiOrigin = '';
    localStorage.setItem(LOCALE_STORAGE_KEY, 'en');
    await TestBed.configureTestingModule({
      imports: [InvitationRequestComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InvitationRequestComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify();
    environment.apiOrigin = originalApiOrigin;
    document.cookie = 'XSRF-TOKEN=; Max-Age=0; path=/';
    localStorage.removeItem(LOCALE_STORAGE_KEY);
  });

  function form(): HTMLFormElement {
    return fixture.nativeElement.querySelector('form');
  }

  function input(): HTMLInputElement {
    return fixture.nativeElement.querySelector('#invitation-email');
  }

  function setEmail(value: string): void {
    input().value = value;
    input().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function submit(): void {
    form().dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
    fixture.detectChanges();
  }

  function csrf(token = 'request-token'): void {
    const request = http.expectOne('/api/auth/csrf');
    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBeTrue();
    document.cookie = `XSRF-TOKEN=${token}; path=/`;
    request.flush('');
    fixture.detectChanges();
  }

  it('has an associated label and prevents empty, malformed, and overlong email submissions', () => {
    expect(fixture.nativeElement.querySelector('label')?.htmlFor).toBe(input().id);
    submit();
    expect(input().getAttribute('aria-invalid')).toBe('true');
    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent).toContain('Check your email');
    setEmail('not-an-email');
    submit();
    setEmail(`${'a'.repeat(250)}@example.com`);
    submit();
    expect(http.match(() => true)).toHaveSize(0);
  });

  it('acquires CSRF before one JSON POST, sends only the trimmed email, and shows neutral success', () => {
    setEmail('  person@example.com  ');
    submit();
    expect(http.match('/api/demo/invitation-requests')).toHaveSize(0);
    expect(form().querySelector('button')?.hasAttribute('disabled')).toBeTrue();
    submit();
    csrf();
    const post = http.expectOne('/api/demo/invitation-requests');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ email: 'person@example.com' });
    expect(Object.keys(post.request.body)).toEqual(['email']);
    expect(post.request.headers.get('Content-Type')).toBe('application/json');
    expect(post.request.headers.get('X-XSRF-TOKEN')).toBe('request-token');
    expect(post.request.withCredentials).toBeTrue();
    expect(post.request.url).not.toContain('person@example.com');
    post.flush('', { status: 202, statusText: 'Accepted' });
    fixture.detectChanges();
    expect(input().value).toBe('');
    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent)
      .toContain('received for review');
  });

  it('refuses cross-origin API configuration before acquiring or forwarding a token', () => {
    environment.apiOrigin = 'https://other.example.test';
    setEmail('person@example.com');
    submit();
    expect(http.match(() => true)).toHaveSize(0);
    expect(input().value).toBe('person@example.com');
    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent)
      .toContain('Security verification failed');
  });

  it('keeps same-origin configured APIs on relative routes', () => {
    environment.apiOrigin = window.location.origin;
    setEmail('person@example.com');
    submit();
    csrf();
    const post = http.expectOne('/api/demo/invitation-requests');
    expect(post.request.url).toBe('/api/demo/invitation-requests');
    post.flush('', { status: 202, statusText: 'Accepted' });
  });

  it('does not treat an unexpected successful HTTP status as acceptance', () => {
    setEmail('person@example.com');
    submit();
    csrf();
    http.expectOne('/api/demo/invitation-requests')
      .flush('', { status: 200, statusText: 'OK' });
    fixture.detectChanges();
    expect(input().value).toBe('person@example.com');
    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent)
      .toContain('temporarily unavailable');
  });

  it('refuses to post when CSRF acquisition did not produce a request token', () => {
    setEmail('person@example.com');
    submit();
    http.expectOne('/api/auth/csrf').flush('');
    fixture.detectChanges();
    expect(http.match('/api/demo/invitation-requests')).toHaveSize(0);
    expect(input().value).toBe('person@example.com');
  });

  for (const [status, message] of [
    [400, 'Check your email'],
    [401, 'Security verification failed'],
    [403, 'Security verification failed'],
    [404, 'currently unavailable'],
    [429, 'Too many requests'],
    [503, 'temporarily unavailable'],
    [0, 'temporarily unavailable'],
  ] as const) {
    it(`maps HTTP ${status} safely, preserves the email, and allows retry`, () => {
      setEmail('person@example.com');
      submit();
      csrf();
      http.expectOne('/api/demo/invitation-requests')
        .flush('internal detail', { status, statusText: 'Failure' });
      fixture.detectChanges();
      expect(input().value).toBe('person@example.com');
      expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent).toContain(message);
      expect(fixture.nativeElement.textContent).not.toContain('internal detail');
      submit();
      csrf();
      http.expectOne('/api/demo/invitation-requests')
        .flush('', { status: 202, statusText: 'Accepted' });
      fixture.detectChanges();
      expect(input().value).toBe('');
    });
  }

  it('updates input and success copy across EN/PL without resubmitting or clearing input', () => {
    const locale = TestBed.inject(LocaleService);
    setEmail('person@example.com');
    locale.selectLocale('pl');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('label')?.textContent).toContain('Adres e-mail');
    expect(input().value).toBe('person@example.com');
    expect(http.match(() => true)).toHaveSize(0);
    submit();
    csrf();
    http.expectOne('/api/demo/invitation-requests')
      .flush('', { status: 202, statusText: 'Accepted' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent)
      .toContain('przyjęte do rozpatrzenia');
    locale.selectLocale('en');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent)
      .toContain('received for review');
    expect(http.match(() => true)).toHaveSize(0);
  });
});
