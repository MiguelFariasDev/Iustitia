'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, ArrowLeft, MailCheck, ShieldCheck } from 'lucide-react'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { env } from '@/config/env'
import { routes } from '@/config/routes'
import { getErrorMessage } from '@/lib/api/errors'
import { authService } from '@/lib/auth/authService'
import { notify } from '@/lib/notify'
import { useAuthStore } from '@/stores/authStore'
import { brazilianStates } from '@/lib/validation/common'
import { formatCnpj } from '@/lib/validation/cnpj'
import { formatCpf } from '@/lib/validation/documento'
import { accountTypeLabels, type AccountType } from '@/lib/auth/types'
import { createTenant } from '../api'
import {
  onboardingOfficeSchema,
  onboardingOwnerSchema,
  onboardingPreferencesSchema,
  practiceAreas,
  teamSizes,
  type OnboardingOfficeValues,
  type OnboardingOwnerValues,
  type OnboardingPreferencesValues,
} from '../schemas'
import { AuthCard } from './AuthCard'
import { OnboardingStepper } from './OnboardingStepper'
import { PasswordInput } from './PasswordInput'
import { PasswordRequirements } from './PasswordRequirements'

const stepsFor = (accountType: AccountType) => [
  accountType === 'Solo' ? 'Dados profissionais' : 'Escritório',
  'Responsável',
  'Preferências',
]
const STEP_HEADING_ID = 'onboarding-step-heading'
const teamSizeLabels: Record<(typeof teamSizes)[number], string> = {
  '1': 'Só eu',
  '2-5': '2 a 5 pessoas',
  '6-15': '6 a 15 pessoas',
  '16+': 'Mais de 15 pessoas',
}

