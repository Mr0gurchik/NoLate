namespace NoLate.Services;

public static class LocationHelper
{
    // Метод проверки и запроса разрешений
    public static async Task<bool> CheckAndRequestLocationPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

        if (status == PermissionStatus.Granted)
            return true;

        if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
        {
            // На iOS если отказали один раз - нужно лезть в настройки (заметочка если я буду адаптировать эту прогу до iOS что будет тяжело...)
            return false;
        }

        // Показываем объяснение если пользователь паникер
        if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
        {
            await Shell.Current.DisplayAlert("Нужен GPS", "Чтобы считать пробки от вашего местоположения, нужен доступ к геопозиции.", "ОК");
        }

        status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        return status == PermissionStatus.Granted;
    }

    // Метод получения текущих координат
    public static async Task<Location?> GetCurrentLocation()
    {
        try
        {
            // Быстрый WiFi чисто по спутникам в россии работает фиговато, а вот по WiFi может работать нормально
            var fastRequest = new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(3));
            var fastLoc = await Geolocation.Default.GetLocationAsync(fastRequest);
            if (fastLoc != null && fastLoc.Accuracy < 100)
            {
                System.Diagnostics.Debug.WriteLine($" WiFi GPS: {fastLoc.Latitude:F6},{fastLoc.Longitude:F6} ({fastLoc.Accuracy:F1}m)");
                return fastLoc;
            }

            // Точный GPS (10 сек)
            var accurateRequest = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
            var accurateLoc = await Geolocation.Default.GetLocationAsync(accurateRequest);

            System.Diagnostics.Debug.WriteLine($"High GPS: {accurateLoc?.Latitude:F6},{accurateLoc?.Longitude:F6} ({accurateLoc?.Accuracy:F1}m)");
            return accurateLoc;
        }

        // Логировка ошибок
        catch (FeatureNotSupportedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"GPS не поддерживается: {ex.Message}");
            return null;
        }
        catch (FeatureNotEnabledException ex)
        {
            System.Diagnostics.Debug.WriteLine($"GPS выключен: {ex.Message}");
            return null;
        }
        catch (PermissionException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Нет разрешений GPS: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GPS Unknown: {ex.Message}");
            return null;
        }
    }
}