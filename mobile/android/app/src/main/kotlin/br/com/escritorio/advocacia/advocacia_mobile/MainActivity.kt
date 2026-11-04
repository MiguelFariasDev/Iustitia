package br.com.escritorio.advocacia.advocacia_mobile

import io.flutter.embedding.android.FlutterFragmentActivity

// FlutterFragmentActivity (e não FlutterActivity): o prompt de biometria do local_auth é um
// BiometricPrompt, que precisa de uma FragmentActivity.
class MainActivity : FlutterFragmentActivity()
