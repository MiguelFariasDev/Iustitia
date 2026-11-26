import type { AxiosInstance } from 'axios'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import type {
  AnexoCaso,
  Caso,
  CitacaoPeca,
  CriarCasoRequest,
  DecisaoCitacao,
  GerarPecaRequest,
  PecaDetalhe,
  StatusPeca,
  Submissao,
} from './types'

/** O upload pode ser grande; o limite padrão do cliente (30 s) é de requisições comuns. */
const UPLOAD_TIMEOUT_MS = 5 * 60_000

export async function listCasos(client: AxiosInstance | undefined = apiClient): Promise<Caso[]> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  const { data } = await client.get<Caso[]>(endpoints.casos.list, { params: { tamanho: 100 } })

  return data
}

export async function criarCaso(
  request: CriarCasoRequest,
  client: AxiosInstance | undefined = apiClient
): Promise<Caso> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  const { data } = await client.post<Caso>(endpoints.casos.list, request)

  return data
}

export async function listAnexos(
  casoId: string,
  client: AxiosInstance | undefined = apiClient
): Promise<AnexoCaso[]> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  const { data } = await client.get<AnexoCaso[]>(endpoints.casos.documentos(casoId))

  return data
}

/** Anexa ao caso. Responde na hora; leitura, OCR e indexação continuam em segundo plano. */
export async function enviarAnexo(
  casoId: string,
  arquivo: File,
  client: AxiosInstance | undefined = apiClient
): Promise<{ id: string; status: string }> {
  if (!client) throw new Error('Cliente HTTP indisponível.')

  const form = new FormData()
  form.append('arquivo', arquivo)
  // O cliente tem application/json por padrão, e com ele o axios serializa o FormData como JSON.
  const { data } = await client.post<{ id: string; status: string }>(
    endpoints.casos.documentos(casoId),
    form,
    { headers: { 'Content-Type': 'multipart/form-data' }, timeout: UPLOAD_TIMEOUT_MS }
  )

  return data
}

export async function removerAnexo(
  casoId: string,
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<void> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  await client.delete(endpoints.casos.documento(casoId, id))
}

/** O texto que o sistema extraiu, para conferir contra o original. */
export async function getTextoAnexo(
  casoId: string,
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<string> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  const { data } = await client.get<string>(endpoints.casos.documentoMarkdown(casoId, id), {
    responseType: 'text',
  })

  return data
}

export async function confirmarRevisaoAnexo(
  casoId: string,
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<void> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  await client.post(endpoints.casos.revisarDocumento(casoId, id))
}

export async function submeterPeca(
  request: GerarPecaRequest,
  client: AxiosInstance | undefined = apiClient
): Promise<Submissao> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  const { data } = await client.post<Submissao>(endpoints.pecas.gerar, request)

  return data
}

export async function getStatusPeca(
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<StatusPeca> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  const { data } = await client.get<StatusPeca>(endpoints.pecas.status(id))

  return data
}

export async function getPeca(
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<PecaDetalhe> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  const { data } = await client.get<PecaDetalhe>(endpoints.pecas.byId(id))

  return data
}

export async function cancelarPeca(
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<void> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  await client.post(endpoints.pecas.cancelar(id))
}

export async function decidirCitacao(
  pecaId: string,
  citacaoId: string,
  decisao: Exclude<DecisaoCitacao, 'pendente'>,
  justificativa: string,
  client: AxiosInstance | undefined = apiClient
): Promise<CitacaoPeca> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  const { data } = await client.put<CitacaoPeca>(endpoints.pecas.citacao(pecaId, citacaoId), {
    decisao,
    justificativa,
  })

  return data
}

export async function concluirRevisao(
  pecaId: string,
  textoFinal: string,
  client: AxiosInstance | undefined = apiClient
): Promise<void> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  await client.post(endpoints.pecas.revisao(pecaId), { declaroRevisao: true, textoFinal })
}

export async function exportarPeca(
  pecaId: string,
  formato: 'docx' | 'md',
  client: AxiosInstance | undefined = apiClient
): Promise<Blob> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  const { data } = await client.get<Blob>(endpoints.pecas.exportar(pecaId), {
    params: { formato },
    responseType: 'blob',
  })

  return data
}
