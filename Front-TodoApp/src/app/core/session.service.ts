import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';

const TOKEN_KEY = 'foco.jwt';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly currentToken = signal<string | null>(null);

  readonly token = this.currentToken.asReadonly();

  setToken(token: string): void {
    if (this.isBrowser) {
      sessionStorage.setItem(TOKEN_KEY, token);
    }
    this.currentToken.set(token);
  }

  restore(): void {
    this.currentToken.set(this.readToken());
  }

  clear(): void {
    if (this.isBrowser) {
      sessionStorage.removeItem(TOKEN_KEY);
    }
    this.currentToken.set(null);
  }

  private readToken(): string | null {
    return this.isBrowser ? sessionStorage.getItem(TOKEN_KEY) : null;
  }
}
