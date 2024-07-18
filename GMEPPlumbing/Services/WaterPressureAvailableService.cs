using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Services
{
  public class WaterPressureAvailableService
  {
    public double CalculateAvailableWaterPressure(double lowPressure, double totalLoss)
    {
      return lowPressure - totalLoss;
    }
  }
}