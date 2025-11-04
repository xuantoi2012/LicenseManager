using System;
using System.ComponentModel;

namespace LicenseManager.Models
{
    public class OnlineUserInfo : INotifyPropertyChanged
    {
        private int _stt;
        private string _activeUser;
        private DateTime _openTime;
        private bool _isBlocked;
        private bool _isOnline;
        private string _macAddress;
        private string _machineName;
        private string _fullName; // Thêm thuộc tính FullName

        public int STT
        {
            get => _stt;
            set { if (_stt != value) { _stt = value; OnPropertyChanged(nameof(STT)); } }
        }
        public string ActiveUser
        {
            get => _activeUser;
            set { if (_activeUser != value) { _activeUser = value; OnPropertyChanged(nameof(ActiveUser)); } }
        }
        public DateTime OpenTime
        {
            get => _openTime;
            set
            {
                if (_openTime != value)
                {
                    _openTime = value;
                    OnPropertyChanged(nameof(OpenTime));
                    OnPropertyChanged(nameof(OpenTimeString));
                }
            }
        }

        public bool IsBlocked
        {
            get => _isBlocked;
            set { if (_isBlocked != value) { _isBlocked = value; OnPropertyChanged(nameof(IsBlocked)); OnPropertyChanged(nameof(BlockedString)); } }
        }

        public string BlockedString => IsBlocked ? "Blocked" : "";

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    OnPropertyChanged(nameof(IsOnline));
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string Status => IsOnline ? "Online" : "Offline";

        public string OpenTimeString => OpenTime.ToString("yyyy-MM-dd HH:mm:ss");

        public string MacAddress
        {
            get => _macAddress;
            set { if (_macAddress != value) { _macAddress = value; OnPropertyChanged(nameof(MacAddress)); } }
        }

        public string MachineName
        {
            get => _machineName;
            set { if (_machineName != value) { _machineName = value; OnPropertyChanged(nameof(MachineName)); } }
        }

        // Thêm property FullName để nhập tên đầy đủ của user
        public string FullName
        {
            get => _fullName;
            set
            {
                if (_fullName != value)
                {
                    _fullName = value;
                    OnPropertyChanged(nameof(FullName));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}