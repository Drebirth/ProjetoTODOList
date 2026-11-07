import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CategoriesApiService } from '../../core/categories-api.service';
import { PreferencesApiService } from '../../core/preferences-api.service';
import { SessionService } from '../../core/session.service';
import { AccountPreferences, Category, Todo } from '../../models/todo.models';
import { TodoApiService } from '../../core/todo-api.service';
import { formatWindowInTimeZone } from '../../core/scheduling-time';

type TodoFilter = 'all' | 'open' | 'completed';

@Component({ selector: 'app-todos-page', imports: [FormsModule], templateUrl: './todos.page.html' })
export class TodosPage {
  private todosLoadRevision = 0;
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly preferencesApi = inject(PreferencesApiService);
  private readonly todoApi = inject(TodoApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly session = inject(SessionService);
  readonly todos = signal<Todo[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly preference = signal<AccountPreferences>({ timeZoneId: 'America/Fortaleza' });
  readonly preferencesReady = signal(false);
  readonly preferenceLoading = signal(false);
  readonly preferenceError = signal('');
  readonly settingsOpen = signal(false);
  readonly preferenceSaving = signal(false);
  readonly settingsError = signal('');
  readonly filter = signal<TodoFilter>('all');
  readonly categoryFilterId = signal<number | 'all'>('all');
  readonly loading = signal(false);
  readonly submitting = signal(false);
  readonly pageError = signal('');
  readonly routeWarning = signal('');
  readonly categoryManagerOpen = signal(false);
  readonly categoryLoading = signal(false);
  readonly categorySubmitting = signal(false);
  readonly categoryError = signal('');
  readonly categoryNotice = signal('');
  readonly editingCategory = signal<Category | null>(null);
  readonly visibleTodos = computed(() => this.todos().filter((todo) => {
    const statusMatches = this.filter() === 'all' || (this.filter() === 'open' ? !todo.concluido : todo.concluido);
    const selectedCategory = this.categoryFilterId();
    const categoryMatches = selectedCategory === 'all' || todo.categoriaId === selectedCategory;
    return statusMatches && categoryMatches;
  }));
  readonly openCount = computed(() => this.todos().filter((todo) => !todo.concluido).length);
  readonly completedCount = computed(() => this.todos().filter((todo) => todo.concluido).length);
  categoryName = '';
  preferenceTimeZoneId = 'America/Fortaleza';

  constructor() {
    if (this.route.snapshot.queryParamMap.get('warning') === 'task-not-found') {
      this.routeWarning.set('A tarefa não foi encontrada. A lista atual está atualizada.');
    }
    afterNextRender(() => {
      this.session.restore();
      if (this.session.token()) this.loadWorkspace();
    });
  }

  logout(): void {
    this.todosLoadRevision++;
    this.session.clear();
    this.todos.set([]);
    this.categories.set([]);
    this.pageError.set('');
    
    this.preferencesReady.set(false);
    this.preferenceError.set('');
    this.settingsOpen.set(false);
    void this.router.navigate(['/login']);
  }

  dismissRouteWarning(): void { this.routeWarning.set(''); }

  loadWorkspace(): void {
    this.preferenceLoading.set(true);
    this.preferenceError.set('');
    this.preferencesApi.get().subscribe({
      next: (preference) => {
        this.preference.set(preference);
        this.preferenceTimeZoneId = preference.timeZoneId;
        this.preferencesReady.set(true);
        this.preferenceLoading.set(false);
        this.loadTodos();
        this.loadCategories();
      },
      error: (error: unknown) => {
        this.preferenceLoading.set(false);
        this.preferenceError.set(this.httpErrorMessage(error, 'Não foi possível carregar o fuso horário da conta.'));
      },
    });
  }

  openSettings(): void {
    this.preferenceTimeZoneId = this.preference().timeZoneId;
    this.settingsError.set('');
    this.settingsOpen.set(true);
  }

  closeSettings(): void {
    if (!this.preferenceSaving()) this.settingsOpen.set(false);
  }

  savePreferences(): void {
    const timeZoneId = this.preferenceTimeZoneId.trim();
    if (!timeZoneId) {
      this.settingsError.set('Informe um identificador IANA de fuso horário.');
      return;
    }
    this.preferenceSaving.set(true);
    this.settingsError.set('');
    this.preferencesApi.update(timeZoneId).subscribe({
      next: (preference) => {
        this.preference.set(preference);
        this.preferenceTimeZoneId = preference.timeZoneId;
        this.preferencesReady.set(true);
        this.preferenceSaving.set(false);
        this.settingsOpen.set(false);
      },
      error: (error: unknown) => {
        this.preferenceSaving.set(false);
        this.settingsError.set(this.httpErrorMessage(error, 'Não foi possível atualizar o fuso horário.'));
      },
    });
  }

  loadTodos(): void {
    const loadRevision = ++this.todosLoadRevision;
    this.loading.set(true);
    this.todoApi.getAll().subscribe({
      next: (items) => {
        if (loadRevision !== this.todosLoadRevision) return;
        this.todos.set(items);
        this.pageError.set('');
        this.loading.set(false);
      },
      error: (error: unknown) => {
        if (loadRevision !== this.todosLoadRevision) return;
        this.loading.set(false);
        if (!this.session.token()) {
          this.todos.set([]);
          this.pageError.set('Sua sessão expirou. Entre novamente para carregar suas tarefas.');
        } else this.pageError.set(this.apiMessage(error));
      },
    });
  }

  loadCategories(): void {
    this.categoryLoading.set(true);
    this.categoryError.set('');
    this.categoriesApi.getAll().subscribe({
      next: (categories) => { this.categories.set(categories); this.categoryLoading.set(false); },
      error: (error: unknown) => {
        this.categoryLoading.set(false);
        this.categoryError.set(this.categoryApiMessage(error));
      },
    });
  }

  setFilter(filter: TodoFilter): void { this.filter.set(filter); }
  setCategoryFilter(categoryId: number | 'all'): void { this.categoryFilterId.set(categoryId); }

  createTodo(): void { void this.router.navigate(['/todos/new']); }
  editTodo(todo: Todo): void { void this.router.navigate(['/todos', todo.id, 'edit']); }

  toggleCompleted(todo: Todo): void {
    this.todoApi.update(todo.id, {
      titulo: todo.titulo, descricao: todo.descricao, concluido: !todo.concluido,
      categoriaId: todo.categoriaId, prioridade: todo.prioridade,
      frequenciaRecorrencia: todo.frequenciaRecorrencia, duracaoRecorrencia: todo.duracaoRecorrencia,
      dataInicioUtc: todo.dataInicioUtc, dataFimUtc: todo.dataFimUtc,
    }).subscribe({
      next: () => this.loadTodos(),
      error: (error: unknown) => this.pageError.set(this.apiMessage(error)),
    });
  }

  openCategories(): void {
    this.categoryManagerOpen.set(true);
    this.categoryError.set('');
    this.categoryNotice.set('');
  }

  closeCategories(): void {
    if (!this.categorySubmitting()) this.categoryManagerOpen.set(false);
  }

  startCategoryEdit(category: Category): void {
    this.editingCategory.set(category);
    this.categoryName = category.nome;
    this.categoryError.set('');
  }

  resetCategoryForm(): void {
    this.editingCategory.set(null);
    this.categoryName = '';
    this.categoryError.set('');
  }

  saveCategory(): void {
    const name = this.categoryName.trim();
    if (!name) return;
    const editing = this.editingCategory();
    this.categorySubmitting.set(true);
    this.categoryError.set('');
    this.categoryNotice.set('');
    const request = editing ? this.categoriesApi.update(editing.id, name) : this.categoriesApi.create(name);
    request.subscribe({
      next: (category) => {
        this.categories.update((items) => editing
          ? items.map((item) => item.id === category.id ? category : item)
          : [...items, category]);
        if (editing) {
          this.todos.update((items) => items.map((todo) => todo.categoriaId === category.id
            ? { ...todo, categoriaNome: category.nome }
            : todo));
        }
        this.categorySubmitting.set(false);
        this.categoryNotice.set(editing ? 'Categoria renomeada.' : 'Categoria criada.');
        this.resetCategoryForm();
      },
      error: (error: unknown) => {
        this.categorySubmitting.set(false);
        this.categoryError.set(this.categoryApiMessage(error));
      },
    });
  }

  deleteCategory(category: Category): void {
    this.categorySubmitting.set(true);
    this.categoryError.set('');
    this.categoryNotice.set('');
    this.categoriesApi.delete(category.id).subscribe({
      next: () => {
        this.categories.update((items) => items.filter((item) => item.id !== category.id));
        this.todos.update((items) => items.map((todo) => todo.categoriaId === category.id
          ? { ...todo, categoriaId: null, categoriaNome: null }
          : todo));
        if (this.categoryFilterId() === category.id) this.categoryFilterId.set('all');
        this.categorySubmitting.set(false);
        this.categoryNotice.set('Categoria excluída.');
      },
      error: (error: unknown) => {
        this.categorySubmitting.set(false);
        this.categoryError.set(this.categoryApiMessage(error));
      },
    });
  }

  deleteTodo(todo: Todo): void {
    if (this.submitting()) return;
    const scope = todo.serieRecorrenciaId !== null ? 'occurrence' : undefined;
    this.submitting.set(true);
    this.pageError.set('');
    this.todoApi.delete(todo, scope).subscribe({
      next: () => {
        this.todos.update((items) => items.filter((item) => item.id !== todo.id));
        this.submitting.set(false);
        this.loadTodos();
      },
      error: (error: unknown) => { this.submitting.set(false); this.pageError.set(this.apiMessage(error)); },
    });
  }

  formatCreatedAt(date: string): string {
    return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone: this.preference().timeZoneId }).format(new Date(date));
  }

