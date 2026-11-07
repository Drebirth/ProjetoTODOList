import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { CategoriesApiService } from '../../core/categories-api.service';
import { PreferencesApiService } from '../../core/preferences-api.service';
import { SessionService } from '../../core/session.service';
import { convertLocalWindow, formatUtcWindowInput, maxRecurrenceDuration, validateRecurrenceDuration } from '../../core/scheduling-time';
import { AccountPreferences, Category, RecurrenceFrequency, Todo, TodoMutation, TodoPriority } from '../../models/todo.models';
import { TodoApiService } from '../../core/todo-api.service';

@Component({ selector: 'app-todo-form-page', imports: [FormsModule, RouterLink], templateUrl: './todo-form.page.html' })
export class TodoFormPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly session = inject(SessionService);
  private readonly preferencesApi = inject(PreferencesApiService);
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly todoApi = inject(TodoApiService);
  readonly categories = signal<Category[]>([]);
  readonly preference = signal<AccountPreferences>({ timeZoneId: 'America/Fortaleza' });
  readonly ready = signal(false);
  readonly editMode = signal(false);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal('');
  readonly editingTodo = signal<Todo | null>(null);
  readonly priorities: { value: TodoPriority; label: string }[] = [
    { value: 'Baixa', label: 'Baixa' }, { value: 'Media', label: 'Média' }, { value: 'Alta', label: 'Alta' },
  ];
  readonly frequencies: { value: RecurrenceFrequency; label: string }[] = [
    { value: 'Diaria', label: 'Diária' }, { value: 'Semanal', label: 'Semanal' }, { value: 'Mensal', label: 'Mensal' },
  ];
  get recurrenceMaximum(): number { return maxRecurrenceDuration(this.recurrenceFrequency); }

  title = '';
  description = '';
  categoryId: number | null = null;
  priority: TodoPriority = 'Media';
  recurrenceEnabled = false;
  recurrenceFrequency: RecurrenceFrequency = 'Semanal';
  recurrenceDuration = 2;
  windowStartLocal = '';
  windowEndLocal = '';

  constructor() {
    afterNextRender(() => {
      this.session.restore();
      if (!this.session.token()) {
        void this.router.navigate(['/login']);
        return;
      }
      this.loadFormData();
    });
  }

  loadFormData(): void {
    this.loading.set(true);
    this.error.set('');
    const idValue = this.route.snapshot.paramMap.get('id');
    const id = idValue === null ? null : Number(idValue);
    this.editMode.set(idValue !== null);
    forkJoin({ preference: this.preferencesApi.get(), categories: this.categoriesApi.getAll() }).subscribe({
      next: ({ preference, categories }) => {
        this.preference.set(preference);
        this.categories.set(categories);
        this.ready.set(true);
        if (id === null) {
          this.loading.set(false);
          return;
        }
        if (!Number.isInteger(id) || id < 1) {
          this.loading.set(false);
          this.error.set('HTTP 404: A tarefa não foi encontrada.');
          return;
        }
        this.todoApi.getById(id).subscribe({
          next: (todo) => { this.populate(todo); this.loading.set(false); },
          error: (error: unknown) => {
            this.loading.set(false);
            this.error.set(this.errorMessage(error, 'Não foi possível carregar a tarefa.'));
            if (error instanceof HttpErrorResponse && error.status === 404) {
              void this.router.navigate(['/todos'], { queryParams: { warning: 'task-not-found' } });
            }
          },
        });
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(this.errorMessage(error, 'Não foi possível carregar as preferências e categorias.'));
      },
    });
  }

  submit(): void {
    this.error.set('');
    if (!this.ready()) { this.error.set('Carregue as preferências e categorias antes de salvar.'); return; }
    const title = this.title.trim();
    const description = this.description.trim();
    if (!title || title.length > 200 || !description || description.length > 200) {
      this.error.set('Título e descrição são obrigatórios e devem ter até 200 caracteres.');
      return;
    }
    if (this.recurrenceEnabled) {
      const recurrenceError = validateRecurrenceDuration(this.recurrenceFrequency, Number(this.recurrenceDuration));
      if (recurrenceError) { this.error.set(recurrenceError); return; }
    }
    let window: { dataInicioUtc: string | null; dataFimUtc: string | null };
    try { window = convertLocalWindow(this.windowStartLocal, this.windowEndLocal, this.preference().timeZoneId); }
    catch (error) { this.error.set(error instanceof Error ? error.message : 'Confira os horários informados.'); return; }
    const existing = this.editingTodo();
    const mutation: TodoMutation = {
      titulo: title,
      descricao: description,
      concluido: existing?.concluido ?? false,
      categoriaId: this.categoryId === null ? null : Number(this.categoryId),
      prioridade: this.priority,
      frequenciaRecorrencia: this.recurrenceEnabled ? this.recurrenceFrequency : null,
      duracaoRecorrencia: this.recurrenceEnabled ? Number(this.recurrenceDuration) : null,
      dataInicioUtc: window.dataInicioUtc,
      dataFimUtc: window.dataFimUtc,
    };
    this.submitting.set(true);
    const request = existing ? this.todoApi.update(existing.id, mutation) : this.todoApi.create(mutation);
    request.subscribe({
      next: () => { this.submitting.set(false); void this.router.navigate(['/todos']); },
      error: (error: unknown) => { this.submitting.set(false); this.error.set(this.errorMessage(error, 'Não foi possível salvar a tarefa.')); },
    });
  }

  cancel(): void { void this.router.navigate(['/todos']); }

  private populate(todo: Todo): void {
    this.editingTodo.set(todo);
    this.title = todo.titulo;
    this.description = todo.descricao;
    this.categoryId = todo.categoriaId;
    this.priority = todo.prioridade;
    this.recurrenceEnabled = todo.frequenciaRecorrencia !== null;
    this.recurrenceFrequency = todo.frequenciaRecorrencia ?? 'Semanal';
    this.recurrenceDuration = todo.duracaoRecorrencia ?? 2;
    this.windowStartLocal = todo.dataInicioUtc ? formatUtcWindowInput(todo.dataInicioUtc, this.preference().timeZoneId) : '';
    this.windowEndLocal = todo.dataFimUtc ? formatUtcWindowInput(todo.dataFimUtc, this.preference().timeZoneId) : '';
  }

  private errorMessage(error: unknown, fallback: string): string {
    if (!(error instanceof HttpErrorResponse)) return fallback;
    const body = error.error as { title?: unknown; detail?: unknown } | null;
    const message = [body?.title, body?.detail].filter((value): value is string => typeof value === 'string').join(' ');
    if (error.status === 404) return `HTTP 404: ${message || 'A tarefa não foi encontrada.'}`;
    return `HTTP ${error.status}: ${message || fallback}`;
  }
}
