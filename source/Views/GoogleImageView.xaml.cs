using BackgroundChanger.Models;
using CommonPlayniteShared;
using Playnite.SDK;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundChanger.Views
{
    /// <summary>
    /// Logique d'interaction pour GoogleImageView.xaml
    /// </summary>
    public partial class GoogleImageView : UserControl
    {
        public List<GoogleImage> GoogleImageResults = null;

        private GoogleImageViewModel GoogleImageViewModel { get; set; } = new GoogleImageViewModel();


        public GoogleImageView(string searchTerm)
        {
            InitializeComponent();

            GoogleImageViewModel.SearchTerm = searchTerm;
            DataContext = GoogleImageViewModel;
        }


        private void PART_ElementList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                GoogleImageResults = PART_ElementList.SelectedItems.Cast<GoogleImage>().ToList();
                if (GoogleImageResults != null)
                {
                    ButtonSelect.IsEnabled = true;
                }
            }
            catch
            {
                ButtonSelect.IsEnabled = false;
            }
        }


        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            ((Window)Parent).Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ((Window)Parent).Close();
        }
    }


    public enum SafeSearchSettings
    {
        [Description(LOC.Default)]
        Default,
        [Description(LOC.EnabledTitle)]
        On,
        [Description(LOC.DisabledTitle)]
        Off
    }


    public class GoogleImageViewModel : ObservableObject
    {
        private GoogleImageDownloader Downloader => new GoogleImageDownloader();

        public double ItemWidth { get; set; } = 240;
        public double ItemHeight { get; set; } = 180;

        private string _searchTerm;
        public string SearchTerm { get => _searchTerm; set => SetValue(ref _searchTerm, value); }

        private int? _searchWidth;
        public int? SearchWidth { get => _searchWidth; set => SetValue(ref _searchWidth, value); }

        private int? _searchHeight;
        public int? SearchHeight { get => _searchHeight; set => SetValue(ref _searchHeight, value); }

        private SafeSearchSettings _safeSearch = SafeSearchSettings.On;
        public SafeSearchSettings SafeSearch { get => _safeSearch; set => SetValue(ref _safeSearch, value); }

        private bool _transparent = false;
        public bool Transparent { get => _transparent; set => SetValue(ref _transparent, value); }


        public RelayCommand<object> SearchCommand => new RelayCommand<object>((a) =>
        {
            Search();
        }, (a) => !string.IsNullOrEmpty(SearchTerm));

        public RelayCommand<string> ClearSearchResolutionCommand => new RelayCommand<string>((a) => ClearSearchResolution());

        public void ClearSearchResolution()
        {
            SearchWidth = null;
            SearchHeight = null;
        }

        public RelayCommand<string> SetSearchResolutionCommand => new RelayCommand<string>((resolution) => SetSearchResolution(resolution));
        public void SetSearchResolution(string resolution)
        {
            Match regex = Regex.Match(resolution, @"(\d+)x(\d+)");
            if (regex.Success)
            {
                SearchWidth = int.Parse(regex.Groups[1].Value);
                SearchHeight = int.Parse(regex.Groups[2].Value);
            }
        }

        public RelayCommand<object> LoadMoreCommand => new RelayCommand<object>((a) => LoadMore());

        public void LoadMore()
        {
            if (AvailableImages.Count > 20)
            {
                //DisplayImages.AddRange(AvailableImages.Take(20));
                AvailableImages.Take(20).ForEach(x => DisplayImages.Add(x));
                AvailableImages.RemoveRange(0, 20);
                ShowLoadMore = true;
            }
            else if (AvailableImages.Count > 0)
            {
                //DisplayImages.AddRange(AvailableImages);
                AvailableImages.ForEach(x => DisplayImages.Add(x));
                AvailableImages.Clear();
                ShowLoadMore = false;
            }
        }


        private List<GoogleImage> _availableImages = new List<GoogleImage>();
        public List<GoogleImage> AvailableImages { get => _availableImages; set => SetValue(ref _availableImages, value); }

        public ObservableCollection<GoogleImage> DisplayImages
        {
            get;
        } = new ObservableCollection<GoogleImage>();

        private bool _showLoadMore = false;
        public bool ShowLoadMore { get => _showLoadMore; set => SetValue(ref _showLoadMore, value); }

        public void Search()
        {
            AvailableImages = new List<GoogleImage>();
            string query = SearchTerm;
            if (SearchWidth != null && SearchHeight != null && !query.Contains("imagesize:"))
            {
                query = $"{query} imagesize:{SearchWidth}x{SearchHeight}";
            }

            if (API.Instance.Dialogs.ActivateGlobalProgress((_) =>
            {
                AvailableImages = Downloader.GetImages(query, SafeSearch, Transparent).GetAwaiter().GetResult();
            }, new GlobalProgressOptions("LOCDownloadingLabel")).Result == true)
            {
                if (!AvailableImages.HasItems())
                {
                    return;
                }

                DisplayImages.Clear();
                if (AvailableImages.Count > 20)
                {
                    //DisplayImages.AddRange(AvailableImages.Take(20));
                    AvailableImages.Take(20).ForEach(x => DisplayImages.Add(x));
                    AvailableImages.RemoveRange(0, 20);
                    ShowLoadMore = true;
                }
                else if (AvailableImages.Count > 0)
                {
                    //DisplayImages.AddRange(AvailableImages);
                    AvailableImages.ForEach(x => DisplayImages.Add(x));
                    AvailableImages.Clear();
                    ShowLoadMore = false;
                }
            }
        }
    }
}