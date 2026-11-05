import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Design system do app. Mesmos tons do web: azul-marinho (primária), azul elétrico
/// (secundária) e dourado (destaque).
abstract final class AppColors {
  static const navy = Color(0xFF0A2540);
  static const electric = Color(0xFF2563EB);
  static const gold = Color(0xFFC9A961);

  /// Primária no tema escuro: o azul-marinho sumiria sobre #121212 (contraste ~1,2:1), então
  /// usa um azul claro que mantém contraste de texto (AA) e preserva a identidade.
  static const navyOnDark = Color(0xFF9DB9DF);
  static const electricOnDark = Color(0xFF7BA4FF);

  static const darkBackground = Color(0xFF121212);
  static const darkSurface = Color(0xFF1C1C1E);

  // Semânticas. A cor nunca é o único indicador: todo badge tem texto.
  static const success = Color(0xFF15803D);
  static const warning = Color(0xFFB45309);
  static const danger = Color(0xFFB91C1C);
}

/// Tons de status (fundo suave + texto de alto contraste), por brilho do tema.
@immutable
class StatusTone {
  const StatusTone({required this.background, required this.foreground});

  final Color background;
  final Color foreground;
}

ThemeData buildAppTheme(Brightness brightness, {bool googleFonts = true}) {
  final dark = brightness == Brightness.dark;

  final scheme = dark
      ? ColorScheme.dark(
          primary: AppColors.navyOnDark,
          onPrimary: AppColors.navy,
          secondary: AppColors.electricOnDark,
          onSecondary: AppColors.navy,
          tertiary: AppColors.gold,
          onTertiary: AppColors.navy,
          surface: AppColors.darkSurface,
          onSurface: const Color(0xFFEDEDED),
          error: const Color(0xFFFCA5A5),
        )
      : const ColorScheme.light(
          primary: AppColors.navy,
          onPrimary: Colors.white,
          secondary: AppColors.electric,
          onSecondary: Colors.white,
          tertiary: AppColors.gold,
          onTertiary: AppColors.navy,
          surface: Colors.white,
          onSurface: Color(0xFF111827),
          error: AppColors.danger,
        );

  final base = ThemeData(brightness: brightness, colorScheme: scheme, useMaterial3: true);
  final textTheme = googleFonts ? GoogleFonts.interTextTheme(base.textTheme) : base.textTheme;

  return base.copyWith(
    textTheme: textTheme,
    scaffoldBackgroundColor: dark ? AppColors.darkBackground : Colors.white,
    appBarTheme: AppBarTheme(
      backgroundColor: dark ? AppColors.darkBackground : Colors.white,
      foregroundColor: scheme.onSurface,
      elevation: 0,
      scrolledUnderElevation: 1,
      centerTitle: false,
      titleTextStyle: textTheme.titleLarge?.copyWith(
        fontWeight: FontWeight.w600,
        color: scheme.onSurface,
      ),
    ),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: dark ? AppColors.darkSurface : Colors.white,
      indicatorColor: scheme.primary.withValues(alpha: dark ? 0.24 : 0.12),
      labelTextStyle: WidgetStatePropertyAll(
        textTheme.labelMedium?.copyWith(fontWeight: FontWeight.w600),
      ),
    ),
    cardTheme: CardThemeData(
      elevation: 0,
      margin: EdgeInsets.zero,
      color: dark ? AppColors.darkSurface : Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(color: dark ? const Color(0xFF2E2E31) : const Color(0xFFE5E7EB)),
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: dark ? AppColors.darkSurface : const Color(0xFFF9FAFB),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        minimumSize: const Size.fromHeight(52),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        minimumSize: const Size.fromHeight(52),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
    ),
    chipTheme: ChipThemeData(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
    ),
    // Alvos de toque de 48dp, no mínimo: é o piso de acessibilidade do Material.
    materialTapTargetSize: MaterialTapTargetSize.padded,
  );
}

extension StatusTones on ColorScheme {
  bool get _dark => brightness == Brightness.dark;

  StatusTone get successTone => _dark
      ? const StatusTone(background: Color(0xFF14301F), foreground: Color(0xFF86EFAC))
      : const StatusTone(background: Color(0xFFDCFCE7), foreground: Color(0xFF14532D));

  StatusTone get warningTone => _dark
      ? const StatusTone(background: Color(0xFF3A2A10), foreground: Color(0xFFFCD34D))
      : const StatusTone(background: Color(0xFFFEF3C7), foreground: Color(0xFF78350F));

  StatusTone get dangerTone => _dark
      ? const StatusTone(background: Color(0xFF3B1818), foreground: Color(0xFFFCA5A5))
      : const StatusTone(background: Color(0xFFFEE2E2), foreground: Color(0xFF7F1D1D));

  StatusTone get infoTone => _dark
      ? const StatusTone(background: Color(0xFF14233F), foreground: Color(0xFF93C5FD))
      : const StatusTone(background: Color(0xFFDBEAFE), foreground: Color(0xFF1E3A8A));

  StatusTone get neutralTone => _dark
      ? const StatusTone(background: Color(0xFF2A2A2D), foreground: Color(0xFFD4D4D8))
      : const StatusTone(background: Color(0xFFF3F4F6), foreground: Color(0xFF374151));
}
