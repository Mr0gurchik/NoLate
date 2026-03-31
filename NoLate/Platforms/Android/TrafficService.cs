using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using NoLate.Services;
using NoLate.Models;
using Android.Runtime;

namespace NoLate.Platforms.Android;

// Объявляем службу как Foreground (работает на переднем плане) чтоб Android не убил её для экономии батареи.
[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
public class TrafficService : Service
{
    private bool _isRunning;
    private DatabaseService? _database;
    private OTPRouteService? _routeService;
    private global::Android.Media.MediaPlayer? _mediaPlayer;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForegroundServiceNotif();

        // Обработка команд будильника
        if (intent?.Action == "RING")
        {
            int alarmId = intent.GetIntExtra("ALARM_ID", 0);
            RingAlarm(alarmId);
            return StartCommandResult.Sticky;
        }

        if (intent?.Action == "STOP_RING")
        {
            StopRinging();
            return StartCommandResult.Sticky;
        }

        if (intent?.Action == "STOP")
        {
            StopServiceSafe();
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        // Инициализация сервиса мониторинга
        _database = new DatabaseService();
        _routeService = new OTPRouteService();
        _isRunning = true;
        Task.Run(TrafficLoop);

        return StartCommandResult.Sticky;
    }

    // Проигрывание звука будильника и уведомление
    private void RingAlarm(int id)
    {
        try
        {
            // Проигрываем системный звук будильника
            var alarmUri = global::Android.Media.RingtoneManager.GetDefaultUri(global::Android.Media.RingtoneType.Alarm);
            if (_mediaPlayer == null)
            {
                // Создаем пустой плеер
                _mediaPlayer = new global::Android.Media.MediaPlayer();

                // Назначаем канал будильника до загрузки звука (эти выеживания нужны чтоб у меня звук зависил от звука будильника а не от общего. Если зарание не создать пустой с этими настройками то будильник сразу возьмет обшие настройки звука)
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    _mediaPlayer.SetAudioAttributes(new global::Android.Media.AudioAttributes.Builder()
                        ?.SetUsage(global::Android.Media.AudioUsageKind.Alarm)
                        ?.SetContentType(global::Android.Media.AudioContentType.Sonification)
                        ?.Build());
                }
                else
                {
#pragma warning disable CA1422
                    _mediaPlayer.SetAudioStreamType(global::Android.Media.Stream.Alarm);
#pragma warning restore CA1422
                }

                // Устанавливаем источник звука и подготавливаем плеер вручную
                if (alarmUri != null)
                {
                    _mediaPlayer.SetDataSource(this, alarmUri);
                    _mediaPlayer.Prepare();
                    _mediaPlayer.Looping = true;
                }
            }
            _mediaPlayer?.Start();

            // Показываем уведомление с кнопкой выключения
            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            string channelId = "nolate_alarm_channel";

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
#pragma warning disable CA1416
                var channel = new NotificationChannel(channelId, "Будильник", NotificationImportance.High);
                notificationManager?.CreateNotificationChannel(channel);
#pragma warning restore CA1416
            }

            // Интент для кнопки выключения звука
            var stopIntent = new Intent(this, typeof(TrafficService));
            stopIntent.SetAction("STOP_RING");
            var stopPendingIntent = PendingIntent.GetService(this, id, stopIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            // Интент для смахивания
            var deleteIntent = new Intent(this, typeof(TrafficService));
            deleteIntent.SetAction("STOP_RING");
            var deletePendingIntent = PendingIntent.GetService(this, id + 10000, deleteIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            // Интент для открытия приложения при тапе по уведомлению или пробуждении экрана
            var appIntent = new Intent(this, typeof(MainActivity));
            appIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop | ActivityFlags.NewTask);
            var appPendingIntent = PendingIntent.GetActivity(this, 0, appIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

#pragma warning disable CS8604, CS8602
            var builder = new NotificationCompat.Builder(this, channelId)
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentTitle("Будильник от NoLate")
                .SetContentText("Пора выходить!")
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetCategory(NotificationCompat.CategoryAlarm)
                .SetContentIntent(appPendingIntent)
                .SetFullScreenIntent(appPendingIntent, true)
                .AddAction(global::Android.Resource.Drawable.IcDelete, "Отключить", stopPendingIntent)
                .SetDeleteIntent(deletePendingIntent)
                .SetOngoing(false);

            notificationManager?.Notify(id + 1000, builder.Build());
#pragma warning restore CS8604, CS8602

            // Удаляем будильник из бд или переносим
            Task.Run(async () =>
            {
                if (_database != null)
                {
                    var alarm = await _database.GetAlarmAsync(id);
                    if (alarm != null)
                    {
                        if (!string.IsNullOrEmpty(alarm.RepeatingDays))
                        {
                            // Парсим дни из базы
                            var allowedDays = alarm.RepeatingDays.Split(',').Select(int.Parse).ToList();

                            // Ищем через сколько дней будет следующее срабатывание
                            int daysToAdd = 1;
                            while (!allowedDays.Contains((int)alarm.MestTime.AddDays(daysToAdd).DayOfWeek))
                            {
                                daysToAdd++;
                            }

                            // Сдвигаем прибытие и сам будильник на найденное количество дней
                            alarm.MestTime = alarm.MestTime.AddDays(daysToAdd);
                            alarm.AlarmTime = alarm.AlarmTime.AddDays(daysToAdd);

                            await _database.SaveAlarmAsync(alarm);

                            // Переназначаем таймер
                            SetSystemAlarm(alarm.Id, alarm.AlarmTime);
                        }
                        // Иначе будильнику хана
                        else
                        {
                            await _database.DeleteAlarmAsync(alarm);
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка проигрывания будильника: {ex.Message}");
        }
    }


    private void StopRinging()
    {
        // Останавливаем звук и убираем уведомление
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Release();
            _mediaPlayer = null;
        }
        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.CancelAll();
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
#pragma warning disable CS0618
            Microsoft.Maui.Controls.MessagingCenter.Send<object>(this, "UpdateAlarms");
#pragma warning restore CS0618
        });
    }

    // Остановка foreground режима
    private void StopServiceSafe()
    {
#pragma warning disable CA1416, CA1422
        StopForeground(true);
#pragma warning restore CA1416, CA1422
    }

    // Уведомление для foreground сервиса
    private void StartForegroundServiceNotif()
    {
#pragma warning disable CS8602
#pragma warning disable CS8604

        string channelId = "nolate_traffic";
        Context context = this;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channelName = new Java.Lang.String("Приложение активно");
#pragma warning disable CA1416
            var channel = new NotificationChannel(channelId, channelName, NotificationImportance.Low);
#pragma warning restore CA1416

            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
#pragma warning disable CA1416
            notificationManager?.CreateNotificationChannel(channel);
#pragma warning restore CA1416
        }

        int iconResId = global::Android.Resource.Drawable.IcMenuMyPlaces;

        var notificationBuilder = new NotificationCompat.Builder(context, channelId)
        .SetContentTitle("NoLate активен")
        .SetContentText("Слежу за пробками...")
        .SetSmallIcon(Resource.Mipmap.appicon);

        var notification = notificationBuilder.Build();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            StartForeground(1001, notification);
        }
        else
        {
#pragma warning disable CA1416
            StartForeground(1001, notification);
#pragma warning restore CA1416
        }

#pragma warning restore CS8602
#pragma warning restore CS8604
    }


    // Главный цикл мониторинга
    private async Task TrafficLoop()
    {
        while (_isRunning)
        {
            try
            {
                if (_database == null || _routeService == null)
                {
                    await Task.Delay(1000);
                    continue;
                }

                // Достаем из бд все активные будильники, которые еще не прозвенели
                var alarms = await _database.GetAlarmsAsync();
                var activeAlarms = alarms.Where(a => a.IsActive && a.AlarmTime > DateTime.Now).ToList();

                if (activeAlarms.Any())
                {
                    // Берем самый ближайший будильник
                    var nextAlarm = activeAlarms.OrderBy(a => a.AlarmTime).First();
                    var timeLeft = nextAlarm.AlarmTime - DateTime.Now;

                    TimeSpan interval;
                    if (timeLeft.TotalHours > 3) interval = TimeSpan.FromHours(1);
                    else if (timeLeft.TotalHours > 1) interval = TimeSpan.FromMinutes(30);
                    else interval = TimeSpan.FromMinutes(10);

                    // Проверяем, есть ли координаты вообще
                    if (nextAlarm.ToLat.HasValue && nextAlarm.ToLon.HasValue && nextAlarm.FromLat.HasValue && nextAlarm.FromLon.HasValue)
                    {
                        // Фоновый пинг к OTPRouteService
                        int? newMinutes = await _routeService.GetTravelTimeMinutes
                        (
                            nextAlarm.FromLat.Value, nextAlarm.FromLon.Value,
                            nextAlarm.ToLat.Value, nextAlarm.ToLon.Value,
                            nextAlarm.Transport ?? "foot"
                        );

                        // Если время пути изменилось больше чем на 5 минут, сдвигаем будильник
                        if (newMinutes.HasValue && Math.Abs(nextAlarm.TravelTime - newMinutes.Value) > 5)
                        {
                            await UpdateAlarmSilence(nextAlarm, newMinutes.Value);
                        }
                    }

                    await Task.Delay(interval);
                }
                else
                {
                    // Если будильников нет, засыпаем на час
                    await Task.Delay(TimeSpan.FromHours(1));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ошибка trafficLoop: {ex.Message}");
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
    }

    // Сдвиг времени будильника
    private async Task UpdateAlarmSilence(AlarmModel alarm, int newTravelMinutes)
    {
        if (_database == null) return;

        // Пересчитываем во сколько нужно быть (Новое время в пути + Время на сборы)
        alarm.TravelTime = newTravelMinutes;
        var newAlarmTime = alarm.MestTime.AddMinutes(-(alarm.TravelTime + alarm.DopTime));

        // Если пересчет показал, что мы уже опоздали ставим будильник через 1 минуту
        if (newAlarmTime < DateTime.Now) newAlarmTime = DateTime.Now.AddMinutes(1);

        alarm.AlarmTime = newAlarmTime;
        await _database.SaveAlarmAsync(alarm);

        // Отдаем новое время системному менеджеру будильников Android
        SetSystemAlarm(alarm.Id, newAlarmTime);
    }

    // Установка системного будильника Android
    private void SetSystemAlarm(int id, DateTime time)
    {
        var am = (AlarmManager?)GetSystemService(AlarmService);
        if (am == null) return;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
#pragma warning disable CA1416
            if (!am.CanScheduleExactAlarms())
            {
                System.Diagnostics.Debug.WriteLine("ОШИБКА: Нет прав на точный будильник!");
                return;
            }
#pragma warning restore CA1416
        }

        // Интент для AlarmReceiver
        var intent = new Intent("com.companyname.nolate.ALARM_TRIGGERED");

        // Указываем пакет для Android 14+ (гарантирует доставку интента на новых версиях)
        if (ApplicationContext != null && !string.IsNullOrEmpty(ApplicationContext.PackageName))
        {
            intent.SetPackage(ApplicationContext.PackageName);
        }

        // ID будильника для AlarmReceiver
        intent.PutExtra("ALARM_ID", id);

        // Приоритет для ресивера (Android 14+)
        intent.AddFlags(ActivityFlags.ReceiverForeground);

        var pi = PendingIntent.GetBroadcast
        (
            this,
            id,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
        );

        if (pi == null) return;

        // Java Calendar способ получить миллисекунды для AlarmManager, который учитывает часовой пояс устройства
        var javaCal = Java.Util.Calendar.Instance;
        if (javaCal != null)
        {
            javaCal.Set(time.Year, time.Month - 1, time.Day, time.Hour, time.Minute, 0);
            javaCal.Set(Java.Util.CalendarField.Millisecond, 0);
        }
        long triggerMs = javaCal?.TimeInMillis ?? new DateTimeOffset(time).ToUnixTimeMilliseconds();

#pragma warning disable CA1416
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
        {
            // SetAlarmClock (иконка в статус-баре + FullScreenIntent)
            var alarmClockInfo = new AlarmManager.AlarmClockInfo(triggerMs, pi);
            am.SetAlarmClock(alarmClockInfo, pi);
        }
        else if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            // Doze Mode совместимость
            am.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMs, pi);
        }
        else
        {
            am.SetExact(AlarmType.RtcWakeup, triggerMs, pi);
        }
#pragma warning restore CA1416
    }


}

// BroadcastReceiver для будильников
[BroadcastReceiver(Name = "com.companyname.nolate.AlarmReceiver", Enabled = true, Exported = true)]
[IntentFilter(new[] { "com.companyname.nolate.ALARM_TRIGGERED" })]
public class AlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;

        var id = intent?.GetIntExtra("ALARM_ID", 0) ?? 0;

        var serviceIntent = new Intent(context, typeof(TrafficService));
        serviceIntent.SetAction("RING");
        serviceIntent.PutExtra("ALARM_ID", id);

        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
        {
#pragma warning disable CA1416
            context.StartForegroundService(serviceIntent);
#pragma warning restore CA1416
        }
        else
        {
            context.StartService(serviceIntent);
        }
    }
}
