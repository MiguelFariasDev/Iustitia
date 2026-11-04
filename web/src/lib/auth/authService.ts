import { isMockAuth } from '@/config/env'
import { supabaseAuthService } from '@/lib/supabase/auth'
import { mockAuthService } from './mockAuth'
import type { AuthService } from './types'

/** Implementação ativa, escolhida por NEXT_PUBLIC_AUTH_MODE (inlinada em build). */
export const authService: AuthService = isMockAuth ? mockAuthService : supabaseAuthService
