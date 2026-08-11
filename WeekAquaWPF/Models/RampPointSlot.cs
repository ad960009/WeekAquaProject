using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WeekAquaWPF.Protocol;

namespace WeekAquaWPF.Models
{
    public class RampPointSlot : INotifyPropertyChanged
    {
        private int _pointId;
        private bool _isEnabled = true;
        private TimeSpan _startTime = new TimeSpan(8, 0, 0);
        private TimeSpan _endTime = new TimeSpan(18, 0, 0);
        private double _redPercent = 80;
        private double _greenPercent = 80;
        private double _bluePercent = 80;
        private double _whitePercent = 80;

        public int PointId
        {
            get => _pointId;
            set { _pointId = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public TimeSpan StartTime
        {
            get => _startTime;
            set { _startTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(StartTimeStr)); }
        }

        public TimeSpan EndTime
        {
            get => _endTime;
            set { _endTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(EndTimeStr)); }
        }

        public string StartTimeStr
        {
            get => _startTime.ToString(@"hh\:mm");
            set
            {
                if (TimeSpan.TryParse(value, out TimeSpan ts))
                {
                    StartTime = ts;
                }
            }
        }

        public string EndTimeStr
        {
            get => _endTime.ToString(@"hh\:mm");
            set
            {
                if (TimeSpan.TryParse(value, out TimeSpan ts))
                {
                    EndTime = ts;
                }
            }
        }

        public double RedPercent
        {
            get => _redPercent;
            set { _redPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(RedByte)); }
        }

        public double GreenPercent
        {
            get => _greenPercent;
            set { _greenPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(GreenByte)); }
        }

        public double BluePercent
        {
            get => _bluePercent;
            set { _bluePercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(BlueByte)); }
        }

        public double WhitePercent
        {
            get => _whitePercent;
            set { _whitePercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(WhiteByte)); }
        }

        public byte RedByte => WeekAquaProtocol.PercentToByte(RedPercent);
        public byte GreenByte => WeekAquaProtocol.PercentToByte(GreenPercent);
        public byte BlueByte => WeekAquaProtocol.PercentToByte(BluePercent);
        public byte WhiteByte => WeekAquaProtocol.PercentToByte(WhitePercent);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
