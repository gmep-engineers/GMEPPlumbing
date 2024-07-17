using GMEPPlumbing.Services;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GMEPPlumbing.ViewModels
{
  public class WaterSystemViewModel : INotifyPropertyChanged
  {
    private readonly WaterMeterLossCalculationService _waterMeterLossService;
    private readonly WaterStaticLossService _waterStaticLossService;

    public WaterSystemViewModel(WaterMeterLossCalculationService waterMeterLoss, WaterStaticLossService elevationStaticLoss)
    {
      _waterMeterLossService = waterMeterLoss;
      _waterStaticLossService = elevationStaticLoss;
    }

    private double _lowPressure;

    public double LowPressure
    {
      get => _lowPressure;
      set
      {
        if (_lowPressure != value)
        {
          _lowPressure = value;
          OnPropertyChanged();
        }
      }
    }

    private double _highPressure;

    public double HighPressure
    {
      get => _highPressure;
      set
      {
        if (_highPressure != value)
        {
          _highPressure = value;
          OnPropertyChanged();
        }
      }
    }

    private double _systemLength;

    public double SystemLength
    {
      get => _systemLength;
      set
      {
        if (_systemLength != value)
        {
          _systemLength = value;
          OnPropertyChanged();
        }
      }
    }

    private double _fixtureCalculation;

    public double FixtureCalculation
    {
      get => _fixtureCalculation;
      set
      {
        if (_fixtureCalculation != value)
        {
          _fixtureCalculation = value;
          OnPropertyChanged();
          CalculateMeterLoss();
        }
      }
    }

    private double _meterSize;

    public double MeterSize
    {
      get => _meterSize;
      set
      {
        if (_meterSize != value)
        {
          _meterSize = value;
          OnPropertyChanged();
          CalculateMeterLoss();
        }
      }
    }

    private double _elevation;

    public double Elevation
    {
      get => _elevation;
      set
      {
        if (_elevation != value)
        {
          _elevation = value;
          OnPropertyChanged();
          CalculateStaticLoss();
        }
      }
    }

    private double _meterLoss;

    public double MeterLoss
    {
      get => _meterLoss;
      private set
      {
        if (_meterLoss != value)
        {
          _meterLoss = value;
          OnPropertyChanged();
        }
      }
    }

    private double _staticLoss;

    public double StaticLoss
    {
      get => _staticLoss;
      private set
      {
        if (_staticLoss != value)
        {
          _staticLoss = value;
          OnPropertyChanged();
        }
      }
    }

    private void CalculateMeterLoss()
    {
      MeterLoss = _waterMeterLossService.CalculateWaterMeterLoss(MeterSize, FixtureCalculation);
    }

    private void CalculateStaticLoss()
    {
      StaticLoss = _waterStaticLossService.CalculateStaticLoss(Elevation);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}