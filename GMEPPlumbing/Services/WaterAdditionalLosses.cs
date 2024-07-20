using GMEPPlumbing.Views;
using System.Linq;

namespace GMEPPlumbing.Services
{
  public class WaterAdditionalLosses
  {
    private readonly UserInterface _userInterface;

    public WaterAdditionalLosses(UserInterface userInterface)
    {
      _userInterface = userInterface;
    }

    public double CalculateTotalAdditionalLosses()
    {
      return _userInterface.AdditionalLosses.Sum(loss => double.TryParse(loss.Amount, out double amount) ? amount : 0);
    }
  }
}