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
import { formatCnpj } from '@/lib/validation/cnpj'
import { brazilianStates } from '@/lib/validation/common'
import { getTenantProfile, updateTenantProfile } from '../api'
import { tenantProfileSchema, type TenantProfileValues } from '../schemas'
import type { TenantProfile } from '../types'
import { FormSkeleton } from './FormSkeleton'
import { SettingsSection } from './SettingsSection'

const timezones = [
  { value: 'America/Sao_Paulo', label: 'Brasília (UTC−3)' },
  { value: 'America/Manaus', label: 'Amazonas (UTC−4)' },
  { value: 'America/Rio_Branco', label: 'Acre (UTC−5)' },
  { value: 'America/Noronha', label: 'Fernando de Noronha (UTC−2)' },
]

function TenantForm({ tenant }: { tenant: TenantProfile }) {
  const queryClient = useQueryClient()
  const form = useForm<TenantProfileValues>({
    resolver: zodResolver(tenantProfileSchema),
    defaultValues: tenant as TenantProfileValues,
  })

  const mutation = useMutation({
    mutationFn: updateTenantProfile,
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.tenant.profile(), saved)
      void queryClient.invalidateQueries({ queryKey: queryKeys.tenant.current() })
      form.reset(saved as TenantProfileValues)
      notify.success('Dados do escritório atualizados.')
    },
    onError: (error) =>
      notify.error('Não foi possível salvar.', { description: getErrorMessage(error) }),
  })

  const text = (
    name: 'name' | 'email' | 'phone' | 'address' | 'city',
    label: string,
    props: React.ComponentProps<typeof Input> = {},
    wide = false
  ) => (
    <FormField
      control={form.control}
      name={name}
      render={({ field }) => (
        <FormItem className={wide ? 'sm:col-span-2' : undefined}>
          <FormLabel>{label}</FormLabel>
          <FormControl>
            <Input {...props} {...field} />
          </FormControl>
          <FormMessage />
        </FormItem>
      )}
    />
  )

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit((v) => mutation.mutate(v))} noValidate>
        <SettingsSection
          title="Dados do escritório"
          description="Usados em documentos, peças e comunicações com clientes."
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
            {text('name', 'Nome do escritório', { autoComplete: 'organization' }, true)}
            <FormField
              control={form.control}
              name="cnpj"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>CNPJ</FormLabel>
                  <FormControl>
                    <Input
                      inputMode="numeric"
                      {...field}
                      onChange={(e) => field.onChange(formatCnpj(e.target.value))}
                    />
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
                  <FormLabel>Seccional da OAB</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger className="w-full">
                        <SelectValue />
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
            {text('email', 'E-mail de contato', { type: 'email', autoComplete: 'email' })}
            {text('phone', 'Telefone', { type: 'tel', autoComplete: 'tel' })}
            {text('address', 'Endereço', { autoComplete: 'street-address' }, true)}
            {text('city', 'Cidade', { autoComplete: 'address-level2' })}
            <FormField
              control={form.control}
              name="timezone"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Fuso horário (contagem de prazos)</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {timezones.map((tz) => (
                        <SelectItem key={tz.value} value={tz.value}>
                          {tz.label}
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

export function TenantSettings() {
  const query = useQuery({ queryKey: queryKeys.tenant.profile(), queryFn: getTenantProfile })
  if (query.isPending) return <FormSkeleton fields={8} />
  if (query.isError)
    return <ErrorFallback error={query.error} onRetry={() => void query.refetch()} />
  return <TenantForm tenant={query.data} />
}
