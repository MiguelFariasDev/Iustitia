import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/providers.dart';
import 'config.dart';
import 'notification_host.dart';
import 'router.dart';
import 'theme.dart';

class AdvocaciaApp extends ConsumerWidget {
  const AdvocaciaApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final useGoogleFonts = ref.watch(useGoogleFontsProvider);

    return MaterialApp.router(
      title: AppConfig.appName,
      debugShowCheckedModeBanner: false,
      routerConfig: ref.watch(routerProvider),
      scaffoldMessengerKey: ref.watch(rootMessengerKeyProvider),
      theme: buildAppTheme(Brightness.light, googleFonts: useGoogleFonts),
      darkTheme: buildAppTheme(Brightness.dark, googleFonts: useGoogleFonts),
      // Segue o sistema até o usuário escolher outra coisa no Perfil.
      themeMode: ref.watch(themeModeProvider),
      locale: const Locale('pt', 'BR'),
      supportedLocales: const [Locale('pt', 'BR')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      builder: (context, child) => NotificationHost(child: child ?? const SizedBox.shrink()),
    );
  }
}
