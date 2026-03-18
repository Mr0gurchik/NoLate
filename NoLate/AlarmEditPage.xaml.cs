using NoLate.Models;
using NoLate.Services;
using System.Globalization;

namespace NoLate;

[QueryProperty(nameof(CurrentLat), "currentLat")]
[QueryProperty(nameof(CurrentLon), "currentLon")]
[QueryProperty(nameof(AlarmId), "Id")] // Принимаем ID при навигации
public partial class AlarmEditPage : ContentPage, IQueryAttributable
{
    public string? CurrentLat { get; set; }
    public string? CurrentLon { get; set; }
    private double _selectedLat;
    private double _selectedLon;
    private string? _selectedAddress;
    private readonly OTPRouteService routeService;
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

    // Метод получения данных при возврате с карты
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Проверяем, пришли ли вообще данные
        if (query.ContainsKey("SelectedAddress"))
        {
            _selectedAddress = query["SelectedAddress"]?.ToString();

            // Если адрес пустой, ставим заглушку
            if (string.IsNullOrWhiteSpace(_selectedAddress))
                _selectedAddress = "Точка на карте";

            if (query.ContainsKey("SelectedLat") && query.ContainsKey("SelectedLon"))
            {
                _selectedLat = (double)query["SelectedLat"];
                _selectedLon = (double)query["SelectedLon"];
            }

            // Обновляем Label
            AddressLabel.Text = $"📍 {_selectedAddress}";

            // Если поле "Название" пустое, можем сразу подставить туда адрес (опционально)
            if (string.IsNullOrWhiteSpace(MestoEntry.Text))
            {
                // Берем первое слово или весь адрес
                MestoEntry.Text = _selectedAddress.Split(',')[0];
            }
        }
    }


    //База данных
    public AlarmEditPage(DatabaseService database)
    {
        InitializeComponent();
        _database = database;
        routeService = new OTPRouteService();
        _currentAlarm = new AlarmModel();
        TransportPicker.SelectedIndex = 0;
        MestDatePicker.MinimumDate = DateTime.Today;

        this.Opacity = 0;
    }

    //Кастомная анимка появления что не была резкой
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ = GetCurrentLocationAsync();
        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        this.TranslationX = screenWidth;
        this.Opacity = 1;
        base.OnAppearing();
        await this.TranslateTo(0, 0, 350, Easing.CubicOut);
    }

    private async Task GetCurrentLocationAsync()
    {
        try
        {
            var location = await LocationHelper.GetCurrentLocation();
            if (location != null)
            {
                CurrentLat = location.Latitude.ToString(CultureInfo.InvariantCulture);
                CurrentLon = location.Longitude.ToString(CultureInfo.InvariantCulture);
                System.Diagnostics.Debug.WriteLine($"GPS готов {CurrentLat}, {CurrentLon}");
            }
        }
        catch {} // здесь ниче ни ловим при ошибке нас просто перекинет в центр москвы (для нас то что gps тупит это норма уже года 4)
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

                // Заполняем поля
                MestoEntry.Text = alarm.Mesto;
                MestDatePicker.Date = alarm.MestTime.Date;
                MestTimePicker.Time = alarm.MestTime.TimeOfDay;
                DopTimeEntry.Text = alarm.DopTime.ToString();
                TransportPicker.SelectedItem = alarm.Transport;

                // Восстанавливаем данные о месте
                _selectedLat = alarm.ToLat ?? 0;
                _selectedLon = alarm.ToLon ?? 0;
                // Восстанавливаем адрес (если он не сохранен отдельно, пишем заглушку или берем название)
                _selectedAddress = alarm.Mesto;
                // Обновляем метку адреса
                AddressLabel.Text = _selectedLat != 0
                ? "Координаты сохранены"
                : "Адрес не выбран";
            }
        }
    }

    // Кнопка открытия карты
    private async void OnOpenMapClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(MapPage));
    }

    // Кнопка сохранения
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_selectedLat == 0 && _selectedLon == 0)
        {
            await DisplayAlert("Ошибка", "Пожалуйста, выберите место назначения на карте", "ОК");
            return;
        }

        LoadingOverlay.IsVisible = true;

        if (string.IsNullOrWhiteSpace(MestoEntry.Text))
        {
            MestoEntry.Text = !string.IsNullOrEmpty(_selectedAddress) ? _selectedAddress : "Поездка";
        }

        // Получаем текущ позицию
        var location = await LocationHelper.GetCurrentLocation();
        if (location == null)
        {
            await DisplayAlert("Ошибка GPS", "Не удалось определить ваше местоположение. Включите GPS!", "ОК");
            return;
        }

        // Запрос на расчет пути к OTP
        int travelTime = 0;
        try
        {
            // Берем выбранный транспорт или по умолчанию "Пешком"
            string selectedTransport = TransportPicker.SelectedItem?.ToString() ?? "Пешком";

            // Передаем координаты и выбранный транспорт в наш OTP сервис
            int? routeTime = await routeService.GetTravelTimeMinutes
            (
                location.Latitude, location.Longitude,
                _selectedLat, _selectedLon,
                selectedTransport
            );

            if (routeTime.HasValue)
            {
                travelTime = routeTime.Value;
            }
            else
            {
                LoadingOverlay.IsVisible = false;
                await DisplayAlert("Ошибка маршрута", "Не удалось построить маршрут. Проверьте интернет.", "ОК");
                return;
            }
        }
        catch (Exception ex)
        {
            LoadingOverlay.IsVisible = false;
            await DisplayAlert("Ошибка", $"Сбой при расчете: {ex.Message}", "ОК");
            return;
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
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
        if (!int.TryParse(DopTimeEntry.Text, out int dopTime)) dopTime = 0;


        //Дальше идет фикс того что пользователь может выставлять будильник при физической невозможности доехать до места
        DateTime departureTime = fullDateTime.AddMinutes(-travelTime); // Когда надо выйти
        DateTime alarmTime = departureTime.AddMinutes(-dopTime); // Когда надо встать

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
            $"Вы не успеете собраться за {dopTime} мин!\\n" +
            $"Максимум на сборы есть: {availableDopTime} мин. (С такими настройками вы можите опаздать)\\n\\n" +
            "Сохранить с сокращенным временем?",
            "Да, сократить", "Нет, я перенесу время прибытия");

            if (!confirm) return;

            // Если "Да" сокращаем сборы до возможного максимума
            dopTime = Math.Max(0, availableDopTime);
            // Пересчитываем время будильника
            alarmTime = DateTime.Now;
        }

        // Заполняем модель
        _currentAlarm.Mesto = MestoEntry.Text;
        _currentAlarm.MestTime = fullDateTime;
        _currentAlarm.TravelTime = travelTime;
        _currentAlarm.DopTime = dopTime;
        _currentAlarm.Transport = TransportPicker.SelectedItem?.ToString() ?? "Пешком";
        _currentAlarm.IsActive = true;
        _currentAlarm.ToLat = _selectedLat;
        _currentAlarm.ToLon = _selectedLon;
        _currentAlarm.FromLat = location.Latitude;
        _currentAlarm.FromLon = location.Longitude;

        // Расчет времени
        _currentAlarm.AlarmTime = alarmTime;

        // Сохранение
        await _database.SaveAlarmAsync(_currentAlarm);
#if ANDROID
        Android.Util.Log.Info("NoLate", $"SAVE: id={_currentAlarm.Id} alarmTime={_currentAlarm.AlarmTime:O}");
#endif
#if ANDROID
        NoLate.Platforms.Android.AlarmScheduler.Schedule(_currentAlarm.Id, _currentAlarm.AlarmTime);
#endif
        LoadingOverlay.IsVisible = false;
        await DisplayAlert("Маршрут построен", $"Ехать: {travelTime} мин\nСборы: {dopTime} мин\nБудильник на: {alarmTime:HH:mm}", "ОК");
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

    //Метод для запрета ввода хрени всякой
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