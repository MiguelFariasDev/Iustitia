'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle } from 'lucide-react'
import { useRouter } from 'next/navigation'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { routes } from '@/config/routes'
import { getErrorMessage } from '@/lib/api/errors'
import { authService } from '@/lib/auth/authService'
import { notify } from '@/lib/notify'
import { resetPasswordSchema, type ResetPasswordValues } from '../schemas'
import { AuthCard } from './AuthCard'
import { PasswordInput } from './PasswordInput'
import { PasswordRequirements } from './PasswordRequirements'

export function ResetPasswordForm() {
  const router = useRouter()
  const [error, setError] = useState<string | null>(null)
  const form = useForm<ResetPasswordValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { password: '', confirmPassword: '' },
  })
  const password = useWatch({ control: form.control, name: 'password' })

  async function onSubmit({ password: newPassword }: ResetPasswordValues) {
    setError(null)
    try {
      await authService.updatePassword(newPassword)
      notify.success('Senha redefinida com sucesso.')
      router.replace(routes.dashboard)
    } catch (err) {
      setError(getErrorMessage(err))
    }
  }

  return (
    <AuthCard title="Redefinir senha" description="Crie uma nova senha para acessar sua conta.">
      {error ? (
        <Alert variant="destructive" role="alert">
          <AlertCircle aria-hidden />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}
      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <FormField
            control={form.control}
            name="password"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Nova senha</FormLabel>
                <FormControl>
                  <PasswordInput autoComplete="new-password" autoFocus {...field} />
                </FormControl>
                <PasswordRequirements value={password} />
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="confirmPassword"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Confirmar nova senha</FormLabel>
                <FormControl>
                  <PasswordInput autoComplete="new-password" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <Button
            type="submit"
            className="w-full"
            disabled={form.formState.isSubmitting}
            aria-busy={form.formState.isSubmitting}
          >
            {form.formState.isSubmitting ? <LoadingSpinner size="sm" decorative /> : null}
            Salvar nova senha
          </Button>
        </form>
      </Form>
    </AuthCard>
  )
}
