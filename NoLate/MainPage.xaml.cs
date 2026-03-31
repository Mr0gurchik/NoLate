using NoLate.Models;
using NoLate.Services;

namespace NoLate;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _database;
    private AlarmModel? _selectedAlarm;
    private bool _startedOnce;

    public MainPage(DatabaseService database)
    {
        InitializeComponent();
        _database = database;

#pragma warning disable CS0618
        // Ловим сигнал об отключении будильника
        Microsoft.Maui.Controls.MessagingCenter.Subscribe<object>(this, "UpdateAlarms", async (sender) =>
        {
            await Task.Delay(1000);
            await LoadAlarmsAsync();
        });
#pragma warning restore CS0618
    }

    // Обновляем список при появлении экрана
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        //  Сначала спросить разрешение на уведомления (ток для Android 13+)
        await RequestNotificationPermission();

        // Запросить права на будильники (Android 12+)
        await RequestExactAlarmPermission();

        // Потом стартануть foreground service (1 раз)
        if (!_startedOnce)
        {
            _startedOnce = true;
            StartBackgroundService();
        }

        // 3) Затем загрузить список
        await LoadAlarmsAsync();
    }

    // Загрузка списка из базы
    private async Task LoadAlarmsAsync()
    {
        try
        {
            var alarms = await _database.GetAlarmsAsync();
            bool isUpdated = false;

            foreach (var alarm in alarms)
            {
                // Если будильник включен, но его время прошло то вырубаем свич
                if (alarm.IsActive && alarm.AlarmTime < DateTime.Now)
                {
                    alarm.IsActive = false;
                    await _database.SaveAlarmAsync(alarm);
                    isUpdated = true;
                }
            }

            // Если были авто-отключения, перезапрашиваем актуальный список
            if (isUpdated) alarms = await _database.GetAlarmsAsync();

            AlarmsCollection.ItemsSource = alarms;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка списка", ex.Message, "OK");
        }
    }

    // Кнопка добавления
    private async void OnAddClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AlarmEditPage));
    }


    // Клик по будильнику в списке
    private async void OnAlarmItemTapped(object sender, TappedEventArgs e)
    {
        var newAlarm = e.Parameter as AlarmModel;
        if (newAlarm == null) return;

        // Подсветка
        if (_selectedAlarm != null && _selectedAlarm != newAlarm)
            _selectedAlarm.IsSelected = false;

        newAlarm.IsSelected = true;
        _selectedAlarm = newAlarm;

        // Обновления кастом меню
        MenuTitleLabel.Text = _selectedAlarm.Mesto;

        // Вывод меню
        ActionMenuOverlay.IsVisible = true;
        await ActionMenuOverlay.FadeTo(1, 150);
    }

    // Редактирование
    private async void OnEditMenuClicked(object sender, EventArgs e)
    {
        await CloseMenu();
        if (_selectedAlarm != null)
        {
            await Shell.Current.GoToAsync($"{nameof(AlarmEditPage)}?Id={_selectedAlarm.Id}");

            await Task.Delay(100);
            // Снимаем выделение
            _selectedAlarm.IsSelected = false;
            _selectedAlarm = null;
        }
    }

    // Удаление
    private async void OnDeleteMenuClicked(object sender, EventArgs e)
    {
        await CloseMenu();

        if (_selectedAlarm != null)
        {
            bool confirm = await DisplayAlert("Удаление", $"Точно удалить {_selectedAlarm.Mesto}?", "Да", "Нет");
            if (confirm)
            {
#if ANDROID
                NoLate.Platforms.Android.AlarmScheduler.Cancel(_selectedAlarm.Id);
#endif

                await _database.DeleteAlarmAsync(_selectedAlarm);
                _selectedAlarm = null;
                await LoadAlarmsAsync();
            }
        }
    }


    private async void OnOverlayTapped(object sender, EventArgs e)
    {
        if (_selectedAlarm != null)
        {
            _selectedAlarm.IsSelected = false;
            _selectedAlarm = null;
        }

        await CloseMenu();
    }

    // Метод для закрытия
    private async Task CloseMenu()
    {
        await ActionMenuOverlay.FadeTo(0, 150);
        ActionMenuOverlay.IsVisible = false;
    }

    // Свич будильника
    private async void OnSwitchToggled(object sender, ToggledEventArgs e)
    {
        if (sender is Switch switchControl && switchControl.BindingContext is AlarmModel alarm)
        {
            // Если пытаемся включить будильник
            if (e.Value)
            {
                // Проверяем не прошло ли уже время этого будильника
                if (alarm.AlarmTime <= DateTime.Now)
                {
                    // Переводим время срабатывания на 1 день вперед
                    alarm.AlarmTime = alarm.AlarmTime.AddDays(1);
                    alarm.MestTime = alarm.MestTime.AddDays(1);

                    await DisplayAlert("Инфо", "Выбранное время уже прошло, перенесли на завтра.", "ОК");
                }
            }

            // Синхронизируем состояние (на случай если биндинг отработал не до конца
            alarm.IsActive = e.Value;

            // Сохраняем изменения в базу
            await _database.SaveAlarmAsync(alarm);

#if ANDROID
            if (!alarm.IsActive)
            {
                NoLate.Platforms.Android.AlarmScheduler.Cancel(alarm.Id);
                global::Android.Util.Log.Error("Alarm", $"Будильник офнут");
            }
            else
            {
                NoLate.Platforms.Android.AlarmScheduler.Schedule(alarm.Id, alarm.AlarmTime);
                global::Android.Util.Log.Error("Alarm", $"Будильник включен");
            }
#endif
            alarm.OnPropertyChanged(nameof(alarm.AlarmTime));
            alarm.OnPropertyChanged(nameof(alarm.MestTime));
            alarm.OnPropertyChanged(nameof(alarm.IsActive));
        }
    }

    // Старт фонового сервиса
    private void StartBackgroundService()
    {
#if ANDROID
        var context = Android.App.Application.Context;
        var intent = new Android.Content.Intent(context, typeof(NoLate.Platforms.Android.TrafficService));

        // Для новых версий Android обязательно использовать StartForegroundService
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
        {
#pragma warning disable CA1416 // Проверка совместимости платформы
            context.StartForegroundService(intent);
#pragma warning restore CA1416 // Проверка совместимости платформы
        }
        else
        {
            context.StartService(intent);
        }
#endif
    }

    //Метод запроса разрешения на уведомления
    private async Task RequestNotificationPermission()
    {
        if (DeviceInfo.Version.Major >= 13)
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
        }
    }

    //Метод запроса разрешения на будильники
    private async Task RequestExactAlarmPermission()
    {
#if ANDROID
        // Проверяем, что это Android 12 (API 31) или выше
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
        {
#pragma warning disable CA1416
            var alarmManager = (Android.App.AlarmManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.AlarmService);

            // Если разрешение на точные будильники еще не выдано
            if (alarmManager != null && !alarmManager.CanScheduleExactAlarms())
            {
                bool answer = await DisplayAlert
                (
                    "Требуется разрешение",
                    "Для точного срабатывания будильника приложению нужно специальное разрешение. Пожалуйста, включите его на следующем экране.",
                    "Перейти в настройки",
                    "Отмена"
                );

                if (answer)
                {
                    // Открываем специальный системный экран настроек для нашего приложения
                    var intent = new Android.Content.Intent(Android.Provider.Settings.ActionRequestScheduleExactAlarm);
                    intent.SetData(Android.Net.Uri.Parse("package:" + Android.App.Application.Context.PackageName));
                    intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                    Android.App.Application.Context.StartActivity(intent);
                }
            }
#pragma warning restore CA1416
        }
#endif
    }

}