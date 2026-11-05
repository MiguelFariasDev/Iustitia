'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Alert, AlertDescription } from '@/components/ui/alert'
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
import { routes } from '@/config/routes'
import { ApiError, getErrorMessage } from '@/lib/api/errors'
import { formatCnpj } from '@/lib/validation/cnpj'
import { brazilianStates } from '@/lib/validation/common'
import { createTenant } from '../api'
import {
  signupAccountSchema,
  signupAreas,
  signupOfficeSchema,
  teamSizes,
  type SignupAccountValues,
  type SignupOfficeValues,
} from '../schemas'
import { saveSignupDraft } from '../signupDraft'
import { AuthCard } from './AuthCard'
import { PasswordInput } from './PasswordInput'
import { PasswordStrengthMeter } from './PasswordStrengthMeter'
import { StepProgress } from './StepProgress'
import { useStepTransition } from './useStepTransition'
import { VerifyEmailNotice } from './VerifyEmailNotice'

const STEPS = ['Você', 'Escritório'] as const
const HEADING_ID = 'signup-step-heading'
const teamSizeLabels: Record<(typeof teamSizes)[number], string> = {
  '1': 'Só eu',
  '2-5': '2 a 5 pessoas',
  '6-15': '6 a 15 pessoas',
  '16+': 'Mais de 15 pessoas',
}

type Phase = 'account' | 'office' | 'verify'

