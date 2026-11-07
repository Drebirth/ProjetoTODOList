export type RecurrenceFrequency = 'Diaria' | 'Semanal' | 'Mensal';
export type TodoPriority = 'Baixa' | 'Media' | 'Alta';

export interface Category {
  id: number;
  nome: string;
}

export interface Todo {
  id: number;
  titulo: string;
  descricao: string;
  categoriaId: number | null;
  categoriaNome: string | null;
  prioridade: TodoPriority;
  dataCriacao: string;
  dataInicioUtc: string | null;
  dataFimUtc: string | null;
  concluido: boolean;
  dataConclusaoUtc: string | null;
  dataPrevistaUtc: string | null;
  frequenciaRecorrencia: RecurrenceFrequency | null;
  duracaoRecorrencia: number | null;
  serieRecorrenciaId: number | null;
  numeroOcorrencia: number | null;
}

export interface TodoPage {
  items: Todo[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TodoMutation {
  titulo: string;
  descricao: string;
  concluido: boolean;
  categoriaId: number | null;
  prioridade: TodoPriority;
  frequenciaRecorrencia: RecurrenceFrequency | null;
  duracaoRecorrencia: number | null;
  dataInicioUtc: string | null;
  dataFimUtc: string | null;
}

export interface CategoryMutation {
  nome: string;
}

export interface AccountPreferences {
  timeZoneId: string;
}

export interface Credentials {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  tokenType: string;
  expiresAtUtc: string;
}

export interface RegisteredUser {
  id: number;
  username: string;
}
