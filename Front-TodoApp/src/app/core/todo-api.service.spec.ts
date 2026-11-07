import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { SessionService } from './session.service';
import { TodoApiService } from './todo-api.service';
import { Todo } from '../models/todo.models';

describe('TodoApiService', () => {
  let service: TodoApiService;
  let http: HttpTestingController;

  const todo: Todo = {
    id: 7,
    titulo: 'Ler documentação',
    descricao: 'Ler documentação',
    categoriaId: 2,
    categoriaNome: 'Estudos',
    prioridade: 'Media',
    dataCriacao: '2026-11-07T12:00:00Z',
    dataInicioUtc: '2026-11-09T12:00:00Z',
    dataFimUtc: '2026-11-09T13:00:00Z',
    concluido: false,
    dataConclusaoUtc: null,
    dataPrevistaUtc: null,
    frequenciaRecorrencia: null,
    duracaoRecorrencia: null,
    serieRecorrenciaId: null,
    numeroOcorrencia: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        TodoApiService,
        SessionService,
      ],
    });
    service = TestBed.inject(TodoApiService);
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(SessionService).setToken('jwt-test');
  });

  afterEach(() => http.verify());

  it('loads each page with the bearer token and camelCase response fields', () => {
    let result: Todo[] | undefined;
    service.getAll().subscribe((items) => (result = items));

    const first = http.expectOne('/api/todos?page=1&pageSize=15');
    expect(first.request.headers.get('Authorization')).toBe('Bearer jwt-test');
    first.flush({ items: [todo], page: 1, pageSize: 15, totalCount: 16 });

    const second = http.expectOne('/api/todos?page=2&pageSize=15');
    second.flush({ items: [{ ...todo, id: 8 }], page: 2, pageSize: 15, totalCount: 16 });
    expect(result?.map((item) => item.id)).toEqual([7, 8]);
  });

  it('does not return duplicate TODO ids when page contents shift during pagination', () => {
    let result: Todo[] | undefined;
    service.getAll().subscribe((items) => result = items);

    const first = http.expectOne('/api/todos?page=1&pageSize=15');
    const firstPageItems = Array.from({ length: 15 }, (_, index) => ({ ...todo, id: index + 1 }));
    first.flush({ items: firstPageItems, page: 1, pageSize: 15, totalCount: 16 });

    http.expectOne('/api/todos?page=2&pageSize=15').flush({
      items: [{ ...todo, id: 15 }, { ...todo, id: 16 }], page: 2, pageSize: 15, totalCount: 16,
    });
    expect(result?.map((item) => item.id)).toEqual(Array.from({ length: 16 }, (_, index) => index + 1));
  });

  it('loads a single task by its route id', () => {
    let result: Todo | undefined;
    service.getById(7).subscribe((item) => result = item);
    const request = http.expectOne('/api/todos/7');
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer jwt-test');
    request.flush(todo);
    expect(result).toEqual(todo);
  });

  it('creates and updates with the API contract', () => {
    service.create({ titulo: 'Revisar', descricao: 'Ler documentação', concluido: false, categoriaId: 2, prioridade: 'Alta', frequenciaRecorrencia: 'Semanal', duracaoRecorrencia: 4, dataInicioUtc: todo.dataInicioUtc, dataFimUtc: todo.dataFimUtc }).subscribe();
    const create = http.expectOne('/api/todos');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ titulo: 'Revisar', descricao: 'Ler documentação', concluido: false, categoriaId: 2, prioridade: 'Alta', frequenciaRecorrencia: 'Semanal', duracaoRecorrencia: 4, dataInicioUtc: todo.dataInicioUtc, dataFimUtc: todo.dataFimUtc });
    create.flush(todo);

    service.update(7, { titulo: 'Revisado', descricao: 'Texto atualizado', concluido: true, categoriaId: null, prioridade: 'Baixa', frequenciaRecorrencia: null, duracaoRecorrencia: null, dataInicioUtc: null, dataFimUtc: null }).subscribe();
    const update = http.expectOne('/api/todos/7');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ titulo: 'Revisado', descricao: 'Texto atualizado', concluido: true, categoriaId: null, prioridade: 'Baixa', frequenciaRecorrencia: null, duracaoRecorrencia: null, dataInicioUtc: null, dataFimUtc: null });
    update.flush({ ...todo, descricao: 'Revisado', concluido: true });
  });

  it('deletes recurring occurrence or whole series using the supported scopes', () => {
    service.delete(todo).subscribe();
    http.expectOne('/api/todos/7').flush(null, { status: 204, statusText: 'No Content' });

    const recurring = { ...todo, serieRecorrenciaId: 3 };
    service.delete(recurring, 'occurrence').subscribe();
    http.expectOne('/api/todos/7?scope=occurrence').flush(null, { status: 204, statusText: 'No Content' });

    service.delete(recurring, 'series').subscribe();
    http.expectOne('/api/todo-series/3').flush(null, { status: 204, statusText: 'No Content' });
  });
});
