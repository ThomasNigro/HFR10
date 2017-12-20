﻿using Hfr.ViewModel;
using Hfr.Views.MiscPages;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Hfr.Views
{
    public sealed partial class Shell : UserControl
    {
        private readonly double ExtraPaneDefaultHeight = 650;
        private readonly double ExtraPaneDefaultWidth = 700;
        public Shell()
        {
            this.InitializeComponent();
            this.SizeChanged += Shell_SizeChanged;
        }

        private void Shell_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Responsive();
        }

        void Responsive()
        {
            if (this.ActualHeight < ExtraPaneDefaultHeight || this.ActualWidth < ExtraPaneDefaultWidth)
            {
                if (ExtraPaneVisible)
                {
                    ExtraPageFrame.HorizontalAlignment = HorizontalAlignment.Stretch;
                    ExtraPageFrame.VerticalAlignment = VerticalAlignment.Stretch;
                    ExtraPageFrame.Height = this.ActualHeight;
                    ExtraPageFrame.Width = this.ActualWidth;
                }
            }
            else
            {
                if (ExtraPaneVisible)
                {
                    ExtraPageFrame.HorizontalAlignment = HorizontalAlignment.Right;
                    ExtraPageFrame.VerticalAlignment = VerticalAlignment.Bottom;
                    ExtraPageFrame.Height = ExtraPaneDefaultHeight;
                    ExtraPageFrame.Width = ExtraPaneDefaultWidth;
                }
            }
        }

        public bool ExtraPaneVisible => ExtraPageFrame.Content != null;

        public void NavigateExtraFrame(Type type, object parameter)
        {
            ExtraPageFrameTranslateTransform.Y = 0;
            ExtraPaneGrid.Visibility = Visibility.Visible;
            FadeInExtraPageStoryboard.Begin();
            ExtraPageFrame.Navigate(type, parameter);
            Responsive();
        }

        public void HideExtraFrame()
        {
            EasingKeyFrame.Value = ExtraPageFrame.ActualHeight;
            FadeOutExtraPageStoryboard.Completed += FadeOutExtraPageStoryboard_Completed;
            FadeOutExtraPageStoryboard.Begin();
        }

        public void GoToDarkTheme(bool dark)
        {
            this.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        }

        private void FadeOutExtraPageStoryboard_Completed(object sender, object e)
        {
            ExtraPaneGrid.Visibility = Visibility.Collapsed;
            ExtraPageFrame.Navigate(typeof(BlankPage));
        }

        #region navview

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // set the initial SelectedItem 
            foreach (NavigationViewItemBase item in NavView.MenuItems)
            {
                if (item is NavigationViewItem && item.Tag.ToString() == "dashboard")
                {
                    NavView.SelectedItem = item;
                    break;
                }
            }
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                Navigate("settings");
            }
            else
            {
                Navigate(args.InvokedItem.ToString().ToLower());
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                Navigate("settings");
            }
            else
            {
                NavigationViewItem item = args.SelectedItem as NavigationViewItem;
                Navigate(item.Tag.ToString().ToLower());
            }
        }

        void Navigate(string view)
        {
            switch (view)
            {
                case "dashboard":
                    Loc.NavigationService.Navigate(Model.View.Main);
                    break;
                case "topics":
                    Loc.NavigationService.Navigate(Model.View.DrapsPage);
                    break;
                case "settings":
                    Loc.NavigationService.Navigate(Model.View.Settings);
                    break;
            }
        }
        #endregion
    }
}
