/**
 * Atraso progressivo após falhas de login. É um freio de CORTESIA no navegador (evita martelar o
 * botão e dá feedback claro); a proteção de verdade é o rate limit do servidor (Supabase Auth e
 * a API) — quem ignora o front não é detido por isto.
 */
const KEY = 'iu-login-failures'
export const FREE_ATTEMPTS = 5
const BASE_SECONDS = 30
const MAX_SECONDS = 15 * 60

interface State {
  failures: number
  lockedUntil: number
}

function read(): State {
  try {
    const raw = sessionStorage.getItem(KEY)
    return raw ? (JSON.parse(raw) as State) : { failures: 0, lockedUntil: 0 }
  } catch {
    return { failures: 0, lockedUntil: 0 }
  }
}

function write(state: State) {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(state))
  } catch {
    // sem armazenamento: sem atraso local, o servidor continua limitando
  }
}

/** Atraso (s) após a n-ésima falha: 0 até 5 falhas; depois 30s, 60s, 120s… até 15 min. */
export function backoffSeconds(failures: number): number {
  if (failures < FREE_ATTEMPTS) return 0
  return Math.min(BASE_SECONDS * 2 ** (failures - FREE_ATTEMPTS), MAX_SECONDS)
}

export function recordLoginFailure(now = Date.now()): number {
  const state = read()
  const failures = state.failures + 1
  const seconds = backoffSeconds(failures)
  write({ failures, lockedUntil: seconds ? now + seconds * 1000 : 0 })
  return seconds
}

export function clearLoginFailures() {
  write({ failures: 0, lockedUntil: 0 })
}

/** Segundos restantes de bloqueio (0 = liberado). */
export function remainingLockSeconds(now = Date.now()): number {
  return Math.max(0, Math.ceil((read().lockedUntil - now) / 1000))
}
