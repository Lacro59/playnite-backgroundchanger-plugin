using System.Collections.Generic;

namespace BackgroundChanger.Models
{
    public class CheckData : ObservableObject
    {
        private string name;
        private string data;
        private bool isChecked = true;

        public string Name
        {
            get => name;
            set => SetValue(ref name, value);
        }

        public string Data
        {
            get => data;
            set => SetValue(ref data, value);
        }

        public bool IsChecked
        {
            get => isChecked;
            set => SetValue(ref isChecked, value);
        }
    }

}
