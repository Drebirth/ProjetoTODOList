import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthApiService } from './auth-api.service';
import { authInterceptor } from './auth.interceptor';
import { SessionService } from './session.service';
import { TodoApiService } from './todo-api.service';

describe('AuthApiService', () => {
  let auth: AuthApiService;
  let http: HttpTestingController;
  let session: SessionService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), AuthApiService, SessionService],
    });
    auth = TestBed.inject(AuthApiService);
    http = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionService);
  });

  afterEach(() => http.verify());

  it('registers on the public route without inventing a session', () => {
    auth.register({ username: 'ana', password: 'senha' }).subscribe();
    const request = http.expectOne('/api/auth/register');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ username: 'ana', password: 'senha' });
    request.flush({ id: 1, username: 'ana' });
    expect(session.token()).toBeNull();
  });

  it('stores the camelCase login response token for subsequent requests', () => {
    auth.login({ username: 'ana', password: 'senha' }).subscribe();
    http.expectOne('/api/auth/login').flush({ token: 'jwt-value', tokenType: 'Bearer', expiresAtUtc: '2026-11-07T14:00:00Z' });
    expect(session.token()).toBe('jwt-value');
  });

  it('clears the stored session when an authorized API request returns 401', () => {
    session.setToken('expired');
    TestBed.inject(TodoApiService).getAll().subscribe({ error: (error: HttpErrorResponse) => expect(error.status).toBe(401) });
    const request = http.expectOne('/api/todos?page=1&pageSize=15');
    expect(request.request.headers.get('Authorization')).toBe('Bearer expired');
    request.flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    expect(session.token()).toBeNull();
  });
});
