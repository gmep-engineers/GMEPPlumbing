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
using System.Windows.Shapes;

namespace GMEPPlumbing.Views
{
    /// <summary>
    /// Interaction logic for BasePointPromptWindow.xaml
    /// </summary>
    public partial class BasePointPromptWindow : Window
    {
        public bool Water => WaterCheck.IsChecked == true;
        public bool Gas => GasCheck.IsChecked == true;
        public bool SewerVent => SewerVentCheck.IsChecked == true;
        public bool Site => SiteCheck.IsChecked == true;
       //public bool Storm => StormCheck.IsChecked == true;
        public string PlanName => PlanNameText.Text;
        public string FloorQty => FloorQtyText.Text;


        public BasePointPromptWindow()
        {
            InitializeComponent();
        }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PlanNameText.Text) || string.IsNullOrWhiteSpace(FloorQtyText.Text))
            {
                MessageBox.Show("Please fill in all fields.");
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}
