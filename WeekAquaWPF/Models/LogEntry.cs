using System;

namespace WeekAquaWPF.Models
{
    public enum LogDirection
    {
        TX,
        RX,
        Info,
        Error
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogDirection Direction { get; set; }
        public string Message { get; set; } = string.Empty;
        public string HexData { get; set; } = string.Empty;

        public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff");
        public string DirectionString => Direction.ToString();
    }
}
