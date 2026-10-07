using System;

namespace GAUGE.Models
{
    // Це структура нашого тренування, яка потім полетить у базу даних SQLite
    public class Workout
    {
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Now;
        public string PhotoPlaceholder { get; set; } = "📷"; // Поки немає реального фото
    }
}