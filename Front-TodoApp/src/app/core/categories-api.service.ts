import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Category } from '../models/todo.models';

@Injectable({ providedIn: 'root' })
export class CategoriesApiService {
  private readonly http = inject(HttpClient);

  getAll(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories');
  }

  create(nome: string): Observable<Category> {
    return this.http.post<Category>('/api/categories', { nome });
  }

  update(id: number, nome: string): Observable<Category> {
    return this.http.put<Category>(`/api/categories/${id}`, { nome });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`/api/categories/${id}`);
  }
}
