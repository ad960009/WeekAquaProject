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
        private string _startTimeStr = "08:00";
        private string _endTimeStr = "18:00";
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
                _startTimeStr = WeekAquaProtocol.FormatTimeString(value);
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
                _endTimeStr = WeekAquaProtocol.FormatTimeString(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(EndTimeStr));
                Validate();
            }
        }

        public string StartTimeStr
        {
            get => _startTimeStr;
            set
            {
                _startTimeStr = value;
                OnPropertyChanged();
                if (WeekAquaProtocol.TryParseTimeString(value, out TimeSpan ts))
                {
                    _startTime = ts;
                    OnPropertyChanged(nameof(StartTime));
                    ClearErrors(nameof(StartTimeStr));
                    Validate();
                }
                else
                {
                    AddError(nameof(StartTimeStr), "Invalid time format. Use HH:mm.");
                }
            }
        }

        public string EndTimeStr
        {
            get => _endTimeStr;
            set
            {
                _endTimeStr = value;
                OnPropertyChanged();
                if (WeekAquaProtocol.TryParseTimeString(value, out TimeSpan ts))
                {
                    _endTime = ts;
                    OnPropertyChanged(nameof(EndTime));
                    ClearErrors(nameof(EndTimeStr));
                    Validate();
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

        private bool _isUvEnabled = true;
        private double _uvPercent = 0.0;

        public bool IsUvEnabled
        {
            get => _isUvEnabled;
            set
            {
                _isUvEnabled = value;
                if (!_isUvEnabled)
                {
                    _uvPercent = 0.0;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsUvReadOnly));
                OnPropertyChanged(nameof(UvPercent));
                OnPropertyChanged(nameof(UvByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                Validate();
            }
        }

        public bool IsUvReadOnly => !_isUvEnabled;

        public double UvPercent
        {
            get => _isUvEnabled ? _uvPercent : 0.0;
            set
            {
                _uvPercent = _isUvEnabled ? value : 0.0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UvByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                Validate();
            }
        }

        public byte RedByte => WeekAquaProtocol.PercentToByte(RedPercent);
        public byte GreenByte => WeekAquaProtocol.PercentToByte(GreenPercent);
        public byte BlueByte => WeekAquaProtocol.PercentToByte(BluePercent);
        public byte WhiteByte => WeekAquaProtocol.PercentToByte(WhitePercent);
        public byte UvByte => WeekAquaProtocol.PercentToByte(UvPercent);

        private bool _isVioletEnabled = true;
        private double _violetPercent = 0.0;

        public bool IsVioletEnabled
        {
            get => _isVioletEnabled;
            set
            {
                _isVioletEnabled = value;
                if (!_isVioletEnabled)
                {
                    _violetPercent = 0.0;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsVioletReadOnly));
                OnPropertyChanged(nameof(VioletPercent));
                OnPropertyChanged(nameof(VioletByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                Validate();
            }
        }

        public bool IsVioletReadOnly => !_isVioletEnabled;

        public double VioletPercent
        {
            get => _isVioletEnabled ? _violetPercent : 0.0;
            set
            {
                _violetPercent = _isVioletEnabled ? value : 0.0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VioletByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                Validate();
            }
        }

        public byte VioletByte => WeekAquaProtocol.PercentToByte(VioletPercent);

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

        public double TotalPowerPercent => WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, GreenPercent, BluePercent, WhitePercent, UvPercent, VioletPercent, ModelCode);

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
            ClearErrors(nameof(UvPercent));
            ClearErrors(nameof(VioletPercent));

            if (StartTime == EndTime)
            {
                AddError(nameof(StartTimeStr), "Start time and end time cannot be identical for a single slot.");
                AddError(nameof(EndTimeStr), "Start time and end time cannot be identical for a single slot.");
            }

            double totalPower = TotalPowerPercent;
            if (totalPower > 100.0)
            {
                string powerError = $"Total power ({totalPower:F1}%) exceeds 100.0% max limit.";
                AddError(nameof(RedPercent), powerError);
                AddError(nameof(GreenPercent), powerError);
                AddError(nameof(BluePercent), powerError);
                AddError(nameof(WhitePercent), powerError);
                AddError(nameof(UvPercent), powerError);
                AddError(nameof(VioletPercent), powerError);
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
