using Android.Gms.Common.Apis;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NoLate.Services;

public class OTPRouteService
{
    private readonly HttpClient _httpClient;

    // Путь до сервера и кей от томтом
    private const string ServerUrl = "https://nolate-otp.shares.zrok.io/otp/routers/default/index/graphql";
    private const string TomTomApiKey = "ZCLN9Kl1vbJz5FFF5txu5tJSGceLs4I3";

    public OTPRouteService()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            // Игнорим ошибки SSL для самоподписанных сертификатов на VPS чтоб приложение не ругалось что сайт не безопасен
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };

        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    // Определяет тип транспорта и вызывает нужный API
    public async Task<int?> GetTravelTimeMinutes(double fromLat, double fromLon, double toLat, double toLon, string transportType = "Пешком 🚶")
    {
        // 1. Создаем переменную с маршрутом по умолчанию (сразу с нужными скобками для GraphQL)
        string transportModesStr = "[{mode: WALK}]";

        if (!string.IsNullOrEmpty(transportType))
        {
            // Если Авто - уходим в TomTom и прерываем выполнение
            if (transportType.Contains("Авто"))
            {
                return await GetTomTomTravelTime(fromLat, fromLon, toLat, toLon);
            }
            // Если Общественный - строим комбинированный маршрут
            else if (transportType.Contains("Общественный"))
            {
                transportModesStr = "[{mode: TRANSIT}]";
            }
            // Пешком
            else if (transportType.Contains("Пешком"))
            {
                transportModesStr = "[{mode: WALK}]";
            }
        }

        global::Android.Util.Log.Error("OTP", $"Отправляю GraphQL запрос (modes: {transportModesStr})");

        // Преобразуем координаты в формат "lat,lon"
        string flat = fromLat.ToString(CultureInfo.InvariantCulture);
        string flon = fromLon.ToString(CultureInfo.InvariantCulture);
        string tlat = toLat.ToString(CultureInfo.InvariantCulture);
        string tlon = toLon.ToString(CultureInfo.InvariantCulture);

        string currentDate = DateTime.Now.ToString("yyyy-MM-dd");
        string currentTime = DateTime.Now.ToString("HH:mm");

        // GraphQL запрос
        var requestBody = new
        {
            query = $@"
        {{
              plan
              (
                from: {{lat: {flat}, lon: {flon}}},
                to: {{lat: {tlat}, lon: {tlon}}},
                date: ""{currentDate}"",
                time: ""{currentTime}"",
                transportModes: {transportModesStr},
                numItineraries: 3,
                searchWindow: 7200
              ) 
              {{
                itineraries
                {{
                  duration
                  legs {{ mode transitLeg }}
                }}
              }}
        }}"
        };


        try
        {
            // Превращаем GraphQL в JSON строку
            string jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);

            var request = new HttpRequestMessage(HttpMethod.Post, ServerUrl)
            {
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };

            // Это надо чтоб приложение не мучели предупреждениями
            request.Headers.Add("Bypass-Tunnel-Reminder", "true");
            request.Headers.Add("User-Agent", "NoLateMobileApp/1.0");
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Отправляем запрос на к ОТР на серваке
            var response = await _httpClient.SendAsync(request);

            // Проверяем успешность
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                global::Android.Util.Log.Error("OTP", $"Ошибка HTTP {response.StatusCode}: {errorText}");
                return null;
            }

            // Читаем ответ от сервака
            var jsonString = await response.Content.ReadAsStringAsync();
            global::Android.Util.Log.Error("OTP", $"Ответ: {jsonString.Substring(0, Math.Min(jsonString.Length, 200))}");

            // Преврашаем JSON в кастомный обект GraphQLResponse
            var data = System.Text.Json.JsonSerializer.Deserialize<GraphQLResponse>
            (
                jsonString,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            // Извлекаем длительность маршрута в секундах и переводим в минуты с округлением в верх
            var itineraries = data?.Data?.Plan?.Itineraries;
            if (itineraries == null || itineraries.Count == 0)
                return null;

            ItineraryData? best;
            if (transportType.Contains("Общественный"))
            {
                // Берём первый маршрут где есть хотя бы один транзитный отрезок (автобус/метро/электричка)
                best = itineraries.FirstOrDefault(i => i.Legs?.Any(l => l.TransitLeg) == true)
                       ?? itineraries.First();
            }
            else
            {
                best = itineraries.First();
            }

            return (int)Math.Ceiling(best.Duration / 60.0);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("OTP", $"Критическая ошибка: {ex.Message}");
            return null;
        }
    }

    // Получение времени через TomTom
    private async Task<int?> GetTomTomTravelTime(double fromLat, double fromLon, double toLat, double toLon)
    {
        // Тоже что и тогда реобразуем координаты в формат "lat,lon"
        string flat = fromLat.ToString(CultureInfo.InvariantCulture);
        string flon = fromLon.ToString(CultureInfo.InvariantCulture);
        string tlat = toLat.ToString(CultureInfo.InvariantCulture);
        string tlon = toLon.ToString(CultureInfo.InvariantCulture);

        // Формируем URL запроса к TomTom (traffic=true использовать пробки в расчёте avoid=tollRoads избегать платных дорог
        string url = $"https://api.tomtom.com/routing/1/calculateRoute/{flat},{flon}:{tlat},{tlon}/json?key={TomTomApiKey}&departAt=now&traffic=true&avoid=tollRoads";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(jsonString);
                var root = document.RootElement;

                // Проверяем успешность
                if (root.TryGetProperty("routes", out var routes) && routes.GetArrayLength() > 0)
                {
                    var summary = routes[0].GetProperty("summary");
                    if (summary.TryGetProperty("travelTimeInSeconds", out var travelTimeInSeconds))
                    {
                        int totalSeconds = travelTimeInSeconds.GetInt32();

                        // Переводим секунды в минуты с округлением в большую сторону
                        int timeWithTrafficMins = (int)Math.Ceiling(totalSeconds / 60.0);

                        // Процентный буфер 40% от времени поездки для компенсации неточностей TomTom в нашей стране (всреднем после этого начил выдавать занчения как яндекс)
                        int buffer = (int)Math.Ceiling(timeWithTrafficMins * 0.40);
                        int finalTravelTime = timeWithTrafficMins + buffer;

                        return finalTravelTime;
                    }
                }
            }
            else
            {
                var errorText = await response.Content.ReadAsStringAsync();
#if ANDROID
                global::Android.Util.Log.Error("TomTomAPI", $"Ошибка HTTP {response.StatusCode}: {errorText}");
#endif
            }
        }
        catch (Exception ex)
        {
#if ANDROID
            global::Android.Util.Log.Error("TomTomAPI", $"Ошибка TomTom: {ex.Message}");
#endif
        }

        return null;
    }


    // Классы для преврашения JSON ответа от GraphQL
    public class GraphQLResponse
    {
        public GraphQLData? Data { get; set; }
    }

    public class GraphQLData
    {
        public PlanData? Plan { get; set; }
    }

    public class PlanData
    {
        public List<ItineraryData>? Itineraries { get; set; }
    }

    public class ItineraryData
    {
        public long Duration { get; set; }
        public List<LegData>? Legs { get; set; }
    }

    public class LegData
    {
        public string? Mode { get; set; }
        public bool TransitLeg { get; set; }
    }
}
