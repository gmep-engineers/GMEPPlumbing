using System;
using System.Collections.Generic;

namespace GMEPPlumbing.Services
{
  public class WaterCalculationService
  {
    public double CalculateAveragePressureDrop(double lowPressure, double highPressure)
    {
      return (highPressure - lowPressure) / 2;
    }
  }
}