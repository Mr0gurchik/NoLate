using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace NoLate
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            SetTheme(Resource.Style.Maui_MainTheme_NoActionBar);
            base.OnCreate(savedInstanceState);

            if (Window != null)
            {
                // Сука прозрачный фон
#pragma warning disable CA1422 // Проверка совместимости платформы
                Window.SetStatusBarColor(Android.Graphics.Color.Transparent);
#pragma warning restore CA1422 // Проверка совместимости платформы

                var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
                if (controller != null)
                {
                    controller.AppearanceLightStatusBars = false;
                }
            }
        }
    }
}