export function SignupFlow() {
  const { step: phase, leaving, goTo } = useStepTransition<Phase>('account')
  const [account, setAccount] = useState<SignupAccountValues | null>(null)
  const [error, setError] = useState<string | null>(null)
  // Honeypot: campo invisível que humanos não preenchem. Robôs de formulário preenchem.
  const [trap, setTrap] = useState('')

  const accountForm = useForm<SignupAccountValues>({
    resolver: zodResolver(signupAccountSchema),
    mode: 'onBlur',
    defaultValues: { name: '', email: '', password: '' },
  })
  const officeForm = useForm<SignupOfficeValues>({
    resolver: zodResolver(signupOfficeSchema),
    mode: 'onBlur',
    defaultValues: {
      officeName: '',
      cnpj: '',
      oabNumber: '',
      oabState: undefined,
      teamSize: undefined,
      practiceArea: undefined,
      acceptTerms: false,
    },
  })
  const password = useWatch({ control: accountForm.control, name: 'password' })

  const focusHeading = () => document.getElementById(HEADING_ID)?.focus()

  function continueToOffice(values: SignupAccountValues) {
    setAccount(values)
    goTo('office', focusHeading)
  }

  async function submit(office: SignupOfficeValues) {
    if (!account) return
    setError(null)

    if (trap) {
      // Robô: finge sucesso, não cria nada.
      goTo('verify', focusHeading)
      return
    }

    try {
      await createTenant({
        officeName: office.officeName,
        cnpj: office.cnpj.replace(/\D/g, ''),
        adminName: account.name,
        adminEmail: account.email,
        adminPassword: account.password,
      })
    } catch (err) {
      if (err instanceof ApiError && err.code === 'TENANT_CNPJ_DUPLICATED') {
        officeForm.setError('cnpj', {
          message: 'Já existe um escritório com este CNPJ. Peça um convite ao administrador dele.',
        })
        officeForm.setFocus('cnpj')
        return
      }
      // 409 sem código de tenant = e-mail já cadastrado no provedor de autenticação. Não revelamos:
      // seguimos para a mesma tela neutra "Confirme seu e-mail".
      if (!(err instanceof ApiError && err.status === 409)) {
        setError(getErrorMessage(err))
        return
      }
    }

    saveSignupDraft({
      email: account.email,
      officeName: office.officeName,
      oabNumber: office.oabNumber,
      oabState: office.oabState,
      teamSize: office.teamSize,
      practiceArea: office.practiceArea,
    })
    goTo('verify', focusHeading)
  }

  if (phase === 'verify' && account) {
    return (
      <div className={leaving ? 'opacity-0 transition-opacity duration-[120ms]' : undefined}>
        <VerifyEmailNotice email={account.email} onChangeEmail={() => goTo('account', focusHeading)} />
      </div>
    )
  }

  const stepIndex = phase === 'account' ? 0 : 1
  const submitting = officeForm.formState.isSubmitting

  return (
    <div className="w-full max-w-[420px] space-y-6">
      <StepProgress steps={STEPS} current={stepIndex} />

      <div
        key={phase}
        className={leaving ? 'opacity-0 transition-opacity duration-[120ms]' : undefined}
      >
        {phase === 'account' ? (
          <AuthCard
            title="Crie sua conta"
            description="Leva menos de dois minutos. Sem cartão de crédito."
            footer={
              <>
                Já tem conta?{' '}
                <Link href={routes.login} className="font-medium text-secondary underline-offset-4 hover:underline">
                  Entrar
                </Link>
              </>
            }
            className="max-w-none"
          >
            <HeadingAnchor>Seus dados</HeadingAnchor>
            <Form {...accountForm}>
              <form onSubmit={accountForm.handleSubmit(continueToOffice)} className="space-y-4" noValidate>
                <FormField
                  control={accountForm.control}
                  name="name"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Nome completo</FormLabel>
                      <FormControl>
                        <Input className="h-11" autoComplete="name" autoFocus {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={accountForm.control}
                  name="email"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>E-mail profissional</FormLabel>
                      <FormControl>
                        <Input className="h-11" type="email" autoComplete="email" inputMode="email" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={accountForm.control}
                  name="password"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Senha</FormLabel>
                      <FormControl>
                        <PasswordInput className="h-11" autoComplete="new-password" {...field} />
                      </FormControl>
                      <PasswordStrengthMeter password={password} />
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <Button type="submit" className="iu-btn-lg w-full">
                  Continuar
                </Button>
              </form>
            </Form>
          </AuthCard>
        ) : (
          <AuthCard
            title="Seu escritório"
            description="Usamos estes dados para configurar o espaço do escritório."
            className="max-w-none"
          >
            <HeadingAnchor>Dados do escritório</HeadingAnchor>
            {error ? (
              <Alert variant="destructive" role="alert">
                <AlertCircle aria-hidden />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            ) : null}
            <Form {...officeForm}>
              <form onSubmit={officeForm.handleSubmit(submit)} className="space-y-4" noValidate>
                <div aria-hidden className="absolute -left-[9999px] h-0 w-0 overflow-hidden">
                  <label>
                    Não preencha este campo
                    <input type="text" name="website" tabIndex={-1} autoComplete="off" value={trap} onChange={(event) => setTrap(event.target.value)} />
                  </label>
                </div>

                <FormField
                  control={officeForm.control}
                  name="officeName"
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
                <FormField
                  control={officeForm.control}
                  name="cnpj"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>CNPJ do escritório</FormLabel>
                      <FormControl>
                        <Input
                          className="iu-mono h-11"
                          inputMode="numeric"
                          placeholder="00.000.000/0000-00"
                          {...field}
                          onChange={(event) => field.onChange(formatCnpj(event.target.value))}
                        />
                      </FormControl>
                      <FormDescription>Necessário para criar o espaço do escritório.</FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <div className="grid grid-cols-[1fr_7rem] gap-3">
                  <FormField
                    control={officeForm.control}
                    name="oabNumber"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Sua OAB (número)</FormLabel>
                        <FormControl>
                          <Input className="iu-mono h-11" inputMode="numeric" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={officeForm.control}
                    name="oabState"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>UF</FormLabel>
                        <Select onValueChange={field.onChange} value={field.value ?? ''}>
                          <FormControl>
                            <SelectTrigger className="h-11 w-full" aria-label="UF da OAB">
                              <SelectValue placeholder="UF" />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
                            {brazilianStates.map((uf) => (
                              <SelectItem key={uf} value={uf}>
                                {uf}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>
                <p className="-mt-2 text-xs text-muted-foreground">OAB informada, não verificada.</p>

                <FormField
                  control={officeForm.control}
                  name="teamSize"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Tamanho da equipe</FormLabel>
                      <Select onValueChange={field.onChange} value={field.value ?? ''}>
                        <FormControl>
                          <SelectTrigger className="h-11 w-full">
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
                <FormField
                  control={officeForm.control}
                  name="practiceArea"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Área principal</FormLabel>
                      <Select onValueChange={field.onChange} value={field.value ?? ''}>
                        <FormControl>
                          <SelectTrigger className="h-11 w-full">
                            <SelectValue placeholder="Selecione" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {signupAreas.map((area) => (
                            <SelectItem key={area} value={area}>
                              {area}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={officeForm.control}
                  name="acceptTerms"
                  render={({ field }) => (
                    <FormItem>
                      <div className="flex items-start gap-3">
                        <FormControl>
                          <Checkbox
                            checked={field.value}
                            onCheckedChange={(checked) => field.onChange(checked === true)}
                            className="mt-0.5"
                          />
                        </FormControl>
                        <FormLabel className="text-sm leading-snug font-normal">
                          Li e aceito os{' '}
                          <Link href={routes.terms} target="_blank" className="font-medium underline underline-offset-4">
                            Termos de Uso
                          </Link>{' '}
                          e a{' '}
                          <Link href={routes.privacy} target="_blank" className="font-medium underline underline-offset-4">
                            Política de Privacidade
                          </Link>
                          .
                        </FormLabel>
                      </div>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <div className="flex gap-3">
                  <Button
                    type="button"
                    variant="outline"
                    className="iu-btn-lg"
                    onClick={() => goTo('account', focusHeading)}
                    disabled={submitting}
                  >
                    Voltar
                  </Button>
                  <Button type="submit" className="iu-btn-lg flex-1" disabled={submitting} aria-busy={submitting}>
                    {submitting ? <LoadingSpinner size="sm" decorative /> : null}
                    Criar escritório
                  </Button>
                </div>
              </form>
            </Form>
          </AuthCard>
        )}
      </div>
    </div>
  )
}

/** Âncora de foco da etapa: leitores de tela anunciam o título ao trocar de passo. */
function HeadingAnchor({ children }: { children: React.ReactNode }) {
  return (
    <h2 id={HEADING_ID} tabIndex={-1} className="sr-only">
      {children}
    </h2>
  )
}
