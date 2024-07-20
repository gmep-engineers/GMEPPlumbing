using GMEPPlumbing.Services;
using GMEPPlumbing.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace GMEPPlumbing.Views
{
  public partial class UserInterface : UserControl
  {
    private WaterSystemViewModel _viewModel;
    public ObservableCollection<AdditionalLoss> AdditionalLosses { get; set; }
    public ObservableCollection<AdditionalLoss> AdditionalLosses2 { get; set; }

    public UserInterface()
    {
      InitializeComponent();
      AdditionalLosses = new ObservableCollection<AdditionalLoss>();
      AdditionalLosses2 = new ObservableCollection<AdditionalLoss>();
      AdditionalLosses.CollectionChanged += AdditionalLosses_CollectionChanged;
      AdditionalLosses2.CollectionChanged += AdditionalLosses2_CollectionChanged;
      DynamicListView.ItemsSource = AdditionalLosses;
      DynamicListView2.ItemsSource = AdditionalLosses2;

      _viewModel = new WaterSystemViewModel(
          new WaterMeterLossCalculationService(),
          new WaterStaticLossService(),
          new WaterTotalLossService(),
          new WaterPressureAvailableService(),
          new WaterDevelopedLengthService(),
          new WaterRemainingPressurePer100FeetService(),
          new WaterAdditionalLosses(this),
          new WaterAdditionalLosses(this)); // Second instance for AdditionalLosses2

      DataContext = _viewModel;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
      if (!string.IsNullOrWhiteSpace(TitleTextBox.Text) && !string.IsNullOrWhiteSpace(ValueTextBox.Text))
      {
        AdditionalLosses.Add(new AdditionalLoss { Title = TitleTextBox.Text, Amount = ValueTextBox.Text });
        TitleTextBox.Clear();
        ValueTextBox.Clear();
      }
    }

    private void AddButton2_Click(object sender, RoutedEventArgs e)
    {
      if (!string.IsNullOrWhiteSpace(TitleTextBox2.Text) && !string.IsNullOrWhiteSpace(ValueTextBox2.Text))
      {
        AdditionalLosses2.Add(new AdditionalLoss { Title = TitleTextBox2.Text, Amount = ValueTextBox2.Text });
        TitleTextBox2.Clear();
        ValueTextBox2.Clear();
      }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
      var button = (Button)sender;
      var itemToRemove = (AdditionalLoss)button.DataContext;
      AdditionalLosses.Remove(itemToRemove);
    }

    private void RemoveButton2_Click(object sender, RoutedEventArgs e)
    {
      var button = (Button)sender;
      var itemToRemove = (AdditionalLoss)button.DataContext;
      AdditionalLosses2.Remove(itemToRemove);
    }

    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
      if (sender is TextBox textBox)
      {
        textBox.Dispatcher.BeginInvoke(new Action(() =>
        {
          textBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
      }
    }

    private void AdditionalLosses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      _viewModel.UpdateAdditionalLosses();
    }

    private void AdditionalLosses2_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      _viewModel.UpdateAdditionalLosses2();
    }
  }

  public class AdditionalLoss
  {
    public string Title { get; set; }
    public string Amount { get; set; }
  }
}