import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { CategoriesApiService } from './categories-api.service';
import { SessionService } from './session.service';

describe('CategoriesApiService', () => {
  let service: CategoriesApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), CategoriesApiService, SessionService],
    });
    service = TestBed.inject(CategoriesApiService);
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(SessionService).setToken('category-token');
  });

  afterEach(() => http.verify());

  it('lists categories with the session bearer token', () => {
    service.getAll().subscribe();
    const request = http.expectOne('/api/categories');
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer category-token');
    request.flush([{ id: 1, nome: 'Estudos' }]);
  });

  it('creates, renames, and deletes through the documented routes', () => {
    service.create('Leitura').subscribe();
    const create = http.expectOne('/api/categories');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ nome: 'Leitura' });
    create.flush({ id: 5, nome: 'Leitura' });

    service.update(5, 'Livros').subscribe();
    const update = http.expectOne('/api/categories/5');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ nome: 'Livros' });
    update.flush({ id: 5, nome: 'Livros' });

    service.delete(5).subscribe();
    const remove = http.expectOne('/api/categories/5');
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null, { status: 204, statusText: 'No Content' });
  });
});
