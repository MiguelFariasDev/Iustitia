/** RFC 7807 com as extensions emitidas pela API (ResultExtensions / ExceptionHandlingMiddleware). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errorType?: string
  errorCode?: string
  errorGroup?: string
  correlationId?: string
  timestamp?: string
  /** Erros de validação (FluentValidation) por campo. */
  errors?: Record<string, string[]>
}

/** Espelha BuildingBlocks.Application.PagedList<T>. */
export interface PagedList<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export interface PageRequest {
  page?: number
  pageSize?: number
  search?: string
}
