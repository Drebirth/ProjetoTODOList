import { HttpErrorResponse } from '@angular/common/http';
import { inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthApiService } from './auth-api.service';

export abstract class AuthPageBase {
  protected readonly authApi = inject(AuthApiService);
  protected readonly router = inject(Router);
  readonly submitting = signal(false);
  readonly error = signal('');
  username = '';
  password = '';

  protected submitAuth(register: boolean): void {
    this.error.set('');
    this.submitting.set(true);
    const credentials = { username: this.username.trim(), password: this.password };
    const next = () => {
      this.submitting.set(false);
      this.password = '';
      void this.router.navigate(register ? ['/login'] : ['/todos']);
    };
    const failure = (error: unknown) => {
      this.submitting.set(false);
      this.error.set(this.errorMessage(error));
    };
    if (register) this.authApi.register(credentials).subscribe({ next, error: failure });
    else this.authApi.login(credentials).subscribe({ next, error: failure });
  }

  private errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 409) return 'Este nome de usuário já está cadastrado.';
    if (error instanceof HttpErrorResponse && error.status === 401) return 'Usuário ou senha inválidos.';
    if (error instanceof HttpErrorResponse && error.status === 400) return 'Confira os dados. O usuário deve ter até 50 caracteres.';
    return 'Não foi possível conectar à API. Tente novamente.';
  }
}
