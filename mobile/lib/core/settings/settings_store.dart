import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Preferências do usuário neste aparelho. Nada aqui é segredo (tema, flags): vão em
/// `shared_preferences`; credenciais ficam no armazenamento seguro (ver SecureLocalStorage).
class SettingsStore {
  SettingsStore(this._prefs);

  final SharedPreferences _prefs;

  static const _themeKey = 'settings.theme_mode';
  static const _biometricKey = 'settings.biometric_lock';
  static const _remindersKey = 'settings.deadline_reminders';

  ThemeMode get themeMode => switch (_prefs.getString(_themeKey)) {
        'light' => ThemeMode.light,
        'dark' => ThemeMode.dark,
        // "system" é o padrão: segue o aparelho até o usuário escolher outra coisa.
        _ => ThemeMode.system,
      };

  Future<void> setThemeMode(ThemeMode mode) => _prefs.setString(_themeKey, mode.name);

  bool get biometricLock => _prefs.getBool(_biometricKey) ?? false;
  Future<void> setBiometricLock(bool value) => _prefs.setBool(_biometricKey, value);

  bool get deadlineReminders => _prefs.getBool(_remindersKey) ?? false;
  Future<void> setDeadlineReminders(bool value) => _prefs.setBool(_remindersKey, value);
}
