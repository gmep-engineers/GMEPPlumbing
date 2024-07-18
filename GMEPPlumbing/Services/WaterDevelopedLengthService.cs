using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMEPPlumbing.Services
{
  public class WaterDevelopedLengthService
  {
    public double CalculateDevelopedLength(double systemLength)
    {
      return systemLength * 1.3;
    }
  }
}