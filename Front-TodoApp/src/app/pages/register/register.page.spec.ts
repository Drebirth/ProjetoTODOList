import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RegisterPage } from './register.page';
import { routes } from '../../app.routes';
import { authInterceptor } from '../../core/auth.interceptor';
import { SessionService } from '../../core/session.service';

describe('RegisterPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterPage],
      providers: [provideRouter(routes), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), SessionService],
    }).compileComponents();
  });

  it('registers from its own page then routes to login', async () => {
    const fixture = TestBed.createComponent(RegisterPage);
    const router = TestBed.inject(Router);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Crie sua conta');
    expect(text).toContain('Já tem uma conta?');
    expect((fixture.nativeElement as HTMLElement).querySelector('.material-symbols-outlined')).toBeNull();
    expect(text).toContain('Criar conta');
    fixture.componentInstance.username = 'ana';
    fixture.componentInstance.password = 'senha';
    fixture.componentInstance.submit();
    const request = http.expectOne('/api/auth/register');
    expect(request.request.body).toEqual({ username: 'ana', password: 'senha' });
    request.flush({ id: 2, username: 'ana' });
    await fixture.whenStable();
    expect(router.url).toBe('/login');
  });
});
