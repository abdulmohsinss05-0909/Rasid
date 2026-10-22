import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;

  const session = (token: string) => ({
    accessToken: token,
    expiresUtc: '2026-10-20T12:15:00Z',
    user: { id: '1', email: 'a@b.c', displayName: 'Admin', roles: ['Admin'] },
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'login', children: [] }]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => backend.verify());

  it('adds the bearer token to API calls only', () => {
    auth.signIn('a@b.c', 'pw').subscribe();
    backend.expectOne('/api/v1/auth/login').flush(session('token-1'));

    http.get('/api/v1/topics').subscribe();
    expect(backend.expectOne('/api/v1/topics').request.headers.get('Authorization')).toBe('Bearer token-1');

    http.get('https://example.org/other').subscribe();
    expect(backend.expectOne('https://example.org/other').request.headers.has('Authorization')).toBe(false);
  });

  it('renews the session once on 401 and retries with the new token', () => {
    auth.signIn('a@b.c', 'pw').subscribe();
    backend.expectOne('/api/v1/auth/login').flush(session('old'));

    let result: unknown;
    http.get('/api/v1/topics').subscribe((r) => (result = r));

    backend.expectOne('/api/v1/topics').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/v1/auth/refresh').flush(session('new'));

    const retry = backend.expectOne('/api/v1/topics');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new');
    retry.flush([{ id: 't1' }]);

    expect(result).toEqual([{ id: 't1' }]);
  });

  it('signs the user out when renewal fails', () => {
    auth.signIn('a@b.c', 'pw').subscribe();
    backend.expectOne('/api/v1/auth/login').flush(session('old'));

    http.get('/api/v1/topics').subscribe({ error: () => undefined });
    backend.expectOne('/api/v1/topics').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isAuthenticated()).toBe(false);
  });
});
