import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { LoginPage } from './pages/login/login.page';
import { RegisterPage } from './pages/register/register.page';
import { TodoFormPage } from './pages/todo-form/todo-form.page';
import { TodosPage } from './pages/todos/todos.page';

export const routes: Routes = [
  { path: 'login', component: LoginPage },
  { path: 'register', component: RegisterPage },
  { path: 'todos/new', component: TodoFormPage, canActivate: [authGuard] },
  { path: 'todos/:id/edit', component: TodoFormPage, canActivate: [authGuard] },
  { path: 'todos', component: TodosPage, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'todos' },
  { path: '**', redirectTo: 'login' },
];
