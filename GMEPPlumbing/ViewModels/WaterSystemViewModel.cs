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
    private readonly WaterTotalLossService _waterTotalLossService;
    private readonly WaterPressureAvailableService _waterPressureAvailableService;
    private readonly WaterDevelopedLengthService _waterDevelopedLengthService;
    private readonly WaterRemainingPressurePer100FeetService _waterRemainingPressurePer100FeetService;

    public WaterSystemViewModel(WaterMeterLossCalculationService waterMeterLoss, WaterStaticLossService elevationStaticLoss, WaterTotalLossService waterTotalLoss, WaterPressureAvailableService waterPressureAvailable, WaterDevelopedLengthService waterDevelopedLength, WaterRemainingPressurePer100FeetService waterRemainingPressurePer100Feet)
    {
      _waterMeterLossService = waterMeterLoss;
      _waterStaticLossService = elevationStaticLoss;
      _waterTotalLossService = waterTotalLoss;
      _waterPressureAvailableService = waterPressureAvailable;
      _waterDevelopedLengthService = waterDevelopedLength;
      _waterRemainingPressurePer100FeetService = waterRemainingPressurePer100Feet;
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
          CalculateWaterPressureAvailable();
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

    private double _requiredPressure;

    public double RequiredPressure
    {
      get => _requiredPressure;
      set
      {
        if (_requiredPressure != value)
        {
          _requiredPressure = value;
          OnPropertyChanged();
          CalculateTotalLoss();
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
          CalculateWaterDevelopedLength();
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

    private double _backflowPressureLoss;

    public double BackflowPressureLoss
    {
      get => _backflowPressureLoss;
      set
      {
        if (_backflowPressureLoss != value)
        {
          _backflowPressureLoss = value;
          OnPropertyChanged();
          CalculateTotalLoss();
        }
      }
    }

    private double _prvPressureLoss;

    public double PRVPressureLoss
    {
      get => _prvPressureLoss;
      set
      {
        if (_prvPressureLoss != value)
        {
          _prvPressureLoss = value;
          OnPropertyChanged();
          CalculateTotalLoss();
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
          CalculateTotalLoss();
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
          CalculateTotalLoss();
        }
      }
    }

    private double _totalLoss;

    public double TotalLoss
    {
      get => _totalLoss;
      private set
      {
        if (_totalLoss != value)
        {
          _totalLoss = value;
          OnPropertyChanged();
          CalculateWaterPressureAvailable();
        }
      }
    }

    private double _pressureAvailable;

    public double PressureAvailable
    {
      get => _pressureAvailable;
      private set
      {
        if (_pressureAvailable != value)
        {
          _pressureAvailable = value;
          OnPropertyChanged();
          CalculateWaterPressureRemainingPer100Feet();
        }
      }
    }

    private double _developedLength;

    public double DevelopedLength
    {
      get => _developedLength;
      private set
      {
        if (_developedLength != value)
        {
          _developedLength = value;
          OnPropertyChanged();
          CalculateWaterPressureRemainingPer100Feet();
        }
      }
    }

    private double _averagePressureDrop;

    public double AveragePressureDrop
    {
      get => _averagePressureDrop;
      private set
      {
        if (_averagePressureDrop != value)
        {
          _averagePressureDrop = value;
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

    private void CalculateTotalLoss()
    {
      TotalLoss = _waterTotalLossService.CalculateTotalLoss(MeterLoss, StaticLoss, RequiredPressure, BackflowPressureLoss);
    }

    private void CalculateWaterPressureAvailable()
    {
      PressureAvailable = _waterPressureAvailableService.CalculateAvailableWaterPressure(LowPressure, TotalLoss);
    }

    private void CalculateWaterDevelopedLength()
    {
      DevelopedLength = _waterDevelopedLengthService.CalculateDevelopedLength(SystemLength);
    }

    private void CalculateWaterPressureRemainingPer100Feet()
    {
      AveragePressureDrop = _waterRemainingPressurePer100FeetService.CalculateRemainingPressurePer100Feet(PressureAvailable, DevelopedLength);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}