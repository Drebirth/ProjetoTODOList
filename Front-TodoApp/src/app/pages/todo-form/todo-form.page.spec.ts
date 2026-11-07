import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { TodoFormPage } from './todo-form.page';
import { routes } from '../../app.routes';
import { authInterceptor } from '../../core/auth.interceptor';
import { SessionService } from '../../core/session.service';

describe('TodoFormPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TodoFormPage],
      providers: [provideRouter(routes), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), SessionService],
    }).compileComponents();
  });

  it('loads preferences and categories before allowing a create request', () => {
    TestBed.overrideProvider(ActivatedRoute, { useValue: { snapshot: { paramMap: { get: () => null } } } });
    TestBed.inject(SessionService).setToken('form-token');
    const fixture = TestBed.createComponent(TodoFormPage);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    const preference = http.expectOne('/api/preferences');
    expect(preference.request.headers.get('Authorization')).toBe('Bearer form-token');
    http.expectOne('/api/categories').flush([{ id: 3, nome: 'Casa' }]);
    preference.flush({ timeZoneId: 'America/Fortaleza' });
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('#todoCategory')).not.toBeNull();
    expect(root.querySelector('.material-symbols-outlined')).toBeNull();
    expect(root.textContent).toContain('Lista de tarefas');
    expect(root.textContent).toContain('Criar tarefa');
    expect(root.textContent).toContain('Título');
    expect(root.textContent).toContain('Descrição');
    expect(root.textContent).not.toContain('â€¦');
    expect(root.textContent).not.toContain('\u00c3');
    fixture.componentInstance.title = 'Organizar';
    fixture.componentInstance.description = 'Separar documentos';
    fixture.componentInstance.windowStartLocal = '2026-11-09T09:00';
    fixture.componentInstance.windowEndLocal = '2026-11-09T10:00';
    fixture.componentInstance.submit();
    const create = http.expectOne('/api/todos');
    expect(create.request.body.dataInicioUtc).toBe('2026-11-09T12:00:00.000Z');
    expect(create.request.body.dataFimUtc).toBe('2026-11-09T13:00:00.000Z');
    create.flush({ id: 73 });
  });

  it('redirects to the dashboard with a visible-warning query after an edit target returns 404', async () => {
    TestBed.overrideProvider(ActivatedRoute, { useValue: { snapshot: { paramMap: { get: () => '74' } } } });
    TestBed.inject(SessionService).setToken('form-token');
    const fixture = TestBed.createComponent(TodoFormPage);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/preferences').flush({ timeZoneId: 'America/Fortaleza' });
    http.expectOne('/api/categories').flush([]);
    const getTodo = http.expectOne('/api/todos/74');
    expect(getTodo.request.method).toBe('GET');
    getTodo.flush(null, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(TestBed.inject(Router).url).toContain('/todos?warning=task-not-found');
    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('404');
    expect(fixture.nativeElement.querySelector('#title')).toBeNull();
  });

  it('loads an existing task in the account zone and submits an edit using UTC instants', () => {
    TestBed.overrideProvider(ActivatedRoute, { useValue: { snapshot: { paramMap: { get: () => '75' } } } });
    TestBed.inject(SessionService).setToken('form-token');
    const fixture = TestBed.createComponent(TodoFormPage);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/preferences').flush({ timeZoneId: 'America/Fortaleza' });
    http.expectOne('/api/categories').flush([{ id: 3, nome: 'Casa' }]);
    http.expectOne('/api/todos/75').flush({ id: 75, titulo: 'Organizar', descricao: 'Documentos', categoriaId: 3, categoriaNome: 'Casa', prioridade: 'Alta', dataCriacao: '2026-11-07T12:00:00Z', dataInicioUtc: '2026-11-09T12:00:00Z', dataFimUtc: '2026-11-09T13:00:00Z', concluido: false, dataConclusaoUtc: null, dataPrevistaUtc: null, frequenciaRecorrencia: 'Semanal', duracaoRecorrencia: 4, serieRecorrenciaId: 77, numeroOcorrencia: 2 });
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const seriesNotice = root.querySelector('[role="note"]')?.textContent ?? '';
    expect(seriesNotice).toContain('Alterações de título, descrição, categoria, prioridade e recorrência');
    expect(seriesNotice).toContain('âncora #2');
    expect(seriesNotice).toContain('as já geradas serão mantidas');
    expect(root.querySelector<HTMLInputElement>('#windowStart')?.value).toBe('2026-11-09T09:00');
    expect(root.querySelector<HTMLInputElement>('#windowEnd')?.value).toBe('2026-11-09T10:00');
    fixture.componentInstance.windowStartLocal = '2026-11-09T11:00';
    fixture.componentInstance.windowEndLocal = '2026-11-09T12:00';
    fixture.componentInstance.submit();
    const update = http.expectOne('/api/todos/75');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body.dataInicioUtc).toBe('2026-11-09T14:00:00.000Z');
    expect(update.request.body.dataFimUtc).toBe('2026-11-09T15:00:00.000Z');
    update.flush({ id: 75 });
  });
});
