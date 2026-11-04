import 'package:flutter/services.dart';
import 'package:share_plus/share_plus.dart';
import 'package:url_launcher/url_launcher.dart';

/// Ações que saem do app (compartilhar, abrir link, copiar). Atrás de uma interface para a
/// tela poder ser testada sem plugin nativo.
abstract interface class ExternalActions {
  Future<void> share({required String text, String? subject});
  Future<bool> openUrl(Uri uri);
  Future<void> copy(String text);
}

class PlatformExternalActions implements ExternalActions {
  @override
  Future<void> share({required String text, String? subject}) =>
      SharePlus.instance.share(ShareParams(text: text, subject: subject));

  @override
  Future<bool> openUrl(Uri uri) => launchUrl(uri, mode: LaunchMode.externalApplication);

  @override
  Future<void> copy(String text) => Clipboard.setData(ClipboardData(text: text));
}
