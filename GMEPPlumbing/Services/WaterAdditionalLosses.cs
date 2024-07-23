using GMEPPlumbing.ViewModels;
using System.Collections.ObjectModel;
using System.Linq;

namespace GMEPPlumbing.Services
{
  public class WaterAdditionalLosses
  {
    public double CalculateTotalAdditionalLosses(ObservableCollection<AdditionalLoss> additionalLosses)
    {
      return additionalLosses.Sum(loss => double.TryParse(loss.Amount, out double amount) ? amount : 0);
    }
  }
}