import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../domain/dashboard_data.dart';

final dashboardProvider = FutureProvider.autoDispose<DashboardData>(
  (ref) => ref.watch(dashboardRepositoryProvider).load(),
);
