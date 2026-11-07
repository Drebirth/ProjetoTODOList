import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AccountPreferences } from '../models/todo.models';

@Injectable({ providedIn: 'root' })
export class PreferencesApiService {
  private readonly http = inject(HttpClient);

  get(): Observable<AccountPreferences> {
    return this.http.get<AccountPreferences>('/api/preferences');
  }

  update(timeZoneId: string): Observable<AccountPreferences> {
    return this.http.put<AccountPreferences>('/api/preferences', { timeZoneId });
  }
}
