using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using NoLate.Services;
using NoLate.Models;
using Android.Runtime;

namespace NoLate.Platforms.Android;

[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
public class TrafficService : Service
{
    private bool _isRunning;
    private DatabaseService? _database;
    private YandexRouteService? _yandexService;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == "STOP")
        {
            StopServiceSafe();
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        StartForegroundServiceNotif();

        // Инициализация сервисов (убедись, что DatabaseService имеет пустой конструктор!)
        _database = new DatabaseService();
        _yandexService = new YandexRouteService();

        _isRunning = true;
        Task.Run(TrafficLoop);

        return StartCommandResult.Sticky;
    }

    private void StopServiceSafe()
    {
        // Используем старый метод с bool, так как он работает везде,
        // а новые флаги у тебя вызывают ошибку компиляции.
#pragma warning disable CA1416, CA1422
        StopForeground(true);
#pragma warning restore CA1416, CA1422
    }

    private void StartForegroundServiceNotif()
    {
        // ОТКЛЮЧАЕМ ПРОВЕРКУ Я ВУЗУАЛКУ РОТ ЕБАЛ ОН НЕ ВОЗРАШАЕТ НУЛЛ ЭНИВЕЙ
#pragma warning disable CS8602
#pragma warning disable CS8604

        string channelId = "nolate_traffic";
        Context context = this;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channelName = new Java.Lang.String("Мониторинг пробок");
#pragma warning disable CA1416 // Проверка совместимости платформы
            var channel = new NotificationChannel(channelId, channelName, NotificationImportance.Low);
#pragma warning restore CA1416 // Проверка совместимости платформы

            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
#pragma warning disable CA1416 // Проверка совместимости платформы
            notificationManager?.CreateNotificationChannel(channel);
#pragma warning restore CA1416 // Проверка совместимости платформы
        }

        int iconResId = global::Android.Resource.Drawable.IcMenuMyPlaces;

        // Теперь компилятор будет молчать
        var notificationBuilder = new NotificationCompat.Builder(context, channelId)
        .SetContentTitle("NoLate активен")
        .SetContentText("Слежу за пробками...")
        .SetSmallIcon(iconResId);

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



    private async Task TrafficLoop()
    {
        while (_isRunning)
        {
            try
            {
                if (_database == null || _yandexService == null)
                {
                    await Task.Delay(1000);
                    continue;
                }

                var alarms = await _database.GetAlarmsAsync();
                var activeAlarms = alarms.Where(a => a.IsActive && a.AlarmTime > DateTime.Now).ToList();

                if (activeAlarms.Any())
                {
                    var nextAlarm = activeAlarms.OrderBy(a => a.AlarmTime).First();
                    var timeLeft = nextAlarm.AlarmTime - DateTime.Now;

                    TimeSpan interval;
                    if (timeLeft.TotalHours > 3) interval = TimeSpan.FromHours(1);
                    else if (timeLeft.TotalHours > 1) interval = TimeSpan.FromMinutes(30);
                    else interval = TimeSpan.FromMinutes(10);

                    // Проверка координат на null перед использованием
                    if (nextAlarm.ToLat.HasValue && nextAlarm.ToLon.HasValue &&
                    nextAlarm.FromLat.HasValue && nextAlarm.FromLon.HasValue)
                    {
                        // Безопасное использование .Value
                        int? newMinutes = await _yandexService.GetTravelTimeMinutes(
                        nextAlarm.FromLat.Value, nextAlarm.FromLon.Value,
                        nextAlarm.ToLat.Value, nextAlarm.ToLon.Value);

                        if (newMinutes.HasValue && Math.Abs(nextAlarm.TravelTime - newMinutes.Value) > 5)
                        {
                            await UpdateAlarmSilence(nextAlarm, newMinutes.Value);
                        }
                    }

                    await Task.Delay(interval);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TrafficLoop error: {ex.Message}");
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
    }

    private async Task UpdateAlarmSilence(AlarmModel alarm, int newTravelMinutes)
    {
        if (_database == null) return;

        alarm.TravelTime = newTravelMinutes;
        var newAlarmTime = alarm.MestTime.AddMinutes(-(alarm.TravelTime + alarm.DopTime));

        if (newAlarmTime < DateTime.Now) newAlarmTime = DateTime.Now.AddMinutes(1);

        alarm.AlarmTime = newAlarmTime;
        await _database.SaveAlarmAsync(alarm);

        SetSystemAlarm(alarm.Id, newAlarmTime);
    }

    private void SetSystemAlarm(int id, DateTime time)
    {
        var am = (AlarmManager?)GetSystemService(AlarmService);
        if (am == null) return;

        var intent = new Intent(this, typeof(AlarmReceiver));
        intent.PutExtra("ALARM_ID", id);

        var pi = PendingIntent.GetBroadcast(this, id, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        if (pi == null) return;

        long triggerMs = new DateTimeOffset(time).ToUnixTimeMilliseconds();

#pragma warning disable CA1416
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            am.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMs, pi);
        }
        else
        {
            am.SetExact(AlarmType.RtcWakeup, triggerMs, pi);
        }
#pragma warning restore CA1416
    }
}

[BroadcastReceiver(Enabled = true, Exported = true)]
public class AlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        var id = intent?.GetIntExtra("ALARM_ID", 0) ?? 0;
        System.Diagnostics.Debug.WriteLine($"ALARM RINGING! ID: {id}");
    }
}