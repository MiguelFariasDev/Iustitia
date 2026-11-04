import type { SearchResult } from './types'

/**
 * Não há `/search` no backend — busca global é um módulo próprio, ainda não construído.
 * Devolve vazio em vez de um índice fabricado: um resultado inventado numa busca leva a
 * abrir um processo que não existe.
 *
 * Cada listagem já tem a sua própria busca real contra a API: clientes por nome ou
 * documento, processos por número CNJ ou parte contrária.
 */
export async function searchGlobal(_term: string): Promise<SearchResult[]> {
  return []
}
