using GMEPPlumbing.Services;
using GMEPPlumbing.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace GMEPPlumbing.Views
{
  public partial class UserInterface : UserControl
  {
    public ObservableCollection<AdditionalLoss> AdditionalLosses { get; set; }

    public UserInterface()
    {
      InitializeComponent();
      AdditionalLosses = new ObservableCollection<AdditionalLoss>();
      DynamicListView.ItemsSource = AdditionalLosses;
      DataContext = new WaterSystemViewModel(new WaterMeterLossCalculationService(), new WaterStaticLossService(), new WaterTotalLossService(), new WaterPressureAvailableService(), new WaterDevelopedLengthService(), new WaterRemainingPressurePer100FeetService());
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

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
      var button = (Button)sender;
      var itemToRemove = (AdditionalLoss)button.DataContext;
      AdditionalLosses.Remove(itemToRemove);
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
  }

  public class AdditionalLoss
  {
    public string Title { get; set; }
    public string Amount { get; set; }
  }
}