import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { Credentials, LoginResponse, RegisteredUser } from '../models/todo.models';
import { SessionService } from './session.service';

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);

  login(credentials: Credentials): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', credentials).pipe(
      tap((response) => this.session.setToken(response.token)),
    );
  }

  register(credentials: Credentials): Observable<RegisteredUser> {
    return this.http.post<RegisteredUser>('/api/auth/register', credentials);
  }
}
