#if ANDROID
using Android.App;
using Android.Content;
using Android.Gms.Common.Apis;
using Android.OS;

namespace NoLate.Platforms.Android;

public static class AlarmScheduler
{
    // Константы для интентов
    public const string ActionAlarmTriggered = "com.companyname.nolate.ALARM_TRIGGERED";
    public const string ExtraAlarmId = "ALARM_ID";

    // Планирует точный будильник на Android
    public static void Schedule(int id, DateTime time)
    {
        var context = global::Android.App.Application.Context;
        var am = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (am == null) return;

        // Android 12+ требует явного разрешения для точных будильников такчто доп проверка
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
#pragma warning disable CA1416
            if (!am.CanScheduleExactAlarms())
            {
                global::Android.Util.Log.Error("AlarmScheduler", $"Нет разрешения на точные будильники");
                return;
            }
#pragma warning restore CA1416
        }

        // Создаём интент для BroadcastReceiver
        var intent = new Intent(ActionAlarmTriggered);
        intent.SetPackage(context.PackageName);
        intent.PutExtra(ExtraAlarmId, id);

        // PendingIntent для broadcast(Immutable для Android 12 + там появились правила что нужно обязательно прописывать можно подменять или нет)
        var operation = PendingIntent.GetBroadcast
        (
            context,
            id,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
        );

        if (operation == null) return;

        // Преврашение DateTime в Java Calendar
        var cal = Java.Util.Calendar.Instance;
        cal.Set(time.Year, time.Month - 1, time.Day, time.Hour, time.Minute, 0);
        cal.Set(Java.Util.CalendarField.Millisecond, 0);
        long triggerMs = cal.TimeInMillis;

        // Устанавливаем будильник с включением экрана
#pragma warning disable CA1416
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? context.PackageName!);
        if (launch != null)
        {
            var showIntent = PendingIntent.GetActivity(
                context, 0, launch,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            am.SetAlarmClock(new AlarmManager.AlarmClockInfo(triggerMs, showIntent), operation);
        }
#pragma warning restore CA1416

        System.Diagnostics.Debug.WriteLine($"[AlarmScheduler] SET id={id} at {time} ms={triggerMs}");
    }

    // Отменяет будильник
    public static void Cancel(int id)
    {
        var context = global::Android.App.Application.Context;

        var intent = new Intent(ActionAlarmTriggered);
        intent.SetPackage(context.PackageName);

        var pi = PendingIntent.GetBroadcast
        (
            context,
            id,
            intent,
            PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable
        );

        pi?.Cancel();
        System.Diagnostics.Debug.WriteLine($"[AlarmScheduler] CANCEL id={id}");
    }
}
#endif
