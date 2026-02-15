using SQLite;

namespace NoLate.Models
{
    [Table("alarms")]
    public class AlarmModel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Адрес
        public string? Mesto { get; set; }

        // В пути
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

        // Актив?
        public bool IsActive { get; set; } = true;
    }
}
