using NoLate.Services;

namespace NoLate
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // прикольную тему нашел кастыль но зато какой это создает пустой экземпляр AlarmEditPage чтоб первое открытие было такое же быстрое как и последуюшие
            Task.Run(() => {
                var dummy = new AlarmEditPage(new DatabaseService());
            });

            return new Window(new AppShell());
        }
    }
}