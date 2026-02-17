using NoLate.Models;
using NoLate.Services;

namespace NoLate;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _database;
    private AlarmModel? _selectedAlarm;

    public MainPage(DatabaseService database)
    {
        InitializeComponent();
        _database = database;
    }

    // Обновляем список при появлении экрана
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
    private async void OnAlarmItemTapped(object sender, TappedEventArgs e)
    {
        var newAlarm = e.Parameter as AlarmModel;
        if (newAlarm == null) return;

        // Подсветка
        if (_selectedAlarm != null && _selectedAlarm != newAlarm)
            _selectedAlarm.IsSelected = false;

        newAlarm.IsSelected = true;
        _selectedAlarm = newAlarm;

        // Обновления каст меню
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
