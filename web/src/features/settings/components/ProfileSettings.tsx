'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { ErrorFallback } from '@/components/shared/ErrorFallback'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
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
import { getErrorMessage } from '@/lib/api/errors'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { brazilianStates } from '@/lib/validation/common'
import { getProfile, updateProfile } from '../api'
import { profileSchema, type ProfileValues } from '../schemas'
import type { Profile } from '../types'
import { FormSkeleton } from './FormSkeleton'
import { SettingsSection } from './SettingsSection'

function ProfileForm({ profile }: { profile: Profile }) {
  const queryClient = useQueryClient()
  const form = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { ...profile, oabState: (profile.oabState as ProfileValues['oabState']) || '' },
  })

  const mutation = useMutation({
    mutationFn: updateProfile,
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.profile, saved)
      form.reset({ ...saved, oabState: saved.oabState as ProfileValues['oabState'] })
      notify.success('Perfil atualizado.')
    },
    onError: (error) =>
      notify.error('Não foi possível salvar o perfil.', { description: getErrorMessage(error) }),
  })

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit((v) => mutation.mutate(v))} noValidate>
        <SettingsSection
          title="Dados pessoais"
          description="Essas informações aparecem para a equipe do escritório."
          footer={
            <Button
              type="submit"
              disabled={mutation.isPending || !form.formState.isDirty}
              aria-busy={mutation.isPending}
            >
              {mutation.isPending ? <LoadingSpinner size="sm" decorative /> : null}
              Salvar alterações
            </Button>
          }
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>Nome completo</FormLabel>
                  <FormControl>
                    <Input autoComplete="name" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="email"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>E-mail</FormLabel>
                  <FormControl>
                    <Input type="email" readOnly aria-readonly {...field} />
                  </FormControl>
                  <FormDescription>Alterações de e-mail passam por confirmação.</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="phone"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Telefone</FormLabel>
                  <FormControl>
                    <Input type="tel" autoComplete="tel" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="oabNumber"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nº OAB</FormLabel>
                  <FormControl>
                    <Input inputMode="numeric" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="oabState"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Seccional</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger className="w-full">
                        <SelectValue placeholder="UF" />
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
        </SettingsSection>
      </form>
    </Form>
  )
}

export function ProfileSettings() {
  const query = useQuery({ queryKey: queryKeys.profile, queryFn: getProfile })
  if (query.isPending) return <FormSkeleton fields={5} />
  if (query.isError)
    return <ErrorFallback error={query.error} onRetry={() => void query.refetch()} />
  return <ProfileForm profile={query.data} />
}
