using GMEPPlumbing.Services;
using GMEPPlumbing.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GMEPPlumbing.Views
{
  public partial class UserInterface : UserControl
  {
    private WaterSystemViewModel _viewModel;

    public UserInterface(string currentDrawingId)
    {
      InitializeComponent();

      _viewModel = new WaterSystemViewModel(
          new WaterMeterLossCalculationService(),
          new WaterStaticLossService(),
          new WaterTotalLossService(),
          new WaterPressureAvailableService(),
          new WaterDevelopedLengthService(),
          new WaterRemainingPressurePer100FeetService(),
          new WaterAdditionalLosses(),
          new WaterAdditionalLosses(),
          currentDrawingId);

      DataContext = _viewModel;

      DynamicListView.ItemsSource = _viewModel.AdditionalLosses;
      DynamicListView2.ItemsSource = _viewModel.AdditionalLosses2;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
      if (!string.IsNullOrWhiteSpace(TitleTextBox.Text) && !string.IsNullOrWhiteSpace(ValueTextBox.Text))
      {
        _viewModel.AddAdditionalLoss(TitleTextBox.Text, ValueTextBox.Text);
        TitleTextBox.Clear();
        ValueTextBox.Clear();
      }
    }

    private void AddButton2_Click(object sender, RoutedEventArgs e)
    {
      if (!string.IsNullOrWhiteSpace(TitleTextBox2.Text) && !string.IsNullOrWhiteSpace(ValueTextBox2.Text))
      {
        _viewModel.AddAdditionalLoss2(TitleTextBox2.Text, ValueTextBox2.Text);
        TitleTextBox2.Clear();
        ValueTextBox2.Clear();
      }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
      var button = (Button)sender;
      var itemToRemove = (AdditionalLoss)button.DataContext;
      _viewModel.RemoveAdditionalLoss(itemToRemove);
    }

    private void RemoveButton2_Click(object sender, RoutedEventArgs e)
    {
      var button = (Button)sender;
      var itemToRemove = (AdditionalLoss)button.DataContext;
      _viewModel.RemoveAdditionalLoss2(itemToRemove);
    }

    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
      if (sender is TextBox textBox)
      {
        textBox.Dispatcher.BeginInvoke(new Action(() =>
        {
          textBox.SelectAll();
        }), DispatcherPriority.Input);
      }
    }
  }
}