using SQLite;
using System.Runtime.CompilerServices;
using System.ComponentModel;

namespace NoLate.Models
{
    [Table("alarms")]
    public class AlarmModel : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Адрес
        public string? Mesto { get; set; }

        // В пути
        [Ignore]
        public string TravelTimeText => $"В пути: {TravelTime} мин + {DopTime} мин";

        // Время к скольки те приперется надо
        public DateTime MestTime { get; set; }

        // Время срабатывания будильника
        public DateTime AlarmTime { get; set; }

        // Время в пути в минутах
        public int TravelTime { get; set; }

        // Доп время
        public int DopTime { get; set; }

        // Тип транспорта (НАдеюсь сделаю)
        public string? Transport { get; set; }

        // Актив
        public bool IsActive { get; set; } = true;

        // Подсветочка
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        // Интерфейс
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
