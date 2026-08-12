using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WeekAquaWPF.Protocol;

namespace WeekAquaWPF.Models
{
    public class RampPointSlot : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private int _pointId;
        private bool _isEnabled = true;
        private TimeSpan _startTime = new TimeSpan(8, 0, 0);
        private TimeSpan _endTime = new TimeSpan(18, 0, 0);
        private double _redPercent = 80;
        private double _greenPercent = 80;
        private double _bluePercent = 80;
        private double _whitePercent = 80;

        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

        public int PointId
        {
            get => _pointId;
            set { _pointId = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged();
                Validate();
            }
        }

        public TimeSpan StartTime
        {
            get => _startTime;
            set
            {
                _startTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartTimeStr));
                Validate();
            }
        }

        public TimeSpan EndTime
        {
            get => _endTime;
            set
            {
                _endTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EndTimeStr));
                Validate();
            }
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
                else
                {
                    AddError(nameof(StartTimeStr), "Invalid time format. Use HH:mm.");
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
                else
                {
                    AddError(nameof(EndTimeStr), "Invalid time format. Use HH:mm.");
                }
            }
        }

        public double RedPercent
        {
            get => _redPercent;
            set
            {
                _redPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RedByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                Validate();
            }
        }

        public double GreenPercent
        {
            get => _greenPercent;
            set
            {
                _greenPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GreenByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                Validate();
            }
        }

        public double BluePercent
        {
            get => _bluePercent;
            set
            {
                _bluePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlueByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                Validate();
            }
        }

        public double WhitePercent
        {
            get => _whitePercent;
            set
            {
                _whitePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WhiteByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                Validate();
            }
        }

        public byte RedByte => WeekAquaProtocol.PercentToByte(RedPercent);
        public byte GreenByte => WeekAquaProtocol.PercentToByte(GreenPercent);
        public byte BlueByte => WeekAquaProtocol.PercentToByte(BluePercent);
        public byte WhiteByte => WeekAquaProtocol.PercentToByte(WhitePercent);

        private string _modelCode = string.Empty;

        public string ModelCode
        {
            get => _modelCode;
            set
            {
                _modelCode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPowerPercent));
                Validate();
            }
        }

        public double TotalPowerPercent => WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, GreenPercent, BluePercent, WhitePercent, 0, ModelCode);

        #region INotifyDataErrorInfo Implementation

        public bool HasErrors => _errors.Any(kv => kv.Value != null && kv.Value.Count > 0);

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return _errors.Values.SelectMany(errList => errList).ToList();
            }

            if (_errors.TryGetValue(propertyName, out var errors))
            {
                return errors;
            }

            return Enumerable.Empty<string>();
        }

        public string FirstErrorMessage => _errors.Values.SelectMany(v => v).FirstOrDefault() ?? string.Empty;

        public void AddError(string propertyName, string error)
        {
            if (!_errors.ContainsKey(propertyName))
            {
                _errors[propertyName] = new List<string>();
            }

            if (!_errors[propertyName].Contains(error))
            {
                _errors[propertyName].Add(error);
                OnErrorsChanged(propertyName);
            }
        }

        public void ClearErrors(string propertyName)
        {
            if (_errors.ContainsKey(propertyName))
            {
                _errors.Remove(propertyName);
                OnErrorsChanged(propertyName);
            }
        }

        private void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(FirstErrorMessage));
        }

        public void Validate()
        {
            ClearErrors(nameof(StartTimeStr));
            ClearErrors(nameof(EndTimeStr));
            ClearErrors(nameof(RedPercent));
            ClearErrors(nameof(GreenPercent));
            ClearErrors(nameof(BluePercent));
            ClearErrors(nameof(WhitePercent));

            if (StartTime >= EndTime)
            {
                AddError(nameof(StartTimeStr), $"Start time ({StartTimeStr}) must be earlier than end time ({EndTimeStr}).");
                AddError(nameof(EndTimeStr), $"End time ({EndTimeStr}) must be later than start time ({StartTimeStr}).");
            }

            double totalPower = TotalPowerPercent;
            if (totalPower > 100.0)
            {
                string powerError = $"Total power ({totalPower:F1}%) exceeds 100.0% max limit.";
                AddError(nameof(RedPercent), powerError);
                AddError(nameof(GreenPercent), powerError);
                AddError(nameof(BluePercent), powerError);
                AddError(nameof(WhitePercent), powerError);
            }
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
