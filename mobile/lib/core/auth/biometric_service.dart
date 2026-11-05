import 'package:flutter/services.dart';
import 'package:local_auth/local_auth.dart';

/// Biometria do aparelho (Face ID, Touch ID, impressão digital).
abstract interface class BiometricService {
  Future<bool> isAvailable();

  /// `true` só se o usuário se autenticou. Cancelar ou falhar devolve `false` — nunca lança.
  Future<bool> authenticate(String reason);
}

class LocalAuthBiometricService implements BiometricService {
  LocalAuthBiometricService([LocalAuthentication? auth]) : _auth = auth ?? LocalAuthentication();

  final LocalAuthentication _auth;

  @override
  Future<bool> isAvailable() async {
    try {
      return await _auth.isDeviceSupported() && (await _auth.getAvailableBiometrics()).isNotEmpty;
    } on PlatformException {
      return false;
    }
  }

  @override
  Future<bool> authenticate(String reason) async {
    try {
      return await _auth.authenticate(localizedReason: reason, biometricOnly: true);
    } on PlatformException {
      return false;
    } on LocalAuthException {
      return false;
    }
  }
}
