using GMEPPlumbing.Services;
using GMEPPlumbing.ViewModels;
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

namespace GMEPPlumbing.Views
{
  public partial class UserInterface : UserControl
  {
    public UserInterface()
    {
      InitializeComponent();
      DataContext = new WaterSystemViewModel(new WaterMeterLossCalculationService(), new WaterStaticLossService(), new WaterTotalLossService(), new WaterPressureAvailableService(), new WaterDevelopedLengthService(), new WaterRemainingPressurePer100FeetService());
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
}