  categoryNameFor(todo: Todo): string | null {
    return todo.categoriaNome ?? this.categories().find((category) => category.id === todo.categoriaId)?.nome ?? null;
  }

  formatScheduledWindow(todo: Todo): string | null {
    if (!this.preferencesReady() || !todo.dataInicioUtc || !todo.dataFimUtc) return null;
    const zone = this.preference().timeZoneId;
    return `${formatWindowInTimeZone(todo.dataInicioUtc, zone)} – ${formatWindowInTimeZone(todo.dataFimUtc, zone)}`;
  }

  private apiMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 400) return 'Confira a descrição e as opções de recorrência.';
    if (error instanceof HttpErrorResponse && error.status === 404) return 'A tarefa não foi encontrada. Atualize a lista e tente novamente.';
    if (error instanceof HttpErrorResponse && error.status === 401) return 'Sua sessão expirou. Entre novamente para continuar.';
    return 'Não foi possível concluir a operação. Tente novamente.';
  }

  private categoryApiMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 409) return 'Esta categoria está em uso por tarefas ou séries e não pode ser excluída.';
    if (error instanceof HttpErrorResponse && error.status === 404) return 'A categoria não foi encontrada. Atualize a lista e tente novamente.';
    if (error instanceof HttpErrorResponse && error.status === 400) return 'Informe um nome para a categoria.';
    if (error instanceof HttpErrorResponse && error.status === 401) return 'Sua sessão expirou. Entre novamente para continuar.';
    return 'Não foi possível concluir a operação de categoria. Tente novamente.';
  }

  private httpErrorMessage(error: unknown, fallback: string): string {
    if (!(error instanceof HttpErrorResponse)) return fallback;
    const body = error.error as { title?: unknown; detail?: unknown; errors?: Record<string, unknown> } | null;
    const messages = [body?.title, body?.detail, ...Object.values(body?.errors ?? {}).flatMap((value) => Array.isArray(value) ? value : [value])]
      .filter((value): value is string => typeof value === 'string' && value.trim().length > 0);
    return `HTTP ${error.status}: ${messages.join(' ') || fallback}`;
  }
}
