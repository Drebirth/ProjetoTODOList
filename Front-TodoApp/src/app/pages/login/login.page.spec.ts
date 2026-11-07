import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { LoginPage } from './login.page';
import { routes } from '../../app.routes';
import { authInterceptor } from '../../core/auth.interceptor';
import { SessionService } from '../../core/session.service';

describe('LoginPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [provideRouter(routes), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), SessionService],
    }).compileComponents();
  });

  it('logs in from its own page and navigates to the protected dashboard', async () => {
    const fixture = TestBed.createComponent(LoginPage);
    const router = TestBed.inject(Router);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Entre na sua conta');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ainda não tem conta?');
    fixture.componentInstance.username = 'ana';
    fixture.componentInstance.password = 'senha';
    fixture.componentInstance.submit();
    const request = http.expectOne('/api/auth/login');
    expect(request.request.body).toEqual({ username: 'ana', password: 'senha' });
    request.flush({ token: 'signed-in', tokenType: 'Bearer', expiresAtUtc: '2026-11-07T14:00:00Z' });
    await fixture.whenStable();
    expect(TestBed.inject(SessionService).token()).toBe('signed-in');
    expect(router.url).toBe('/todos');
  });

  it('renders the page without Material Symbols and keeps textual controls', async () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('.material-symbols-outlined')).toBeNull();
    expect(root.textContent).toContain('Entrar');
  });

});
