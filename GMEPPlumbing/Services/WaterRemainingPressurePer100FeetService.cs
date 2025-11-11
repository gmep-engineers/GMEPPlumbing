using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Services
{
  public class WaterRemainingPressurePer100FeetService
  {
    public double CalculateRemainingPressurePer100Feet(
      double remainingPressure,
      double developedLength
    )
    {
      return 100 * remainingPressure / developedLength;
    }
  }
}
