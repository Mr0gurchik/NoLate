using NoLate.Models;
using NoLate.Services;

namespace NoLate;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _database;

    public MainPage(DatabaseService database)
    {
        InitializeComponent();
        _database = database;
    }

    // 1. Важно: Обновляем список при появлении экрана
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAlarmsAsync();
    }

    // Загрузка списка из базы
    private async Task LoadAlarmsAsync()
    {
        try
        {
            var alarms = await _database.GetAlarmsAsync();
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
        await DisplayAlert("Добавить будильник",
            "Экран создания будет позже.\n" +
            "Создаём тестовый будильник.", "OK");

        var newAlarm = new AlarmModel()
        {
            Mesto = "Работа",
            MestTime = DateTime.Now.AddHours(3),
            AlarmTime = DateTime.Now.AddHours(2),
            TravelTime = 60,
            DopTime = 30,
            Transport = "Авто",
            IsActive = true
        };

        await _database.SaveAlarmAsync(newAlarm);
        await LoadAlarmsAsync();
    }

    // Клик по будильнику в списке
    private async void OnAlarmSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AlarmModel alarm)
        {
            var action = await DisplayActionSheet("Действия", "Отмена", null,
                "👁 Подробности", "🗑 Удалить");

            switch (action)
            {
                case "👁 Подробности":
                    await DisplayAlert("Будильник",
                        $"📍 {alarm.Mesto}\n" +
                        $"🏁 Прибыть: {alarm.MestTime:HH:mm}\n" +
                        $"⏰ Подъём: {alarm.AlarmTime:HH:mm}",
                        "OK");
                    break;

                case "🗑 Удалить":
                    var confirm = await DisplayAlert("Удалить?",
                        $"Удалить '{alarm.Mesto}'?", "Да", "Нет");

                    if (confirm)
                    {
                        await _database.DeleteAlarmAsync(alarm);
                        await LoadAlarmsAsync();
                        await DisplayAlert("Готово", "Будильник удалён", "OK");
                    }
                    break;
            }

            AlarmsCollection.SelectedItem = null; // Сброс выделения
        }
    }

    // Обработчик переключателя (чтобы не сбивались настройки)
    private async void OnSwitchToggled(object sender, ToggledEventArgs e)
    {
        if (sender is Switch switchControl && switchControl.BindingContext is AlarmModel alarm)
        {
            alarm.IsActive = e.Value;
            await _database.SaveAlarmAsync(alarm);
        }
    }

    private async void OnCounterClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Кнопка", "OnCounterClicked сработал", "OK");
    }
}
