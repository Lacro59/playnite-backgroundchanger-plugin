using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPlayniteShared;
using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Interfaces;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BackgroundChanger.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginBackgroundImage.xaml
    /// </summary>
    public partial class PluginBackgroundImage : PluginUserControlExtend
    {
        private BackgroundChangerDatabase PluginDatabase = BackgroundChanger.PluginDatabase;
        internal override IPluginDatabase _PluginDatabase
        {
            get
            {
                return PluginDatabase;
            }
            set
            {
                PluginDatabase = (BackgroundChangerDatabase)_PluginDatabase;
            }
        }

        private PluginBackgroundImageDataContext ControlDataContext;
        internal override IDataContext _ControlDataContext
        {
            get
            {
                return ControlDataContext;
            }
            set
            {
                ControlDataContext = (PluginBackgroundImageDataContext)_ControlDataContext;
            }
        }

        private System.Timers.Timer BcTimer;
        private int Counter = 0;
        private GameBackgroundImages gameBackgroundImages;


        public override void SetDefaultDataContext()
        {
            ControlDataContext = new PluginBackgroundImageDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.Settings.EnableBackgroundImage,
                UseAnimated = PluginDatabase.PluginSettings.Settings.EnableImageAnimatedBackground,
                EnableRandomSelect = PluginDatabase.PluginSettings.Settings.EnableBackgroundImageRandomSelect,
                EnableAutoChanger = PluginDatabase.PluginSettings.Settings.EnableBackgroundImageAutoChanger
            };
        }


        public PluginBackgroundImage()
        {
            InitializeComponent();
            Image1FadeIn = (Storyboard)TryFindResource("Image1FadeIn");
            Image2FadeIn = (Storyboard)TryFindResource("Image2FadeIn");
            Image1FadeOut = (Storyboard)TryFindResource("Image1FadeOut");
            Image2FadeOut = (Storyboard)TryFindResource("Image2FadeOut");
            BorderDarkenFadeOut = (Storyboard)TryFindResource("BorderDarkenFadeOut");
            Image1FadeOut.Completed += Image1FadeOut_Completed;
            Image2FadeOut.Completed += Image2FadeOut_Completed;
            BorderDarkenFadeOut.Completed += BorderDarkenOut_Completed;


            PluginDatabase.PluginSettings.PropertyChanged += PluginSettings_PropertyChanged;
            PluginDatabase.Database.ItemUpdated += Database_ItemUpdated;
            PluginDatabase.Database.ItemCollectionChanged += Database_ItemCollectionChanged;
            PluginDatabase.PlayniteApi.Database.Games.ItemUpdated += Games_ItemUpdated;

            // Apply settings
            PluginSettings_PropertyChanged(null, null);
        }

        private void GetFadeImageProperties()
        {
            System.Threading.SpinWait.SpinUntil(() =>
            {
                FrameworkElement PART_ImageBackground = IntegrationUI.SearchElementByName("ControlRoot", false, false, 2);

                if (PART_ImageBackground != null)
                {
                    PropertyInfo[] ImageBackgroundProperties = PART_ImageBackground.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    PropertyInfo[] backChangerImageProperties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    
                    List<string> UsedProperties = new List<string>
                    {
                        "AnimationEnabled", "ImageOpacityMask", "ImageDarkeningBrush", "Stretch", "StretchDirection",
                        "IsBlurEnabled", "BlurAmount", "HighQualityBlur"
                    };

                    foreach (PropertyInfo propImageBackground in ImageBackgroundProperties)
                    {
                        if (propImageBackground.CanWrite)
                        {
                            if (UsedProperties.Contains(propImageBackground.Name))
                            {
                                var propBackChangerImage = backChangerImageProperties.Where(x => x.Name == propImageBackground.Name).FirstOrDefault();

                                try
                                {
                                    var value = propImageBackground.GetValue(PART_ImageBackground, null);
                                    propBackChangerImage.SetValue(this, value, null);

                                }
                                catch (Exception ex)
                                {
                                    Common.LogError(ex, false);
                                }
                            }
                        }
                    }
                }

                return PART_ImageBackground != null;
            }, 5000);
        }


        public override Task<bool> SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            return Task.Run(() =>
            {
                gameBackgroundImages = (GameBackgroundImages)PluginGameData;

                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    try
                    {
                        if (!gameBackgroundImages.HasDataBackground)
                        {
                            MustDisplay = false;
                            return;
                        }

                        SetBackground();
                        this.DataContext = ControlDataContext;
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                    }
                }));

                return true;
            });
        }


        public void SetBackground()
        {
            string PathImage = string.Empty;

            if (BcTimer != null)
            {
                Counter = 0;
                BcTimer.Stop();
                BcTimer = null;
            }
            
            if (gameBackgroundImages.HasDataBackground)
            {
                if (ControlDataContext.EnableAutoChanger)
                {
                    if (ControlDataContext.EnableRandomSelect)
                    {
                        Random rnd = new Random();
                        int ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsBackground.Count));
                        PathImage = gameBackgroundImages.ItemsBackground[ImgSelected].FullPath;
                    }
                    else
                    {
                        PathImage = gameBackgroundImages.ItemsBackground[Counter].FullPath;
                    }

                    SetBackgroundImage(PathImage);

                    BcTimer = new System.Timers.Timer(PluginDatabase.PluginSettings.Settings.BackgroundImageAutoChangerTimer * 1000);
                    BcTimer.AutoReset = true;
                    BcTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                    BcTimer.Start();
                }
                else if (ControlDataContext.EnableRandomSelect)
                {
                    Random rnd = new Random();
                    int ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsBackground.Count));
                    PathImage = gameBackgroundImages.ItemsBackground[ImgSelected].FullPath;

                    SetBackgroundImage(PathImage);
                }
                else
                {
                    SetDefaultBackgroundImage();
                }
            }
            else
            {
                SetDefaultBackgroundImage();
            }
        }

        public void SetDefaultBackgroundImage()
        {
            if (PluginDatabase.GameContext.BackgroundImage.IsNullOrEmpty())
            {
                SetBackgroundImage();
            }
            else
            {
                string PathImage = ImageSourceManager.GetImagePath(PluginDatabase.GameContext.BackgroundImage);
                if (PathImage.IsNullOrEmpty())
                {
                    PathImage = PluginDatabase.PlayniteApi.Database.GetFullFilePath(PluginDatabase.GameContext.BackgroundImage);
                }

                SetBackgroundImage(PathImage);
            }
        }

        public void SetBackgroundImage(string PathImage = null)
        {
            this.Dispatcher?.BeginInvoke(DispatcherPriority.Render, (Action)delegate
            {
                if (!File.Exists(PathImage))
                {
                    PathImage = null;
                }
                this.Source = PathImage;
            });
        }


        private enum CurrentImage
        {
            Image1,
            Image2,
            None
        }

        private CurrentImage currentImage = CurrentImage.None;
        private object currentSource = null;

        internal Storyboard Image1FadeIn;
        internal Storyboard Image2FadeIn;
        internal Storyboard Image1FadeOut;
        internal Storyboard Image2FadeOut;
        internal Storyboard stateAnim;
        internal Storyboard BorderDarkenFadeOut;

        #region AnimationEnabled
        public static readonly DependencyProperty AnimationEnabledProperty = DependencyProperty.Register(
            nameof(AnimationEnabled),
            typeof(bool),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(true));

        public bool AnimationEnabled
        {
            get { return (bool)GetValue(AnimationEnabledProperty); }
            set { SetValue(AnimationEnabledProperty, value); }
        }
        #endregion AnimationEnabled

        #region Source
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(object),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(null, SourceChanged));

        public object Source
        {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
        #endregion Source

        #region ImageOpacityMask
        public static readonly DependencyProperty ImageOpacityMaskProperty = DependencyProperty.Register(
            nameof(ImageOpacityMask),
            typeof(Brush),
            typeof(PluginBackgroundImage),
            new PropertyMetadata());

        public Brush ImageOpacityMask
        {
            get { return (Brush)GetValue(ImageOpacityMaskProperty); }
            set { SetValue(ImageOpacityMaskProperty, value); }
        }
        #endregion ImageOpacityMask

        #region ImageDarkeningBrush
        public static readonly DependencyProperty ImageDarkeningBrushProperty = DependencyProperty.Register(
            nameof(ImageDarkeningBrush),
            typeof(Brush),
            typeof(PluginBackgroundImage),
            new PropertyMetadata());

        public Brush ImageDarkeningBrush
        {
            get { return (Brush)GetValue(ImageDarkeningBrushProperty); }
            set { SetValue(ImageDarkeningBrushProperty, value); }
        }
        #endregion ImageDarkeningBrush

        #region Stretch
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(Stretch.UniformToFill));

        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }
        #endregion Strech

        #region IsBlurEnabled
        public static readonly DependencyProperty IsBlurEnabledProperty = DependencyProperty.Register(
            nameof(IsBlurEnabled),
            typeof(bool),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(false, BlurSettingChanged));

        public bool IsBlurEnabled
        {
            get { return (bool)GetValue(IsBlurEnabledProperty); }
            set { SetValue(IsBlurEnabledProperty, value); }
        }
        #endregion IsBlurEnabled

        #region BlurAmount
        public static readonly DependencyProperty BlurAmountProperty = DependencyProperty.Register(
            nameof(BlurAmount),
            typeof(int),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(10, BlurSettingChanged));

        public int BlurAmount
        {
            get { return (int)GetValue(BlurAmountProperty); }
            set { SetValue(BlurAmountProperty, value); }
        }
        #endregion IsBlurEnabled

        #region HighQualityBlur
        public static readonly DependencyProperty HighQualityBlurProperty = DependencyProperty.Register(
            nameof(HighQualityBlurProperty),
            typeof(bool),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(false, BlurSettingChanged));

        public bool HighQualityBlur
        {
            get { return (bool)GetValue(HighQualityBlurProperty); }
            set { SetValue(HighQualityBlurProperty, value); }
        }
        #endregion HighQualityBlur

        private void Image1FadeOut_Completed(object sender, EventArgs e)
        {
            Image1.Source = null;
            Image1.UpdateLayout();
            GC.Collect();
        }

        private void Image2FadeOut_Completed(object sender, EventArgs e)
        {
            Image2.Source = null;
            Image2.UpdateLayout();
        }

        private void BorderDarkenOut_Completed(object sender, EventArgs e)
        {
            BorderDarken.Opacity = 0;
        }

        private static void BlurSettingChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var control = (PluginBackgroundImage)obj;
            if (control.Source == null)
            {
                return;
            }

            var blurAmount = control.BlurAmount;
            var blurEnabled = control.IsBlurEnabled;
            var highQuality = control.HighQualityBlur;
            if (blurEnabled)
            {
                control.ImageHolder.Effect = new BlurEffect()
                {
                    KernelType = KernelType.Gaussian,
                    Radius = blurAmount,
                    RenderingBias = highQuality ? RenderingBias.Quality : RenderingBias.Performance
                };
            }
            else
            {
                control.ImageHolder.Effect = null;
            }
        }

        private static void SourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var control = (PluginBackgroundImage)obj;
            control.LoadNewSource(args.NewValue, args.OldValue);
        }

        private async void LoadNewSource(object newSource, object oldSource)
        {
            var blurAmount = BlurAmount;
            var blurEnabled = IsBlurEnabled;
            var highQuality = HighQualityBlur;
            //BitmapImage image = null;
            string image = null;

            if (newSource?.Equals(currentSource) == true)
            {
                return;
            }

            currentSource = newSource;
            //if (newSource != null)
            //{
            //    image = await Task.Factory.StartNew(() =>
            //    {
            //        if (newSource is string str)
            //        {
            //            return ImageSourceManager.GetImage(str, false);
            //        }
            //        else if (newSource is BitmapLoadProperties props)
            //        {
            //            return ImageSourceManager.GetImage(props.Source, false, props);
            //        }
            //        else
            //        {
            //            return null;
            //        }
            //    });
            //}
            if (newSource is string)
            {
                image = (string)currentSource;
            }

            if (blurEnabled)
            {
                if (ImageHolder.Effect == null)
                {
                    ImageHolder.Effect = new BlurEffect()
                    {
                        KernelType = KernelType.Gaussian,
                        Radius = blurAmount,
                        RenderingBias = highQuality ? RenderingBias.Quality : RenderingBias.Performance
                    };
                }
            }
            else
            {
                if (ImageHolder.Effect != null)
                {
                    ImageHolder.Effect = null;
                }
            }

            if (AnimationEnabled)
            {
                if (image == null)
                {
                    if (currentImage == CurrentImage.None)
                    {
                        return;
                    }

                    if (currentImage == CurrentImage.Image1)
                    {
                        Image1FadeOut.Begin();
                        BorderDarkenFadeOut.Begin();
                    }
                    else if (currentImage == CurrentImage.Image2)
                    {
                        Image2FadeOut.Begin();
                        BorderDarkenFadeOut.Begin();
                    }

                    currentImage = CurrentImage.None;
                }
                else
                {
                    if (currentImage == CurrentImage.None)
                    {
                        Image1FadeOut.Stop();
                        Image1.Source = image;
                        Image1FadeIn.Begin();
                        BorderDarken.Opacity = 1;
                        BorderDarkenFadeOut.Stop();
                        currentImage = CurrentImage.Image1;
                    }
                    else if (currentImage == CurrentImage.Image1)
                    {
                        Image2FadeOut.Stop();
                        Image2.Source = image;
                        Image2FadeIn.Begin();
                        Image1FadeOut.Begin();
                        BorderDarken.Opacity = 1;
                        BorderDarkenFadeOut.Stop();
                        currentImage = CurrentImage.Image2;
                    }
                    else if (currentImage == CurrentImage.Image2)
                    {
                        Image1FadeOut.Stop();
                        Image1.Source = image;
                        Image1FadeIn.Begin();
                        Image2FadeOut.Begin();
                        BorderDarken.Opacity = 1;
                        BorderDarkenFadeOut.Stop();
                        currentImage = CurrentImage.Image1;
                    }
                }
            }
            else
            {
                if (currentImage == CurrentImage.Image1)
                {
                    Image1.Source = image;
                }
                else if (currentImage == CurrentImage.Image2)
                {
                    Image2.Source = image;
                }
                else
                {
                    Image1.Source = image;
                    currentImage = CurrentImage.Image1;
                }
            }
        }


        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            try
            {
                if (ControlDataContext.EnableRandomSelect)
                {
                    Random rnd = new Random();
                    int ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsBackground.Count));
                    while (ImgSelected == Counter)
                    {
                        ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsBackground.Count));
                    }
                    Counter = ImgSelected;

                    string PathImage = gameBackgroundImages.ItemsBackground[ImgSelected].FullPath;

                    SetBackgroundImage(PathImage);
                }
                else
                {
                    Counter++;

                    if (Counter == gameBackgroundImages.ItemsBackground.Count)
                    {
                        Counter = 0;
                    }

                    string PathImage = gameBackgroundImages.ItemsBackground[Counter].FullPath;

                    SetBackgroundImage(PathImage);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
        }


        private void ImageHolder_Loaded(object sender, RoutedEventArgs e)
        {
            // Copy FadeImage properties
            GetFadeImageProperties();
        }
    }


    public class PluginBackgroundImageDataContext : IDataContext
    {
        public bool IsActivated { get; set; }
        public bool UseAnimated { get; set; }
        public bool EnableRandomSelect { get; set; }
        public bool EnableAutoChanger { get; set; }
    }
}
