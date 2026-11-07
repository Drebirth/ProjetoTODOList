import { PLATFORM_ID } from '@angular/core';
import { Router, UrlTree } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { SessionService } from './core/session.service';
import { routes } from './app.routes';
import { LoginPage } from './pages/login/login.page';
import { RegisterPage } from './pages/register/register.page';
import { TodosPage } from './pages/todos/todos.page';
import { TodoFormPage } from './pages/todo-form/todo-form.page';

describe('application routes', () => {
  it('renders independent login, register, dashboard, create and edit pages', () => {
    expect(routes.find((route) => route.path === 'login')?.component).toBe(LoginPage);
    expect(routes.find((route) => route.path === 'register')?.component).toBe(RegisterPage);
    expect(routes.find((route) => route.path === 'todos')?.component).toBe(TodosPage);
    expect(routes.find((route) => route.path === 'todos/new')?.component).toBe(TodoFormPage);
    expect(routes.find((route) => route.path === 'todos/:id/edit')?.component).toBe(TodoFormPage);
    expect(routes.find((route) => route.path === '')?.redirectTo).toBe('todos');
    expect(routes.find((route) => route.path === '**')?.redirectTo).toBe('login');
  });

  it('protects pages in browser and restores a browser session before deciding', () => {
    sessionStorage.removeItem('foco.jwt');
    TestBed.configureTestingModule({ providers: [provideRouter(routes), { provide: PLATFORM_ID, useValue: 'browser' }, SessionService] });
    const session = TestBed.inject(SessionService);
    const router = TestBed.inject(Router);
    const destination = TestBed.runInInjectionContext(() => authGuard({} as never, { url: '/todos/new' } as never));
    expect(destination instanceof UrlTree).toBe(true);
    expect(router.serializeUrl(destination as UrlTree)).toContain('/login');
    session.setToken('browser-token');
    expect(TestBed.runInInjectionContext(() => authGuard({} as never, { url: '/todos' } as never))).toBe(true);
  });

  it('allows server rendering without touching browser session storage', () => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes), { provide: PLATFORM_ID, useValue: 'server' }, SessionService] });
    expect(TestBed.runInInjectionContext(() => authGuard({} as never, { url: '/todos' } as never))).toBe(true);
  });
});
