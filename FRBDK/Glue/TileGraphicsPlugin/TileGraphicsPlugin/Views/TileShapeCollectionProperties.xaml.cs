﻿using FlatRedBall.Glue.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TileGraphicsPlugin.Controllers;
using TileGraphicsPlugin.ViewModels;

namespace TileGraphicsPlugin.Views
{
    /// <summary>
    /// Interaction logic for TileShapeCollectionProperties.xaml
    /// </summary>
    public partial class TileShapeCollectionProperties : UserControl
    {
        TileShapeCollectionPropertiesViewModel ViewModel => DataContext as TileShapeCollectionPropertiesViewModel;


        public TileShapeCollectionProperties()
        {
            InitializeComponent();
        }

        private void TextBox_KeyEnterUpdate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DependencyProperty prop = null;
                if ( sender is TextBox tbox)
                {
                    prop = TextBox.TextProperty;

                }
                else if(sender is ComboBox cbox)
                {
                    prop = ComboBox.TextProperty;
                }

                if(prop != null)
                {
                    BindingExpression binding = BindingOperations.GetBindingExpression(
                        sender as DependencyObject, prop);
                    if (binding != null) { binding.UpdateSource(); }
                }
            }
        }

        private void TilesetTileSelector_NewTileSelected(TilesetTileSelectorFullViewModel vm)
        {
            var newName = vm.TileType;

            // refresh the available types

            // The property may be the same before and after, but the underlying
            // ID may have changed. Therefore, let's force set it which raises the
            // notifications to other properties
            //this.ViewModel.CollisionTileTypeName = newName;
            this.ViewModel.ForceSetCollisionTileTypeName(newName);

            TileShapeCollectionsPropertiesController.Self.RefreshAvailableTypes();
        }
    }
}
