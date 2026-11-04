export type SearchResultKind = 'process' | 'client' | 'task' | 'publication' | 'document'

export interface SearchResult {
  id: string
  kind: SearchResultKind
  title: string
  subtitle: string
}

export const searchGroupLabels: Record<SearchResultKind, string> = {
  process: 'Processos',
  client: 'Clientes',
  task: 'Tarefas',
  publication: 'Publicações',
  document: 'Documentos',
}
