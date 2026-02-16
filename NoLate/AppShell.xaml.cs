namespace NoLate
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(AlarmEditPage), typeof(AlarmEditPage));
        }
    }
}
