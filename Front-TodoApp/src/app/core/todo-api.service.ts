import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { expand, map, Observable, reduce } from 'rxjs';
import { Todo, TodoMutation, TodoPage } from '../models/todo.models';

const PAGE_SIZE = 15;

@Injectable({ providedIn: 'root' })
export class TodoApiService {
  private readonly http = inject(HttpClient);

  getAll(): Observable<Todo[]> {
    return this.getPage(1).pipe(
      expand((page) => {
        const loaded = (page.page - 1) * page.pageSize + page.items.length;
        return loaded < page.totalCount && page.items.length > 0
          ? this.getPage(page.page + 1)
          : [];
      }),
      reduce((all, page) => {
        for (const todo of page.items) all.set(todo.id, todo);
        return all;
      }, new Map<number, Todo>()),
      map((itemsById) => [...itemsById.values()]),
    );
  }

  create(todo: TodoMutation): Observable<Todo> {
    return this.http.post<Todo>('/api/todos', todo);
  }

  getById(id: number): Observable<Todo> {
    return this.http.get<Todo>(`/api/todos/${id}`);
  }

  update(id: number, todo: TodoMutation): Observable<Todo> {
    return this.http.put<Todo>(`/api/todos/${id}`, todo);
  }

  delete(todo: Todo, scope?: 'occurrence' | 'series'): Observable<void> {
    if (todo.serieRecorrenciaId !== null && scope === 'series') {
      return this.http.delete<void>(`/api/todo-series/${todo.serieRecorrenciaId}`);
    }

    let params = new HttpParams();
    if (todo.serieRecorrenciaId !== null && scope === 'occurrence') {
      params = params.set('scope', 'occurrence');
    }
    return this.http.delete<void>(`/api/todos/${todo.id}`, { params });
  }

  private getPage(page: number): Observable<TodoPage> {
    const params = new HttpParams().set('page', page).set('pageSize', PAGE_SIZE);
    return this.http.get<TodoPage>('/api/todos', { params });
  }
}
