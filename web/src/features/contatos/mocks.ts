import { daysFromNow } from '@/mocks/dates'
import { formatCpfOuCnpj } from './documento'
import type { ContatoFormValues } from './schemas'
import type { Contato, ContatosFilters } from './types'

/**
 * DADOS DE DEMONSTRAÇÃO. O módulo Contatos não existe no backend: este é um repositório EM
 * MEMÓRIA, que reimplementa as regras que a API terá (ordenação por nome, filtro por tipo,
 * campos vazios viram `null`). Recarregar a página volta ao estado inicial.
 *
 * TODO(contatos-backend): trocar por `endpoints.contatos` em api.ts quando o endpoint existir.
 */
function seed(): Contato[] {
  const base = (c: Omit<Contato, 'createdAt'> & { dias: number }): Contato => {
    const { dias, ...rest } = c
    return { ...rest, createdAt: daysFromNow(-dias) }
  }

  return [
    base({
      id: 'contato-001',
      nome: 'Carlos Eduardo Pereira',
      tipo: 'ParteContraria',
      documento: '529.982.247-25',
      email: 'carlos.pereira@exemplo.com.br',
      telefone: '(11) 98765-4321',
      observacoes: 'Réu na ação de cobrança. Representado pelo Dr. Almeida.',
      dias: 40,
    }),
    base({
      id: 'contato-002',
      nome: 'Helena Rocha Martins',
      tipo: 'Testemunha',
      documento: '111.444.777-35',
      email: null,
      telefone: '(21) 99876-1234',
      observacoes: 'Presenciou o acidente. Disponível à tarde.',
      dias: 25,
    }),
    base({
      id: 'contato-003',
      nome: 'Papelaria Central LTDA',
      tipo: 'Fornecedor',
      documento: '11.222.333/0001-81',
      email: 'vendas@papelariacentral.com.br',
      telefone: '(11) 3456-7890',
      observacoes: null,
      dias: 90,
    }),
    base({
      id: 'contato-004',
      nome: 'Construtora Horizonte S.A.',
      tipo: 'ParteContraria',
      documento: '45.723.174/0001-10',
      email: 'juridico@horizonte.com.br',
      telefone: '(31) 3322-1100',
      observacoes: 'Departamento jurídico atende de segunda a sexta.',
      dias: 12,
    }),
    base({
      id: 'contato-005',
      nome: 'Dr. Roberto Almeida',
      tipo: 'Outros',
      documento: null,
      email: 'roberto@almeidaadv.com.br',
      telefone: null,
      observacoes: 'Advogado da parte contrária — correspondente em Belo Horizonte.',
      dias: 5,
    }),
  ]
}

let store: Contato[] = seed()
let sequence = 100

export function resetContatosMock() {
  store = seed()
  sequence = 100
}

const byNome = (a: Contato, b: Contato) => a.nome.localeCompare(b.nome, 'pt-BR')

export function mockListContatos(filters: ContatosFilters = {}): Contato[] {
  return store.filter((c) => !filters.tipo || c.tipo === filters.tipo).sort(byNome)
}

export function mockGetContato(id: string): Contato | null {
  return store.find((c) => c.id === id) ?? null
}

/** Campos em branco viram `null`, como o payload da API faz (ver clientes/api.ts). */
function fromForm(values: ContatoFormValues) {
  const orNull = (v: string) => (v.trim() ? v.trim() : null)

  return {
    nome: values.nome.trim(),
    tipo: values.tipo,
    documento: values.documento ? formatCpfOuCnpj(values.documento) : null,
    email: orNull(values.email),
    telefone: orNull(values.telefone),
    observacoes: orNull(values.observacoes),
  }
}

export function mockCreateContato(values: ContatoFormValues): Contato {
  sequence += 1
  const created: Contato = {
    id: `contato-${sequence}`,
    ...fromForm(values),
    createdAt: new Date().toISOString(),
  }
  store = [...store, created]

  return created
}

export function mockUpdateContato(id: string, values: ContatoFormValues): Contato | null {
  const current = mockGetContato(id)
  if (!current) return null

  const updated: Contato = { ...current, ...fromForm(values) }
  store = store.map((c) => (c.id === id ? updated : c))

  return updated
}

export function mockDeleteContato(id: string): boolean {
  const existed = store.some((c) => c.id === id)
  store = store.filter((c) => c.id !== id)

  return existed
}
