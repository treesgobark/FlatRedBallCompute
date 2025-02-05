﻿using FlatRedBall.Glue.MVVM;
using FlatRedBall.Glue.ViewModels;
using FlatRedBall.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FlatRedBall.Glue.Plugins.EmbeddedPlugins.LoadRecentFilesPlugin.ViewModels
{
    internal class LoadRecentViewModel : ViewModel, ISearchBarViewModel
    {
        public List<RecentItemViewModel> AllItems
        {
            get; private set;
        } = new List<RecentItemViewModel>();

        public ObservableCollection<RecentItemViewModel> FilteredItems
        {
            get; set;
        } = new ObservableCollection<RecentItemViewModel>();

        public string SearchBoxText
        {
            get => Get<string>();
            set => Set(value);
        }
        public bool IsSearchBoxFocused
        {
            get => Get<bool>();
            set => Set(value);
        }

        public RecentItemViewModel SelectedItem
        {
            get => Get<RecentItemViewModel>();
            set => Set(value);
        }

        [DependsOn(nameof(SearchBoxText))]
        public Visibility SearchButtonVisibility => (!string.IsNullOrEmpty(SearchBoxText)).ToVisibility();

        public Visibility TipsVisibility => Visibility.Collapsed;

        [DependsOn(nameof(IsSearchBoxFocused))]
        [DependsOn(nameof(SearchBoxText))]
        public Visibility SearchPlaceholderVisibility =>
            (IsSearchBoxFocused == false && string.IsNullOrWhiteSpace(SearchBoxText)).ToVisibility();


        public string FilterResultsInfo => String.Empty;

        public LoadRecentViewModel()
        {
            this.PropertyChanged += HandlePropertyChanged;
        }

        private void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(SearchBoxText):
                    RefreshFilteredItems();
                    break;
            }
        }

        public void RefreshFilteredItems()
        {
            var itemBefore = SelectedItem;
            FilteredItems.Clear();

            var sortedItems = AllItems.OrderBy(item => item.IsFavorite == false);
            // No keep it based on date
                // .ThenBy(item => FileManager.RemovePath( item.FullPath));

            foreach(var item in sortedItems)
            {
                if(IsMatch(item))
                {
                    FilteredItems.Add(item);
                }
            }

            if(itemBefore != null && FilteredItems.Any(item => item.FullPath == itemBefore.FullPath))
            {
                SelectedItem = FilteredItems.First(item => item.FullPath == itemBefore.FullPath);
            }
            else
            {
                SelectedItem = FilteredItems.FirstOrDefault();
            }
        }

        private bool IsMatch(RecentItemViewModel item)
        {
            return string.IsNullOrEmpty(SearchBoxText) ||
                item.FullPath.ToLowerInvariant().Contains(SearchBoxText.ToLowerInvariant());
        }
    }
}
