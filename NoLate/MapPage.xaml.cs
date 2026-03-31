using Android.Webkit;
using Microsoft.Maui.Handlers;
using NoLate.Services;
using System.Globalization;
using System.Web;

namespace NoLate;

public partial class MapPage : ContentPage, IQueryAttributable
{
    public string? CurrentLat { get; set; }
    public string? CurrentLon { get; set; }
    public string? StartCurrentLat { get; set; }
    public string? StartCurrentLon { get; set; }
    private double? _userLat;
    private double? _userLon;

    // Конструктор настраивает WebView и сразу грузит карту
    public MapPage()
    {
        InitializeComponent(); // Загружает MapPage.xaml
        ModifyWebView(); // Включает JS + GPS в WebView
        GetUserLocationAndLoadMap(); // Получает GPS → грузит карту
    }

    // Настройка WebView для Android (только Android для яблака потом)
    void ModifyWebView()
    {
        MapWebView.HandlerChanged += (sender, e) =>
        {
#if ANDROID
            // Получаем доступ к родному Android WebView
            var webView = (Android.Webkit.WebView?)MapWebView.Handler?.PlatformView;
            if (webView != null)
            {
                var settings = webView.Settings;
                if (settings != null)
                {
                    settings.JavaScriptEnabled = true;                   // Включаем JavaScript
                    settings.SetGeolocationEnabled(true);                // GPS в браузере
                    webView.SetWebChromeClient(new MyWebChromeClient()); // Авто-разрешение GPS
                }
            }
#endif
        };
    }

#if ANDROID
    public class MyWebChromeClient : Android.Webkit.WebChromeClient
    {
        public override void OnGeolocationPermissionsShowPrompt(string? origin, Android.Webkit.GeolocationPermissions.ICallback? callback)
        {
            // Разрешаем ВСЕ запросы GPS
            callback?.Invoke(origin, true, false);
        }

        // Логируем все GPS запросы
        public override void OnGeolocationPermissionsHidePrompt() { }
    }
#endif
    // Тек позиция
    private void GetUserLocationAndLoadMap()
    {
        LoadMap();
    }

    // Загружает штмлку с параметрами
    private void LoadMap()
    {
        string url = "file:///android_asset/map.html";

        // В приоритете берем инфу из едита если её нет то берем из мап
        if (!string.IsNullOrEmpty(StartCurrentLat) && double.TryParse(StartCurrentLat, NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) && !string.IsNullOrEmpty(StartCurrentLon) && double.TryParse(StartCurrentLon, NumberStyles.Any, CultureInfo.InvariantCulture, out double lon))
        {
            url += $"?lat={lat:F6}&lon={lon:F6}";
        }
        else if (_userLat.HasValue && _userLon.HasValue)
        {
            url += $"?lat={_userLat.Value:F6}&lon={_userLon.Value:F6}";
        }

        MapWebView.Source = new UrlWebViewSource { Url = url };
    }

    // Ловим данные из штмлки
    private async void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        // Проверяем URL от Джавы
        if (e.Url != null && e.Url.StartsWith("invoke://data"))
        {
            e.Cancel = true;  // Отменяем реальную навигацию

            try
            {
                var uri = new Uri(e.Url);
                var query = HttpUtility.ParseQueryString(uri.Query);

                string? toLatStr = query["toLat"];
                string? toLonStr = query["toLon"];
                string toAddr = query["toAddr"] ?? "Адрес не определен";

                string? fromLatStr = query["fromLat"];
                string? fromLonStr = query["fromLon"];
                string fromAddr = query["fromAddr"] ?? "Текущая позиция";

                if (double.TryParse(toLatStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double toLat) &&
                    double.TryParse(toLonStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double toLon))
                {
                    var navigationParams = new Dictionary<string, object>
                    {
                        { "SelectedLat", toLat },
                        { "SelectedLon", toLon },
                        { "SelectedAddress", toAddr }
                    };

                    if (double.TryParse(fromLatStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double fromLat) &&
                        double.TryParse(fromLonStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double fromLon))
                    {
                        navigationParams.Add("FromLat", fromLat);
                        navigationParams.Add("FromLon", fromLon);
                        navigationParams.Add("FromAddress", fromAddr);
                    }

                    await Shell.Current.GoToAsync("..", navigationParams);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка карты", ex.Message, "ОК");
            }
        }
    }


    // класс для передачи параметров в мап
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("currentLat", out var latObj) &&
            query.TryGetValue("currentLon", out var lonObj) &&
            double.TryParse(latObj?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
            double.TryParse(lonObj?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double lon))
        {
            _userLat = lat;
            _userLon = lon;
        }
    }
}