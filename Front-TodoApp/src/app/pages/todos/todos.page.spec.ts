import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TodosPage } from './todos.page';
import { routes } from '../../app.routes';
import { authInterceptor } from '../../core/auth.interceptor';
import { SessionService } from '../../core/session.service';
import { Todo } from '../../models/todo.models';

function task(id: number, titulo: string): Todo {
  return {
    id, titulo, descricao: 'Descrição da tarefa', categoriaId: null, categoriaNome: null,
    prioridade: 'Media', dataCriacao: '2026-11-07T12:00:00Z', dataInicioUtc: null, dataFimUtc: null,
    concluido: false, dataConclusaoUtc: null, dataPrevistaUtc: null, frequenciaRecorrencia: null,
    duracaoRecorrencia: null, serieRecorrenciaId: null, numeroOcorrencia: null,
  };
}

async function renderDashboard(initialTodos = [task(81, 'Tarefa presente')]) {
  await TestBed.configureTestingModule({
    imports: [TodosPage],
    providers: [provideRouter(routes), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), SessionService],
  }).compileComponents();
  TestBed.overrideProvider(ActivatedRoute, { useValue: { snapshot: { queryParamMap: { get: () => null } } } });
  TestBed.inject(SessionService).setToken('dashboard-token');
  const fixture = TestBed.createComponent(TodosPage);
  fixture.detectChanges();
  const http = TestBed.inject(HttpTestingController);
  http.expectOne('/api/preferences').flush({ timeZoneId: 'America/Fortaleza' });
  http.expectOne('/api/categories').flush([]);
  http.expectOne('/api/todos?page=1&pageSize=15').flush({ items: initialTodos, page: 1, pageSize: 15, totalCount: initialTodos.length });
  fixture.detectChanges();
  return { fixture, http, page: fixture.componentInstance };
}

describe('TodosPage', () => {
  it('renders creation date and time in the account time zone and falls back to loaded category name', async () => {
    await TestBed.configureTestingModule({
      imports: [TodosPage],
      providers: [provideRouter(routes), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), SessionService],
    }).compileComponents();
    TestBed.overrideProvider(ActivatedRoute, { useValue: { snapshot: { queryParamMap: { get: (key: string) => key === 'warning' ? 'task-not-found' : null } } } });
    TestBed.inject(SessionService).setToken('dashboard-token');
    const fixture = TestBed.createComponent(TodosPage);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/preferences').flush({ timeZoneId: 'America/Fortaleza' });
    http.expectOne('/api/categories').flush([{ id: 9, nome: 'Trabalho' }]);
    http.expectOne('/api/todos?page=1&pageSize=15').flush({ items: [{ id: 81, titulo: 'Reunião', descricao: 'Pauta', categoriaId: 9, categoriaNome: null, prioridade: 'Alta', dataCriacao: '2026-11-07T12:00:00Z', dataInicioUtc: '2026-11-07T12:00:00Z', dataFimUtc: '2026-11-07T13:00:00Z', concluido: false, dataConclusaoUtc: null, dataPrevistaUtc: null, frequenciaRecorrencia: null, duracaoRecorrencia: null, serieRecorrenciaId: null, numeroOcorrencia: null }], page: 1, pageSize: 15, totalCount: 1 });
    fixture.detectChanges();
    const todo = fixture.componentInstance.todos()[0];
    expect((fixture.nativeElement as HTMLElement).querySelector('.material-symbols-outlined')).toBeNull();
    expect(fixture.componentInstance.categoryNameFor(todo)).toBe('Trabalho');
    expect(fixture.componentInstance.formatCreatedAt(todo.dataCriacao)).toBe('07/11/2026, 09:00');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('07/11/2026, 09:00');
    expect(text).toContain('Trabalho');
    expect(text).toContain('Configurações');
    expect(text).toContain('Sair da conta');
    expect(text).toContain('Concluídas');
    expect(text).toContain(' · America/Fortaleza');
    expect(text).not.toContain('â€¦');
    expect(text).not.toContain('\u00c2\u00b7');
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent).toContain('A tarefa não foi encontrada');
  });

  it('deletes a regular TODO immediately and reloads the API list', async () => {
    const { fixture, http, page } = await renderDashboard();
    const deleteButton = fixture.nativeElement.querySelector('.task-actions .danger-hover') as HTMLButtonElement;
    deleteButton.click();
    fixture.detectChanges();
    const request = http.expectOne('/api/todos/81');
    expect(request.request.method).toBe('DELETE');
    expect(fixture.nativeElement.querySelector('dialog.delete-dialog')).toBeNull();
    request.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    http.expectOne('/api/todos?page=1&pageSize=15').flush({ items: [], page: 1, pageSize: 15, totalCount: 0 });
    fixture.detectChanges();
    expect(page.todos()).toEqual([]);
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Tarefa presente');
  });

  it('keeps a TODO visible and reports an error when immediate deletion fails', async () => {
    const { fixture, http, page } = await renderDashboard();
    (fixture.nativeElement.querySelector('.task-actions .danger-hover') as HTMLButtonElement).click();
    http.expectOne('/api/todos/81').flush({}, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(page.todos().map((todo) => todo.id)).toEqual([81]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Tarefa presente');
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent).toContain('não foi encontrada');
    expect(page.submitting()).toBe(false);
  });

  it('immediately deletes only the selected recurring occurrence without a modal', async () => {
    const recurringTask = { ...task(81, 'Ocorrência'), serieRecorrenciaId: 12, numeroOcorrencia: 2 };
    const { fixture, http, page } = await renderDashboard([recurringTask]);
    (fixture.nativeElement.querySelector('.task-actions .danger-hover') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('dialog.delete-dialog')).toBeNull();
    http.expectOne('/api/todos/81?scope=occurrence').flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    http.expectOne('/api/todos?page=1&pageSize=15').flush({ items: [], page: 1, pageSize: 15, totalCount: 0 });
    fixture.detectChanges();
    expect(page.todos()).toEqual([]);
  });

  it('does not let an older list response overwrite the latest loaded TODOs', async () => {
    const { fixture, http, page } = await renderDashboard([]);
    page.loadTodos();
    page.loadTodos();
    const pendingLoads = http.match('/api/todos?page=1&pageSize=15');
    expect(pendingLoads.length).toBe(2);

    pendingLoads[1].flush({ items: [task(92, 'Tarefa atual')], page: 1, pageSize: 15, totalCount: 1 });
    pendingLoads[0].flush({ items: [task(91, 'Tarefa antiga')], page: 1, pageSize: 15, totalCount: 1 });
    fixture.detectChanges();

    expect(page.todos().map((todo) => todo.id)).toEqual([92]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Tarefa atual');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Tarefa antiga');
  });
});
