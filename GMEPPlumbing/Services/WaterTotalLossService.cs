using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Services
{
  public class WaterTotalLossService
  {
    public double CalculateTotalLoss(double meterLoss, double staticLoss, double requiredPressure, double backflowLoss, double prvLoss, double additionalLosses)
    {
      return meterLoss + staticLoss + requiredPressure + backflowLoss + prvLoss + additionalLosses;
    }
  }
}