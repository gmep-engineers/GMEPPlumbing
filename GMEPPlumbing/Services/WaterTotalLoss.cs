using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Services
{
  public class WaterTotalLoss
  {
    public double CalculateTotalLoss(double meterLoss, double staticLoss, double requiredPressure, double backflowLoss)
    {
      return meterLoss + staticLoss + requiredPressure + backflowLoss;
    }
  }
}