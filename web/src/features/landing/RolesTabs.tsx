'use client'

import { Check } from 'lucide-react'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { roles } from './content'

export function RolesTabs() {
  return (
    <Tabs defaultValue={roles[0].id} className="gap-6">
      <TabsList className="h-auto flex-wrap justify-start">
        {roles.map((role) => (
          <TabsTrigger key={role.id} value={role.id} className="min-h-11 px-4">
            {role.label}
          </TabsTrigger>
        ))}
      </TabsList>
      {roles.map((role) => (
        <TabsContent key={role.id} value={role.id} className="iu-in">
          <h3 className="mb-4 text-xl font-semibold">{role.headline}</h3>
          <ul className="space-y-3">
            {role.items.map((item) => (
              <li key={item} className="flex gap-3 text-sm leading-relaxed">
                <Check className="mt-0.5 size-4 shrink-0 text-[var(--iu-brand)]" aria-hidden />
                {item}
              </li>
            ))}
          </ul>
        </TabsContent>
      ))}
    </Tabs>
  )
}
