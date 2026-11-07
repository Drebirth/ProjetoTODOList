import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { PreferencesApiService } from './preferences-api.service';
import { SessionService } from './session.service';

describe('PreferencesApiService', () => {
  let service: PreferencesApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), PreferencesApiService, SessionService],
    });
    service = TestBed.inject(PreferencesApiService);
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(SessionService).setToken('prefs-token');
  });

  afterEach(() => http.verify());

  it('gets the authenticated account time zone', () => {
    service.get().subscribe();
    const request = http.expectOne('/api/preferences');
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer prefs-token');
    request.flush({ timeZoneId: 'America/Fortaleza' });
  });

  it('updates the IANA time zone with the documented body', () => {
    service.update('America/New_York').subscribe();
    const request = http.expectOne('/api/preferences');
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('Authorization')).toBe('Bearer prefs-token');
    expect(request.request.body).toEqual({ timeZoneId: 'America/New_York' });
    request.flush({ timeZoneId: 'America/New_York' });
  });
});
