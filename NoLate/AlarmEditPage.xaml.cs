using NoLate.Models;
using NoLate.Services;

namespace NoLate;

[QueryProperty(nameof(AlarmId), "Id")] // Принимаем ID при навигации
public partial class AlarmEditPage : ContentPage
{
    private readonly DatabaseService _database;
    private AlarmModel _currentAlarm;

    // Свойство для получения ID из URL навигации
    public string AlarmId
    {
        set
        {
            LoadAlarm(value);
        }
    }

    public AlarmEditPage(DatabaseService database)
    {
        InitializeComponent();
        _database = database;
        _currentAlarm = new AlarmModel();
        TransportPicker.SelectedIndex = 0;
        MestDatePicker.MinimumDate = DateTime.Today;

        this.Opacity = 0;
    }

    //кастомная анимка появления что не была резкой
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        this.TranslationX = screenWidth;
        this.Opacity = 1;
        base.OnAppearing();
        await this.TranslateTo(0, 0, 350, Easing.CubicOut);
    }

    // Загрузка буд при редакт
    private async void LoadAlarm(string id)
    {
        if (int.TryParse(id, out int alarmId))
        {
            var alarm = await _database.GetAlarmAsync(alarmId);
            if (alarm != null)
            {
                _currentAlarm = alarm;

                // Заполняем
                MestoEntry.Text = alarm.Mesto;
                MestTimePicker.Time = alarm.MestTime.TimeOfDay;
                TravelTimeEntry.Text = alarm.TravelTime.ToString();
                DopTimeEntry.Text = alarm.DopTime.ToString();
                TransportPicker.SelectedItem = alarm.Transport;
            }
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MestoEntry.Text))
        {
            await DisplayAlert("Ой!", "Напишите, куда едем", "OK");
            return;
        }

        // Сборка даты и времени
        DateTime selectedDate = MestDatePicker.Date;
        TimeSpan selectedTime = MestTimePicker.Time;

        // Получаем полное время прибытия
        DateTime fullDateTime = selectedDate.Date + selectedTime;

        // Проверка на чюдика:
        // Если выбранное время было, то переносим на "завтра"
        if (fullDateTime <= DateTime.Now)
        {
            // Добавляем 1 день к сегоднешней дате, сохраняя выбранное время
            fullDateTime = DateTime.Today.AddDays(1) + selectedTime;

            await DisplayAlert("Инфо", "Выбранное время уже прошло, перенесли на завтра.", "ОК");
        }

        // Парсинг остальных полей
        if (!int.TryParse(TravelTimeEntry.Text, out int travelTime)) travelTime = 0;
        if (!int.TryParse(DopTimeEntry.Text, out int dopTime)) dopTime = 0;


        //Дальше идет фикс того что пользователь может выставлять будильник при физической невозможности доехать до места
        DateTime departureTime = fullDateTime.AddMinutes(-travelTime); // Когда надо выйти
        DateTime alarmTime = departureTime.AddMinutes(-dopTime);       // Когда надо встать

        TimeSpan timeLeft = departureTime - DateTime.Now; // Сколько времени осталось до выхода

        // Сценарий 1: Мы уже физически не успеем доехать
        if (timeLeft.TotalMinutes < 0)
        {
            await DisplayAlert("Увы...",
                $"Вы не успеете приехать к {fullDateTime:HH:mm}!\n" +
                $"Дорога занимает {travelTime} мин, а осталось всего {Math.Max(0, (int)(fullDateTime - DateTime.Now).TotalMinutes)} мин.",
                "Жаль");
            return;
        }

        // Сценарий 2: Мы успеваем доехать, но на сборы времени нет
        if (alarmTime < DateTime.Now)
        {
            // Сколько реально осталось минут на сборы?
            int availableDopTime = (int)timeLeft.TotalMinutes;

            bool confirm = await DisplayAlert("Внимание!",
                $"Вы не успеете собраться за {dopTime} мин!\n" +
                $"Максимум на сборы есть: {availableDopTime} мин. (С такими настройками вы можите опаздать)\n\n" +
                "Сохранить с сокращенным временем?",
                "Да, сократить", "Нет, я перенесу время прибытия");

            if (!confirm) return;

            // Если "Да" - сокращаем сборы до возможного максимума
            dopTime = Math.Max(0, availableDopTime);
            // Пересчитываем время будильника
            alarmTime = DateTime.Now;
        }

        // Заполняем модель
        _currentAlarm.Mesto = MestoEntry.Text;
        _currentAlarm.MestTime = fullDateTime;
        _currentAlarm.TravelTime = travelTime;
        _currentAlarm.DopTime = dopTime;
        _currentAlarm.Transport = TransportPicker.SelectedItem?.ToString() ?? "Авто";
        _currentAlarm.IsActive = true;

        // Расчет времени
        _currentAlarm.AlarmTime = _currentAlarm.MestTime.AddMinutes(-travelTime - dopTime);

        // Сохранение
        await _database.SaveAlarmAsync(_currentAlarm);
        double screenWidth = this.Width;
        await this.TranslateTo(screenWidth, 0, 300, Easing.CubicIn);
        await Navigation.PopAsync(animated: false);
    }

    //кнопка отмены
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        double screenWidth = this.Width;
        await this.TranslateTo(screenWidth, 0, 300, Easing.CubicIn);
        await Navigation.PopAsync(animated: false);
    }

    private void OnTimeEntryTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is Entry entry)
        {
            if (string.IsNullOrEmpty(entry.Text)) return;

            // Ток цифры
            string newText = new string(entry.Text.Where(char.IsDigit).ToArray());

            // Если в тексте были запрещенные символы (точка, минус, буква),
            // то newText будет отличаться от entry.Text
            if (entry.Text != newText)
            {
                entry.Text = newText;
            }
        }
    }
}
