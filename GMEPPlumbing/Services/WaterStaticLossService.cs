using System;

namespace GMEPPlumbing.Services
{
  public class WaterStaticLossService
  {
    private static readonly double GravitationalForce = 0.433;

    public double CalculateStaticLoss(double elevation)
    {
      double result = GravitationalForce * elevation;
      return Math.Round(result, 1);
    }
  }
}