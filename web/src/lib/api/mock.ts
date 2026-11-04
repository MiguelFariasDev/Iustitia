/** Simula latência de rede para mocks (exercita Suspense/skeletons como em produção). */
export function mockResponse<T>(data: T, latencyMs = 400): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(structuredClone(data)), latencyMs))
}
