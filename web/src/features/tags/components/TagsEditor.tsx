'use client'

import { Plus, Tag as TagIcon, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useTagActions, useTagsState } from '../hooks'
import { tagsDe } from '../repositorio'
import type { AlvoTag } from '../types'
import { TagBadge } from './TagBadge'

interface TagsEditorProps {
  alvo: AlvoTag
  alvoId: string
  /** Nome de quem recebe a tag, para o leitor de tela ("Tags do processo"). */
  rotulo: string
}

/** Tags aplicadas + um seletor para marcar, criar e apagar tags. */
export function TagsEditor({ alvo, alvoId, rotulo }: TagsEditorProps) {
  const estado = useTagsState()
  const aplicadas = tagsDe(estado, alvo, alvoId)
  const acoes = useTagActions()
  const [nome, setNome] = useState('')

  async function criar() {
    const tag = await acoes.criar(nome)
    if (!tag) return
    if (!aplicadas.some((t) => t.id === tag.id)) await acoes.definir(alvo, alvoId, tag.id, true)
    setNome('')
  }

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {aplicadas.map((tag) => (
        <TagBadge key={tag.id} tag={tag} />
      ))}
      <Popover>
        <PopoverTrigger asChild>
          <Button variant="outline" size="sm" className="h-7 gap-1 px-2 text-xs">
            {aplicadas.length === 0 ? <TagIcon aria-hidden /> : <Plus aria-hidden />}
            {aplicadas.length === 0 ? 'Adicionar tag' : 'Editar'}
            <span className="sr-only"> — {rotulo}</span>
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-72 space-y-3">
          <p className="text-sm font-medium">Tags — {rotulo}</p>

          {estado.tags.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Nenhuma tag criada. Digite um nome abaixo para criar a primeira.
            </p>
          ) : (
            <ul className="max-h-48 space-y-1 overflow-y-auto">
              {estado.tags.map((tag) => (
                <li key={tag.id} className="flex items-center gap-2">
                  <Checkbox
                    id={`tag-${alvo}-${alvoId}-${tag.id}`}
                    checked={aplicadas.some((t) => t.id === tag.id)}
                    onCheckedChange={(marcada) => void acoes.definir(alvo, alvoId, tag.id, marcada === true)}
                  />
                  <label
                    htmlFor={`tag-${alvo}-${alvoId}-${tag.id}`}
                    className="flex-1 cursor-pointer"
                  >
                    <TagBadge tag={tag} />
                  </label>
                  <button
                    type="button"
                    onClick={() => void acoes.remover(tag.id)}
                    aria-label={`Apagar a tag ${tag.nome} de todos os lugares`}
                    className="rounded p-1 text-muted-foreground hover:text-destructive"
                  >
                    <Trash2 className="size-3.5" aria-hidden />
                  </button>
                </li>
              ))}
            </ul>
          )}

          <form
            className="flex gap-2"
            onSubmit={(event) => {
              event.preventDefault()
              void criar()
            }}
          >
            <Input
              value={nome}
              onChange={(event) => setNome(event.target.value)}
              placeholder="Nova tag"
              maxLength={30}
              aria-label="Nome da nova tag"
            />
            <Button type="submit" size="sm" disabled={nome.trim() === ''}>
              Criar
            </Button>
          </form>
          <p className="text-xs text-muted-foreground">
            As tags valem para todo o escritório.
          </p>
        </PopoverContent>
      </Popover>
    </div>
  )
}
