'use client'

import { useQuery } from '@tanstack/react-query'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { listUsers } from '@/features/settings/api'
import { queryKeys } from '@/lib/query/queryKeys'
import { userRoleLabels, type UserRole } from '@/lib/auth/types'

/** Quem atua como advogado num processo: sociedade e advogados, em atividade. */
const papeisDeAdvogado: readonly UserRole[] = ['Owner', 'Partner', 'Lawyer']

/** Valor reservado: o Select do Radix não aceita `value=""` em item. */
const EU = '__eu__'

interface AdvogadosFieldProps {
  /** Id do advogado responsável; '' = quem está cadastrando. */
  responsavel: string
  adicionais: readonly string[]
  onChange: (valor: { responsavel: string; adicionais: string[] }) => void
}

/**
 * Advogado responsável + outros advogados do processo (escritório com equipe). Cada advogado
 * incluído é avisado, ao salvar, de que o processo foi atribuído a ele. O responsável sai da lista de
 * "outros" para ninguém aparecer duas vezes.
 */
export function AdvogadosField({ responsavel, adicionais, onChange }: AdvogadosFieldProps) {
  const { data: usuarios } = useQuery({ queryKey: queryKeys.users.list(), queryFn: listUsers })
  const advogados = (usuarios ?? []).filter(
    (usuario) => usuario.status === 'active' && papeisDeAdvogado.includes(usuario.role)
  )

  function alternar(id: string, marcado: boolean) {
    onChange({
      responsavel,
      adicionais: marcado
        ? [...adicionais.filter((a) => a !== id), id]
        : adicionais.filter((a) => a !== id),
    })
  }

  return (
    <div className="space-y-4 sm:col-span-2">
      <div className="space-y-2">
        <Label htmlFor="advogado-responsavel">Advogado responsável</Label>
        <Select
          value={responsavel === '' ? EU : responsavel}
          onValueChange={(proximo) => {
            const novo = proximo === EU ? '' : proximo
            // O novo responsável deixa de ser "outro advogado".
            onChange({ responsavel: novo, adicionais: adicionais.filter((a) => a !== novo) })
          }}
        >
          <SelectTrigger id="advogado-responsavel" aria-label="Advogado responsável">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={EU}>Eu</SelectItem>
            {advogados.map((usuario) => (
              <SelectItem key={usuario.id} value={usuario.id}>
                {usuario.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <fieldset className="space-y-2">
        <legend className="text-sm font-medium">Outros advogados no processo</legend>
        {advogados.filter((usuario) => usuario.id !== responsavel).length === 0 ? (
          <p className="text-sm text-muted-foreground">Nenhum outro advogado no escritório.</p>
        ) : (
          <ul className="grid gap-2 sm:grid-cols-2">
            {advogados
              .filter((usuario) => usuario.id !== responsavel)
              .map((usuario) => (
                <li key={usuario.id} className="flex items-center gap-2">
                  <Checkbox
                    id={`advogado-${usuario.id}`}
                    checked={adicionais.includes(usuario.id)}
                    onCheckedChange={(marcado) => alternar(usuario.id, marcado === true)}
                  />
                  <Label htmlFor={`advogado-${usuario.id}`} className="cursor-pointer font-normal">
                    {usuario.name}{' '}
                    <span className="text-xs text-muted-foreground">
                      · {userRoleLabels[usuario.role]}
                    </span>
                  </Label>
                </li>
              ))}
          </ul>
        )}
        <p className="text-xs text-muted-foreground">
          Cada advogado incluído recebe um aviso de que o processo foi atribuído a ele.
        </p>
      </fieldset>
    </div>
  )
}
