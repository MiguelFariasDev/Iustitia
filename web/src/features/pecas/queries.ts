import { queryOptions } from '@tanstack/react-query'
import { listAnexos, listCasos } from './api'
import { anexoTerminado } from './types'

const POLLING_MS = 3000

export const casosQuery = () =>
  queryOptions({ queryKey: ['casos', 'list'], queryFn: () => listCasos() })

export const anexosQuery = (casoId: string) =>
  queryOptions({
    queryKey: ['casos', casoId, 'documentos'],
    queryFn: () => listAnexos(casoId),
    enabled: casoId !== '',
    // Acompanha enquanto algum anexo ainda está sendo lido ou indexado.
    refetchInterval: (consulta) =>
      consulta.state.data?.some((anexo) => !anexoTerminado(anexo.status)) ? POLLING_MS : false,
  })