export function OnboardingWizard() {
  const router = useRouter()
  const [step, setStep] = useState(0)
  const [office, setOffice] = useState<OnboardingOfficeValues | null>(null)
  const [owner, setOwner] = useState<OnboardingOwnerValues | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [completion, setCompletion] = useState<{
    email: string
    reason: 'confirm' | 'login'
  } | null>(null)

  const officeForm = useForm<OnboardingOfficeValues>({
    resolver: zodResolver(onboardingOfficeSchema),
    defaultValues: {
      accountType: 'Firm',
      officeName: '',
      cnpj: '',
      cpf: '',
      oabState: undefined,
      phone: '',
    },
  })
  const ownerForm = useForm<OnboardingOwnerValues>({
    resolver: zodResolver(onboardingOwnerSchema),
    defaultValues: { ownerName: '', email: '', oabNumber: '', password: '', confirmPassword: '' },
  })
  const preferencesForm = useForm<OnboardingPreferencesValues>({
    resolver: zodResolver(onboardingPreferencesSchema),
    defaultValues: {
      teamSize: undefined,
      practiceAreas: [],
      acceptTerms: false,
      acknowledgeAiReview: false,
    },
  })
  const password = useWatch({ control: ownerForm.control, name: 'password' })
  const accountType = useWatch({ control: officeForm.control, name: 'accountType' })
  const isSolo = accountType === 'Solo'
  const steps = stepsFor(accountType)

  function goTo(next: number) {
    setStep(next)
    // Leva o foco ao topo da etapa para leitores de tela anunciarem a mudança.
    requestAnimationFrame(() => document.getElementById(STEP_HEADING_ID)?.focus())
  }

  async function finish(preferences: OnboardingPreferencesValues) {
    if (!office || !owner) return
    setError(null)

    // 1) Cadastro: o backend cria a conta E o titular (no Supabase Auth e no banco) de uma vez.
    //    Falha aqui é erro de cadastro de verdade — nada foi criado, e tentar de novo é seguro.
    let created
    try {
      created = await createTenant({ office, owner, preferences })
    } catch (err) {
      setError(getErrorMessage(err))
      return
    }

    // Cadastro feito: a senha não precisa mais ficar em memória além do login logo abaixo.
    const { email, password } = owner
    setOwner(null)

    if (created.requiresEmailConfirmation) {
      setCompletion({ email, reason: 'confirm' })
      return
    }

    // 2) Entrada automática, com as credenciais que a pessoa acabou de definir. Se falhar, a
    //    conta JÁ EXISTE: mandar repetir o cadastro daria "já existe uma conta", então o caminho
    //    é entrar manualmente.
    try {
      const session = await authService.signIn({ email, password })
      useAuthStore.getState().setSession(session)
    } catch {
      setCompletion({ email, reason: 'login' })
      return
    }

    notify.success(`Bem-vindo(a) ao ${env.appName}!`, {
      description:
        office.accountType === 'Solo'
          ? 'Sua conta de advogado(a) autônomo(a) foi criada.'
          : `${office.officeName} foi cadastrado com sucesso.`,
    })
    router.replace(routes.dashboard)
    router.refresh()
  }

  if (completion) {
    return (
      <AuthCard title={completion.reason === 'confirm' ? 'Confirme seu e-mail' : 'Conta criada'}>
        <div role="status" className="flex gap-3 rounded-xl border bg-card p-4">
          <MailCheck className="mt-0.5 size-5 shrink-0 text-success" aria-hidden />
          <p className="text-sm">
            {completion.reason === 'confirm' ? (
              <>
                Enviamos um link de confirmação para{' '}
                <strong className="break-all">{completion.email}</strong>. Depois de confirmar,
                entre com o e-mail e a senha que você acabou de definir.
              </>
            ) : (
              <>
                Sua conta foi criada, mas não conseguimos entrar automaticamente. Entre com o e-mail{' '}
                <strong className="break-all">{completion.email}</strong> e a senha que você acabou
                de definir.
              </>
            )}
          </p>
        </div>
        <Button asChild>
          <Link href={routes.login}>Ir para o login</Link>
        </Button>
      </AuthCard>
    )
  }

  return (
    <AuthCard
      className="max-w-xl"
      title={isSolo ? 'Crie sua conta' : 'Cadastre seu escritório'}
      description="Leva menos de 2 minutos. Você poderá ajustar tudo depois em Configurações."
      footer={
        <>
          Já tem conta?{' '}
          <Link
            href={routes.login}
            className="font-medium text-secondary underline-offset-4 hover:underline"
          >
            Entrar
          </Link>
        </>
      }
    >
      <OnboardingStepper steps={steps} current={step} />

      <div id={STEP_HEADING_ID} tabIndex={-1} className="outline-none">
        <h2 className="text-lg font-semibold">
          Etapa {step + 1} de {steps.length}: {steps[step]}
        </h2>
      </div>

      {error ? (
        <Alert variant="destructive" role="alert">
          <AlertCircle aria-hidden />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}

      {step === 0 ? (
        <Form {...officeForm}>
          <form
            noValidate
            className="space-y-4"
            onSubmit={officeForm.handleSubmit((values) => {
              // Solo trabalha sozinho(a): a pergunta de equipe não se aplica e vale 1. Voltando
              // para Firm depois, a resposta fixada sai para a pessoa escolher de fato.
              if (values.accountType === 'Solo') preferencesForm.setValue('teamSize', '1')
              else if (office?.accountType === 'Solo') preferencesForm.resetField('teamSize')
              setOffice(values)
              goTo(1)
            })}
          >
            {env.soloSignupEnabled ? (
              <FormField
                control={officeForm.control}
                name="accountType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel id="tipo-conta-label">Como você atua?</FormLabel>
                    <div
                      role="radiogroup"
                      aria-labelledby="tipo-conta-label"
                      className="grid gap-3 sm:grid-cols-2"
                    >
                      {(['Firm', 'Solo'] as const).map((tipo) => (
                        <label
                          key={tipo}
                          className="flex cursor-pointer flex-col gap-1 rounded-xl border p-3 has-[:checked]:border-primary has-[:checked]:bg-primary/5 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring"
                        >
                          <span className="flex items-center gap-2 text-sm font-medium">
                            <input
                              type="radio"
                              name={field.name}
                              value={tipo}
                              checked={field.value === tipo}
                              onChange={() => field.onChange(tipo)}
                              className="accent-primary"
                            />
                            {accountTypeLabels[tipo]}
                          </span>
                          <span className="text-xs text-muted-foreground">
                            {tipo === 'Firm'
                              ? 'Tenho um escritório, com ou sem equipe. Identificado por CNPJ.'
                              : 'Atuo sozinho(a), sem equipe. Identificado(a) pelo meu CPF.'}
                          </span>
                        </label>
                      ))}
                    </div>
                    <FormMessage />
                  </FormItem>
                )}
              />
            ) : null}

            {isSolo ? null : (
              <FormField
                control={officeForm.control}
                name="officeName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Nome do escritório</FormLabel>
                    <FormControl>
                      <Input autoComplete="organization" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}
            <div className="grid gap-4 sm:grid-cols-2">
              {isSolo ? (
                <FormField
                  control={officeForm.control}
                  name="cpf"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>CPF</FormLabel>
                      <FormControl>
                        <Input
                          inputMode="numeric"
                          autoComplete="off"
                          placeholder="000.000.000-00"
                          {...field}
                          onChange={(e) => field.onChange(formatCpf(e.target.value))}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              ) : (
                <FormField
                  control={officeForm.control}
                  name="cnpj"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>CNPJ (opcional)</FormLabel>
                      <FormControl>
                        <Input
                          inputMode="numeric"
                          placeholder="00.000.000/0000-00"
                          {...field}
                          onChange={(e) => field.onChange(formatCnpj(e.target.value))}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}
              <FormField
                control={officeForm.control}
                name="oabState"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Seccional da OAB</FormLabel>
                    <Select value={field.value ?? ''} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Selecione a UF" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {brazilianStates.map((uf) => (
                          <SelectItem key={uf} value={uf}>
                            OAB/{uf}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
            <FormField
              control={officeForm.control}
              name="phone"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Telefone (opcional)</FormLabel>
                  <FormControl>
                    <Input type="tel" autoComplete="tel" placeholder="(51) 99999-9999" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="flex justify-end">
              <Button type="submit">Continuar</Button>
            </div>
          </form>
        </Form>
      ) : null}

      {step === 1 ? (
        <Form {...ownerForm}>
          <form
            noValidate
            className="space-y-4"
            onSubmit={ownerForm.handleSubmit((values) => {
              setOwner(values)
              goTo(2)
            })}
          >
            <FormField
              control={ownerForm.control}
              name="ownerName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Seu nome completo</FormLabel>
                  <FormControl>
                    <Input autoComplete="name" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={ownerForm.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>E-mail</FormLabel>
                    <FormControl>
                      <Input type="email" autoComplete="email" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={ownerForm.control}
                name="oabNumber"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Nº OAB (opcional)</FormLabel>
                    <FormControl>
                      <Input inputMode="numeric" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
            <FormField
              control={ownerForm.control}
              name="password"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Senha</FormLabel>
                  <FormControl>
                    <PasswordInput autoComplete="new-password" {...field} />
                  </FormControl>
                  <PasswordRequirements value={password} />
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={ownerForm.control}
              name="confirmPassword"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Confirmar senha</FormLabel>
                  <FormControl>
                    <PasswordInput autoComplete="new-password" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="flex justify-between">
              <Button type="button" variant="ghost" onClick={() => goTo(0)}>
                <ArrowLeft aria-hidden />
                Voltar
              </Button>
              <Button type="submit">Continuar</Button>
            </div>
          </form>
        </Form>
      ) : null}

      {step === 2 ? (
        <Form {...preferencesForm}>
          <form noValidate className="space-y-5" onSubmit={preferencesForm.handleSubmit(finish)}>
            {isSolo ? null : (
              <FormField
                control={preferencesForm.control}
                name="teamSize"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Tamanho da equipe</FormLabel>
                    <Select value={field.value ?? ''} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Selecione" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {teamSizes.map((size) => (
                          <SelectItem key={size} value={size}>
                            {teamSizeLabels[size]}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}
            <FormField
              control={preferencesForm.control}
              name="practiceAreas"
              render={({ field }) => (
                <FormItem>
                  <fieldset className="space-y-2">
                    <legend className="text-sm font-medium">Áreas de atuação</legend>
                    <div className="grid grid-cols-2 gap-2">
                      {practiceAreas.map((area) => {
                        const checked = field.value.includes(area)
                        return (
                          <label
                            key={area}
                            className="flex items-center gap-2 rounded-md border p-2.5 text-sm has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring"
                          >
                            <Checkbox
                              checked={checked}
                              onCheckedChange={(c) =>
                                field.onChange(
                                  c === true
                                    ? [...field.value, area]
                                    : field.value.filter((a) => a !== area)
                                )
                              }
                            />
                            {area}
                          </label>
                        )
                      })}
                    </div>
                  </fieldset>
                  <FormMessage />
                </FormItem>
              )}
            />

            <Alert>
              <ShieldCheck aria-hidden />
              <AlertTitle>Inteligência artificial com revisão humana</AlertTitle>
              <AlertDescription>
                Sugestões geradas por IA (classificação de publicações, prazos, Athena) nunca viram
                ação sem a confirmação de um(a) advogado(a) — conforme o Provimento CFOAB nº
                271/2025. Seus dados não são usados para treinar modelos.
              </AlertDescription>
            </Alert>

            <FormField
              control={preferencesForm.control}
              name="acknowledgeAiReview"
              render={({ field }) => (
                <FormItem>
                  <div className="flex items-start gap-2">
                    <FormControl>
                      <Checkbox
                        checked={field.value}
                        onCheckedChange={(c) => field.onChange(c === true)}
                      />
                    </FormControl>
                    <FormLabel className="leading-snug font-normal">
                      Estou ciente de que toda sugestão de IA exige revisão humana antes de qualquer
                      ação.
                    </FormLabel>
                  </div>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={preferencesForm.control}
              name="acceptTerms"
              render={({ field }) => (
                <FormItem>
                  <div className="flex items-start gap-2">
                    <FormControl>
                      <Checkbox
                        checked={field.value}
                        onCheckedChange={(c) => field.onChange(c === true)}
                      />
                    </FormControl>
                    <FormLabel className="leading-snug font-normal">
                      Li e aceito os termos de uso e a política de privacidade (LGPD).
                    </FormLabel>
                  </div>
                  <FormDescription>
                    Você pode solicitar a exclusão dos seus dados a qualquer momento.
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="flex justify-between">
              <Button type="button" variant="ghost" onClick={() => goTo(1)}>
                <ArrowLeft aria-hidden />
                Voltar
              </Button>
              <Button
                type="submit"
                disabled={preferencesForm.formState.isSubmitting}
                aria-busy={preferencesForm.formState.isSubmitting}
              >
                {preferencesForm.formState.isSubmitting ? (
                  <LoadingSpinner size="sm" decorative />
                ) : null}
                {isSolo ? 'Criar conta' : 'Criar escritório'}
              </Button>
            </div>
          </form>
        </Form>
      ) : null}
    </AuthCard>
  )
}
