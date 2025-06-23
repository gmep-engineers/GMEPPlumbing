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
    /// <summary>
    /// Interaction logic for Routing.xaml
    /// </summary>
    public partial class Routing : UserControl
    {
        public Dictionary<string, double> LengthToFixtures { get; set; } = new Dictionary<string, double>();
        public Routing(Dictionary<string, double> lengthToFixtures)
        {
          LengthToFixtures = lengthToFixtures;
          InitializeComponent();
          DataContext = this;
        }
    }
}
