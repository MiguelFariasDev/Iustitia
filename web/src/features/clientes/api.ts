import type { AxiosInstance } from 'axios'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { onlyDigits } from '@/lib/validation/documento'
import { toCliente, toClienteListItem, type ClienteDto, type ListClientesDto } from './contract'
import type { ClienteFormValues } from './schemas'
import type { Cliente, ClientesFilters, ClientesPage } from './types'

/**
 * Clientes não têm modo mock: a tela nasceu depois do endpoint existir. Onde o mock ainda
 * é necessário (agenda, alertas, financeiro), ele continua sinalizado por env.useMocks.
 */
export async function listClientes(
  filters: ClientesFilters = {},
  client: AxiosInstance = apiClient
): Promise<ClientesPage> {
  const { data } = await client.get<ListClientesDto>(endpoints.clientes.list, {
    params: {
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
      tipo: filters.tipo ?? undefined,
      apenasAtivos: filters.apenasAtivos ?? undefined,
      busca: filters.busca || undefined,
    },
  })

  return { ...data, items: data.items.map(toClienteListItem) }
}

export async function getClienteById(
  id: string,
  client: AxiosInstance = apiClient
): Promise<Cliente> {
  const { data } = await client.get<ClienteDto>(endpoints.clientes.byId(id))

  return toCliente(data)
}

export async function createCliente(values: ClienteFormValues): Promise<Cliente> {
  const { data } = await apiClient.post<ClienteDto>(
    endpoints.clientes.list,
    toCreatePayload(values)
  )

  return toCliente(data)
}

export async function updateCliente(id: string, values: ClienteFormValues): Promise<void> {
  await apiClient.put(endpoints.clientes.byId(id), toUpdatePayload(values))
}

export async function deactivateCliente(id: string): Promise<void> {
  await apiClient.delete(endpoints.clientes.byId(id))
}

export async function reactivateCliente(id: string): Promise<void> {
  await apiClient.post(endpoints.clientes.reactivate(id))
}

/**
 * Campos em branco viram `null`, não string vazia: no backend `""` e ausente são coisas
 * diferentes — `""` passaria pela validação de "opcional" e seria persistido como um
 * telefone vazio em vez de nenhum telefone.
 *
 * Documento e telefone são enviados só com dígitos; a máscara é da apresentação.
 */
/** Campos comuns a criação e edição. */
function toCommonPayload(values: ClienteFormValues) {
  return {
    nome: values.nome,
    dataNascimento: nullIfBlank(values.dataNascimento),
    observacoes: nullIfBlank(values.observacoes),
    contato: {
      email: nullIfBlank(values.email),
      telefone: values.telefone ? onlyDigits(values.telefone) : null,
    },
    endereco: {
      logradouro: nullIfBlank(values.logradouro),
      numero: nullIfBlank(values.numero),
      complemento: nullIfBlank(values.complemento),
      bairro: nullIfBlank(values.bairro),
      cidade: nullIfBlank(values.cidade),
      uf: nullIfBlank(values.uf),
      cep: values.cep ? onlyDigits(values.cep) : null,
    },
  }
}

function toCreatePayload(values: ClienteFormValues) {
  return {
    ...toCommonPayload(values),
    tipo: values.tipo,
    documento: onlyDigits(values.documento),
  }
}

/**
 * A edição NÃO envia tipo nem documento — são a identidade do cliente e o backend os recusa
 * (ver Cliente.Update). Por isso o comum é montado à parte, em vez de a edição remover
 * campos do payload de criação: assim um campo novo precisa ser adicionado conscientemente
 * a cada um, em vez de vazar para o update sem ninguém notar.
 */
function toUpdatePayload(values: ClienteFormValues) {
  return toCommonPayload(values)
}

function nullIfBlank(value: string | undefined) {
  return value && value.trim() ? value.trim() : null
}
