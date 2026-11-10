'use client'

import { useSyncExternalStore } from 'react'

const MINUTO = 60_000

function subscribe(onChange: () => void) {
  // Reavalia a cada minuto: o "hoje" do painel vira à meia-noite sem recarregar a página.
  const id = setInterval(onChange, MINUTO)

  return () => clearInterval(id)
}

/** Arredondado ao minuto: o valor só muda quando muda de fato (evita laço do useSyncExternalStore). */
const getSnapshot = () => Math.floor(Date.now() / MINUTO) * MINUTO

/**
 * "Agora" sem divergência de hidratação: no servidor devolve `null` (o fuso e a hora do servidor
 * não são os da pessoa), e o cliente passa a informar a hora local assim que hidrata. Quem usa
 * deve tratar `null` como "ainda não sei" — saudação e data aparecem logo depois, sem alerta de
 * conteúdo diferente do HTML do servidor.
 */
export function useNow(): Date | null {
  const minuto = useSyncExternalStore(subscribe, getSnapshot, () => null)

  return minuto === null ? null : new Date(minuto)
}
