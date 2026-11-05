'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { ImageUp } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Checkbox } from '@/components/ui/checkbox'
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { routes } from '@/config/routes'
import { TERMS_VERSION } from '@/features/landing/LegalPage'
import { validateLogo } from '../../logo'
import { timezones } from '../../catalog'
import { browserTimezone } from '../../store'
import { StepActions } from '../StepActions'
import type { StepProps } from './types'

const schema = z.object({
  name: z.string().trim().min(3, 'Informe o nome do escritório.').max(120, 'Nome muito longo.'),
  timezone: z.string().min(1, 'Selecione o fuso horário.'),
})
type Values = z.infer<typeof schema>

/** Passo 1 (obrigatório): nome, logotipo, fuso e — se o aceite não veio do cadastro — os termos. */
export function StepOffice({ state, onComplete }: StepProps) {
  const office = state.data.office
  const needsConsent = !state.data.consent
  const [consent, setConsent] = useState(false)
  const [consentError, setConsentError] = useState(false)
  const [logo, setLogo] = useState<string | undefined>(office?.logoDataUrl)
  const [persistLogo, setPersistLogo] = useState(true)
  const [logoError, setLogoError] = useState<string | null>(null)

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    mode: 'onBlur',
    defaultValues: { name: office?.name ?? '', timezone: office?.timezone ?? browserTimezone() },
  })

  async function onLogo(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) return
    const result = await validateLogo(file)
    if (!result.ok) {
      setLogoError(result.error)
      event.target.value = ''
      return
    }
    setLogoError(null)
    setLogo(result.dataUrl)
    setPersistLogo(result.persistable)
  }

  function submit(values: Values) {
    if (needsConsent && !consent) {
      setConsentError(true)
      return
    }
    onComplete({
      office: {
        name: values.name,
        timezone: values.timezone,
        language: 'pt-BR',
        logoDataUrl: logo && persistLogo ? logo : undefined,
      },
      ...(needsConsent
        ? { consent: { termsAcceptedAt: new Date().toISOString(), termsVersion: TERMS_VERSION } }
        : {}),
    })
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(submit)} className="space-y-5" noValidate>
        <FormField
          control={form.control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Nome do escritório</FormLabel>
              <FormControl>
                <Input className="h-11" autoComplete="organization" autoFocus {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="space-y-2">
          <label htmlFor="logo" className="text-sm font-medium">
            Logotipo <span className="font-normal text-muted-foreground">(opcional)</span>
          </label>
          <div className="flex items-center gap-4">
            <div className="flex size-16 items-center justify-center overflow-hidden rounded-xl border bg-card">
              {logo ? (
                // eslint-disable-next-line @next/next/no-img-element -- prévia local (data URL), sem otimização possível
                <img src={logo} alt="Prévia do logotipo" className="size-full object-contain" />
              ) : (
                <ImageUp className="size-6 text-muted-foreground" aria-hidden />
              )}
            </div>
            <Input
              id="logo"
              type="file"
              accept="image/png,image/svg+xml"
              className="h-11 flex-1 cursor-pointer"
              onChange={onLogo}
              aria-describedby="logo-help"
            />
          </div>
          <p id="logo-help" className="text-xs text-muted-foreground">
            PNG ou SVG, até 2 MB. Por enquanto o logotipo fica guardado neste navegador: o envio ao
            servidor depende do armazenamento de arquivos, que ainda não existe.
            {!persistLogo ? ' Este arquivo é grande demais para ser guardado e valerá só nesta sessão.' : ''}
          </p>
          {logoError ? (
            <p role="alert" className="iu-in text-sm text-[var(--iu-danger)]">
              {logoError}
            </p>
          ) : null}
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="timezone"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Fuso horário</FormLabel>
                <Select onValueChange={field.onChange} value={field.value}>
                  <FormControl>
                    <SelectTrigger className="h-11 w-full">
                      <SelectValue />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    {[...new Map([[field.value, { value: field.value, label: field.value }], ...timezones.map((t) => [t.value, t] as const)]).values()].map((tz) => (
                      <SelectItem key={tz.value} value={tz.value}>
                        {tz.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FormDescription>Os prazos são calculados neste fuso.</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
          <div className="space-y-2">
            <span className="text-sm font-medium">Idioma</span>
            <Input className="h-11" value="Português (Brasil)" disabled readOnly aria-label="Idioma" />
          </div>
        </div>

        {needsConsent ? (
          <div className="space-y-2">
            <div className="flex items-start gap-3">
              <Checkbox
                id="consent"
                checked={consent}
                onCheckedChange={(checked) => {
                  setConsent(checked === true)
                  setConsentError(false)
                }}
                aria-invalid={consentError || undefined}
                aria-describedby={consentError ? 'consent-error' : undefined}
                className="mt-0.5"
              />
              <label htmlFor="consent" className="text-sm leading-snug">
                Li e aceito os{' '}
                <Link href={routes.terms} target="_blank" className="font-medium underline underline-offset-4">
                  Termos de Uso
                </Link>{' '}
                e a{' '}
                <Link href={routes.privacy} target="_blank" className="font-medium underline underline-offset-4">
                  Política de Privacidade
                </Link>
                .
              </label>
            </div>
            {consentError ? (
              <p id="consent-error" role="alert" className="iu-in text-sm text-[var(--iu-danger)]">
                É necessário aceitar os termos para continuar.
              </p>
            ) : null}
          </div>
        ) : null}

        <StepActions />
      </form>
    </Form>
  )
}
