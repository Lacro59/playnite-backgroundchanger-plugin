using System.Collections.Generic;

namespace BackgroundChanger.Models
{
    public class CheckData : ObservableObject
    {
        private string _name;
        private string _data;
        private bool _isChecked = true;

        public string Name
        {
            get => _name;
            set => SetValue(ref _name, value);
        }

        public string Data
        {
            get => _data;
            set => SetValue(ref _data, value);
        }

        public bool IsChecked
        {
            get => _isChecked;
            set => SetValue(ref _isChecked, value);
        }
    }

}
