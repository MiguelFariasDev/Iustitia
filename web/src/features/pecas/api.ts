import type { AxiosInstance } from 'axios'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import type {
  DocumentoAcervo,
  GerarPecaRequest,
  PecaDetalhe,
  StatusPeca,
  Submissao,
  TipoAnexo,
} from './types'

/** Envia um anexo ao acervo. Responde na hora; a indexação continua em segundo plano. */
export async function enviarAnexo(
  arquivo: File,
  tipoDocumento: TipoAnexo,
  client: AxiosInstance | undefined = apiClient
): Promise<{ id: string; status: string }> {
  if (!client) throw new Error('Cliente HTTP indisponível.')

  const form = new FormData()
  form.append('arquivo', arquivo)
  form.append('tipoDocumento', tipoDocumento)
  // Anexo de processo é material do caso: fica só para o escritório (padrão do backend).
  form.append('visibilidade', 'escritorio')

  const { data } = await client.post<{ id: string; status: string }>(endpoints.acervo.upload, form)

  return data
}

export async function getAnexo(
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<DocumentoAcervo> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  const { data } = await client.get<DocumentoAcervo>(endpoints.acervo.byId(id))

  return data
}

export async function removerAnexo(
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<void> {
  if (!client) throw new Error('Cliente HTTP indisponível.')
  await client.delete(endpoints.acervo.byId(id))
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
