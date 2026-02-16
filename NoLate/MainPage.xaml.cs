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
        await Shell.Current.GoToAsync(nameof(AlarmEditPage));
    }


    // Клик по будильнику в списке
    private async void OnAlarmSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AlarmModel alarm)
        {
            string action = await DisplayActionSheet($"Будильник: {alarm.Mesto}", "Отмена", null, "Редактировать", "Удалить");

            if (action == "Редактировать")
            {
                await Shell.Current.GoToAsync($"{nameof(AlarmEditPage)}?Id={alarm.Id}");
            }
            else if (action == "Удалить")
            {
                bool confirm = await DisplayAlert("Удаление", $"Удалить {alarm.Mesto}?", "Да", "Нет");
                if (confirm)
                {
                    await Task.Run(() => _database.DeleteAlarmAsync(alarm));
                    await Task.Delay(50);
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await LoadAlarmsAsync();
                    });
                }
            }

            AlarmsCollection.SelectedItem = null;
        }
    }

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
