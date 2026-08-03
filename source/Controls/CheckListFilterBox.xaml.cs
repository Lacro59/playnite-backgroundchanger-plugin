using BackgroundChanger.Models;
using BackgroundChanger.Services;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundChangerPlugin.Controls
{
    /// <summary>
    /// Multi-select filter dropdown for <see cref="CheckData"/> items without editable ComboBox text side effects.
    /// </summary>
    public partial class CheckListFilterBox : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(List<CheckData>),
                typeof(CheckListFilterBox),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty UseShortSummaryProperty =
            DependencyProperty.Register(
                nameof(UseShortSummary),
                typeof(bool),
                typeof(CheckListFilterBox),
                new PropertyMetadata(false, OnSummaryModeChanged));

        public static readonly RoutedEvent FilterChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(FilterChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(CheckListFilterBox));

        public CheckListFilterBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets or sets the filter items displayed in the dropdown.
        /// </summary>
        public List<CheckData> ItemsSource
        {
            get { return (List<CheckData>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        /// <summary>
        /// Gets or sets whether the closed header shows a short dimension summary.
        /// </summary>
        public bool UseShortSummary
        {
            get { return (bool)GetValue(UseShortSummaryProperty); }
            set { SetValue(UseShortSummaryProperty, value); }
        }

        /// <summary>
        /// Occurs when a checkbox selection changes.
        /// </summary>
        public event RoutedEventHandler FilterChanged
        {
            add { AddHandler(FilterChangedEvent, value); }
            remove { RemoveHandler(FilterChangedEvent, value); }
        }

        /// <summary>
        /// Refreshes the summary text shown in the closed dropdown header.
        /// </summary>
        public void RefreshSummary()
        {
            if (PART_TextFilterString != null)
            {
                PART_TextFilterString.Text = SteamGridFilterHelper.BuildSummary(ItemsSource, UseShortSummary);
            }
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CheckListFilterBox control = (CheckListFilterBox)d;
            control.PART_ItemsPanel.ItemsSource = e.NewValue as List<CheckData>;
            control.RefreshSummary();
        }

        private static void OnSummaryModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CheckListFilterBox)d).RefreshSummary();
        }

        private void FilterItem_Changed(object sender, RoutedEventArgs e)
        {
            RefreshSummary();
            RaiseEvent(new RoutedEventArgs(FilterChangedEvent, this));
        }
    }
}
