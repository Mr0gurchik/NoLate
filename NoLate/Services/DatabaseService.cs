using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;
using NoLate.Models;

namespace NoLate.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _db;

        public DatabaseService()
        {
            // Путь к базе в папке приложения
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"nolates.db3");

            _db = new SQLiteAsyncConnection(dbPath); //(асихронная чтоб не вис юай на будуюшие)

            _db.CreateTableAsync<AlarmModel>().Wait();
        }

        // Все буд
        public Task<List<AlarmModel>> GetAlarmsAsync()
            => _db.Table<AlarmModel>().OrderBy(a => a.AlarmTime).ToListAsync();

        // 1 буд по id
        public Task<AlarmModel> GetAlarmAsync(int id)
            => _db.Table<AlarmModel>().FirstOrDefaultAsync(a => a.Id == id);

        // Созд обн
        public Task<int> SaveAlarmAsync(AlarmModel alarm)
        {
            if (alarm.Id == 0)
                return _db.InsertAsync(alarm);
            return _db.UpdateAsync(alarm);
        }

        // Делит
        public Task<int> DeleteAlarmAsync(AlarmModel alarm)
            => _db.DeleteAsync(alarm);
    }
}

