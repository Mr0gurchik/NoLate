using System.Globalization;
using System.Net.Http.Json;

namespace NoLate.Services;

public class YandexRouteService
{
    private readonly HttpClient _httpClient;
    private const string ApiKey = "ac766f34-ef44-442c-b82c-ea28a7755d65";

    public YandexRouteService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<int?> GetTravelTimeMinutes(double fromLat, double fromLon, double toLat, double toLon)
    {
        // 1. Формируем координаты (Lat,Lon)
        string origin = $"{fromLat.ToString(CultureInfo.InvariantCulture)},{fromLon.ToString(CultureInfo.InvariantCulture)}";
        string destination = $"{toLat.ToString(CultureInfo.InvariantCulture)},{toLon.ToString(CultureInfo.InvariantCulture)}";

        // 2. URL для Routing API Яндекса
        // waypoints=lat1,lon1|lat2,lon2
        // mode=driving - авто
        string url = $"https://api.routing.yandex.net/v2/route?waypoints={origin}|{destination}&mode=driving&apikey={ApiKey}";

        try
        {
            var response = await _httpClient.GetAsync(url);

            // Показываем реальную ошибку
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"Yandex Error {response.StatusCode}: {errorText}");
            }

            // Парсим JSON
            var data = await response.Content.ReadFromJsonAsync<YandexRouteResponse>();

            var route = data?.Route;

            // Приоритет: время с пробками, если нет — обычное время
            var duration = route?.DurationInTraffic ?? route?.Duration;

            if (duration.HasValue)
            {
                return (int)Math.Ceiling(duration.Value / 60.0); // Переводим секунды в минуты
            }
            else
            {
                throw new Exception("Маршрут построен, но время не найдено");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Сбой маршрута: {ex.Message}");
        }
        return null;
    }

    // Классы для парсинга JSON (Routing API возвращает свой формат)
    public class YandexRouteResponse
    {
        public RouteData? Route { get; set; }
    }

    public class RouteData
    {
        public double? Duration { get; set; } // Время без пробок (сек)
        public double? DurationInTraffic { get; set; } // Время с пробками (сек)
        public double? Distance { get; set; } // Расстояние (м)
    }
}