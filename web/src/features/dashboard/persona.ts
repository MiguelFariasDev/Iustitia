import type { AccountType, UserRole } from '@/lib/auth/types'

/**
 * Qual painel a pessoa vê. São as quatro visões do protótipo v4, que não coincidem 1:1 com os
 * papéis do backend (Owner, Partner, Lawyer, Intern, Admin, Financial):
 *
 *  - Sócio:      Owner e Partner;
 *  - Advogado:   Lawyer — e também a conta Solo, que é um(a) advogado(a) trabalhando os próprios
 *                casos, não alguém que gerencia equipe;
 *  - Secretária: Admin e Intern (apoio administrativo);
 *  - Financeiro: Financial.
 *
 * Papel desconhecido cai na visão de menor privilégio. É escolha de INTERFACE: o que cada papel
 * pode ler continua sendo decidido pela API (ver lib/auth/access.ts e o ADR-068).
 */
export type Persona = 'socio' | 'advogado' | 'secretaria' | 'financeiro'

export function personaFor(role: UserRole | null | undefined, accountType?: AccountType | null) {
  if (accountType === 'Solo') return 'advogado' satisfies Persona
  switch (role) {
    case 'Owner':
    case 'Partner':
      return 'socio' satisfies Persona
    case 'Lawyer':
      return 'advogado' satisfies Persona
    case 'Financial':
      return 'financeiro' satisfies Persona
    default:
      return 'secretaria' satisfies Persona
  }
}
