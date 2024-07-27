using Autodesk.AutoCAD.ApplicationServices;
using GMEPPlumbing.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Markup;

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
    private readonly WaterAdditionalLosses _waterAdditionalLossesService;
    private readonly WaterAdditionalLosses _waterAdditionalLossesService2;

    public ObservableCollection<AdditionalLoss> AdditionalLosses { get; set; }
    public ObservableCollection<AdditionalLoss> AdditionalLosses2 { get; set; }

    public WaterSystemViewModel(
        WaterMeterLossCalculationService waterMeterLoss,
        WaterStaticLossService waterStaticLoss,
        WaterTotalLossService waterTotalLoss,
        WaterPressureAvailableService waterPressureAvailable,
        WaterDevelopedLengthService waterDevelopedLength,
        WaterRemainingPressurePer100FeetService waterRemainingPressurePer100Feet,
        WaterAdditionalLosses waterAdditionalLossesService,
        WaterAdditionalLosses waterAdditionalLossesService2)
    {
      _waterMeterLossService = waterMeterLoss;
      _waterStaticLossService = waterStaticLoss;
      _waterTotalLossService = waterTotalLoss;
      _waterPressureAvailableService = waterPressureAvailable;
      _waterDevelopedLengthService = waterDevelopedLength;
      _waterRemainingPressurePer100FeetService = waterRemainingPressurePer100Feet;
      _waterAdditionalLossesService = waterAdditionalLossesService;
      _waterAdditionalLossesService2 = waterAdditionalLossesService2;

      AdditionalLosses = new ObservableCollection<AdditionalLoss>();
      AdditionalLosses2 = new ObservableCollection<AdditionalLoss>();

      AdditionalLosses.CollectionChanged += (s, e) => UpdateAdditionalLosses();
      AdditionalLosses2.CollectionChanged += (s, e) => UpdateAdditionalLosses2();
    }

    #region Properties for Section 1

    private string _sectionHeader1 = "Main CPVC Pipe to Unit Submeter";

    public string SectionHeader1
    {
      get => _sectionHeader1;
      set => SetProperty(ref _sectionHeader1, value);
    }

    private double _streetLowPressure;

    public double StreetLowPressure
    {
      get => _streetLowPressure;
      set
      {
        if (SetProperty(ref _streetLowPressure, value))
        {
          CalculateWaterPressureAvailable();
        }
      }
    }

    private double _streetHighPressure;

    public double StreetHighPressure
    {
      get => _streetHighPressure;
      set => SetProperty(ref _streetHighPressure, value);
    }

    private double _meterSize;

    public double MeterSize
    {
      get => _meterSize;
      set
      {
        if (SetProperty(ref _meterSize, value))
        {
          CalculateMeterLoss();
        }
      }
    }

    private double _fixtureCalculation;

    public double FixtureCalculation
    {
      get => _fixtureCalculation;
      set
      {
        if (SetProperty(ref _fixtureCalculation, value))
        {
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
        if (SetProperty(ref _elevation, value))
        {
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
        if (SetProperty(ref _backflowPressureLoss, value))
        {
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
        if (SetProperty(ref _prvPressureLoss, value))
        {
          CalculateTotalLoss();
        }
      }
    }

    private double _pressureRequiredOrAtUnit;

    public double PressureRequiredOrAtUnit
    {
      get => _pressureRequiredOrAtUnit;
      set
      {
        if (SetProperty(ref _pressureRequiredOrAtUnit, value))
        {
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
        if (SetProperty(ref _systemLength, value))
        {
          CalculateWaterDevelopedLength();
        }
      }
    }

    private double _meterLoss;

    public double MeterLoss
    {
      get => _meterLoss;
      private set
      {
        if (SetProperty(ref _meterLoss, value))
        {
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
        if (SetProperty(ref _staticLoss, value))
        {
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
        if (SetProperty(ref _totalLoss, value))
        {
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
        if (SetProperty(ref _pressureAvailable, value))
        {
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
        if (SetProperty(ref _developedLength, value))
        {
          CalculateWaterPressureRemainingPer100Feet();
        }
      }
    }

    private double _averagePressureDrop;

    public double AveragePressureDrop
    {
      get => _averagePressureDrop;
      private set => SetProperty(ref _averagePressureDrop, value);
    }

    private double _additionalLossesTotal;

    public double AdditionalLossesTotal
    {
      get => _additionalLossesTotal;
      private set
      {
        if (SetProperty(ref _additionalLossesTotal, value))
        {
          CalculateTotalLoss();
        }
      }
    }

    private bool _existingMeter = true;

    public bool ExistingMeter
    {
      get => _existingMeter;
      set => SetProperty(ref _existingMeter, value);
    }

    private string _pipeMaterial = "Type \"L\" Copper";

    public string PipeMaterial
    {
      get => _pipeMaterial;
      set => SetProperty(ref _pipeMaterial, value);
    }

    private int _coldWaterMaxVelocity = 8;

    public int ColdWaterMaxVelocity
    {
      get => _coldWaterMaxVelocity;
      set => SetProperty(ref _coldWaterMaxVelocity, value);
    }

    private int _hotWaterMaxVelocity = 5;

    public int HotWaterMaxVelocity
    {
      get => _hotWaterMaxVelocity;
      set => SetProperty(ref _hotWaterMaxVelocity, value);
    }

    private int _developedLengthPercentage = 130;

    public int DevelopedLengthPercentage
    {
      get => _developedLengthPercentage;
      set => SetProperty(ref _developedLengthPercentage, value);
    }

    #endregion Properties for Section 1

    #region Properties for Section 2

    private string _sectionHeader2 = "PEX Pipe Inside the Unit";

    public string SectionHeader2
    {
      get => _sectionHeader2;
      set => SetProperty(ref _sectionHeader2, value);
    }

    private double _pressureRequired2;

    public double PressureRequired2
    {
      get => _pressureRequired2;
      set
      {
        if (SetProperty(ref _pressureRequired2, value))
        {
          CalculateTotalLoss2();
        }
      }
    }

    private double _meterSize2;

    public double MeterSize2
    {
      get => _meterSize2;
      set
      {
        if (SetProperty(ref _meterSize2, value))
        {
          CalculateMeterLoss2();
        }
      }
    }

    private double _fixtureCalculation2;

    public double FixtureCalculation2
    {
      get => _fixtureCalculation2;
      set
      {
        if (SetProperty(ref _fixtureCalculation2, value))
        {
          CalculateMeterLoss2();
        }
      }
    }

    private double _systemLength2;

    public double SystemLength2
    {
      get => _systemLength2;
      set
      {
        if (SetProperty(ref _systemLength2, value))
        {
          CalculateWaterDevelopedLength2();
        }
      }
    }

    private double _meterLoss2;

    public double MeterLoss2
    {
      get => _meterLoss2;
      private set
      {
        if (SetProperty(ref _meterLoss2, value))
        {
          CalculateTotalLoss2();
        }
      }
    }

    private double _totalLoss2;

    public double TotalLoss2
    {
      get => _totalLoss2;
      private set
      {
        if (SetProperty(ref _totalLoss2, value))
        {
          CalculateWaterPressureAvailable2();
        }
      }
    }

    private double _pressureAvailable2;

    public double PressureAvailable2
    {
      get => _pressureAvailable2;
      private set
      {
        if (SetProperty(ref _pressureAvailable2, value))
        {
          CalculateWaterPressureRemainingPer100Feet2();
        }
      }
    }

    private double _developedLength2;

    public double DevelopedLength2
    {
      get => _developedLength2;
      private set
      {
        if (SetProperty(ref _developedLength2, value))
        {
          CalculateWaterPressureRemainingPer100Feet2();
        }
      }
    }

    private double _averagePressureDrop2;

    public double AveragePressureDrop2
    {
      get => _averagePressureDrop2;
      private set => SetProperty(ref _averagePressureDrop2, value);
    }

    private double _additionalLossesTotal2;

    public double AdditionalLossesTotal2
    {
      get => _additionalLossesTotal2;
      private set
      {
        if (SetProperty(ref _additionalLossesTotal2, value))
        {
          CalculateTotalLoss2();
        }
      }
    }

    private string _meterLossErrorMessage;

    public string MeterLossErrorMessage
    {
      get => _meterLossErrorMessage;
      private set => SetProperty(ref _meterLossErrorMessage, value);
    }

    #endregion Properties for Section 2

    #region Calculation Methods for Section 1

    private void CalculateMeterLoss()
    {
      var (pressureLoss, message) = _waterMeterLossService.CalculateWaterMeterLoss(MeterSize, FixtureCalculation);

      if (pressureLoss.HasValue)
      {
        MeterLoss = pressureLoss.Value;
        MeterLossErrorMessage = null;
      }
      else
      {
        MeterLoss = 0;
        MeterLossErrorMessage = message;
      }

      OnPropertyChanged(nameof(MeterLoss));
      OnPropertyChanged(nameof(MeterLossErrorMessage));
    }

    private void CalculateStaticLoss()
    {
      StaticLoss = _waterStaticLossService.CalculateStaticLoss(Elevation);
    }

    private void CalculateTotalLoss()
    {
      TotalLoss = _waterTotalLossService.CalculateTotalLoss(MeterLoss, StaticLoss, PressureRequiredOrAtUnit, BackflowPressureLoss, PRVPressureLoss, AdditionalLossesTotal);
    }

    private void CalculateWaterPressureAvailable()
    {
      PressureAvailable = _waterPressureAvailableService.CalculateAvailableWaterPressure(StreetLowPressure, TotalLoss);
    }

    private void CalculateWaterDevelopedLength()
    {
      DevelopedLength = _waterDevelopedLengthService.CalculateDevelopedLength(SystemLength, DevelopedLengthPercentage);
    }

    private void CalculateWaterPressureRemainingPer100Feet()
    {
      AveragePressureDrop = _waterRemainingPressurePer100FeetService.CalculateRemainingPressurePer100Feet(PressureAvailable, DevelopedLength);
    }

    public void UpdateAdditionalLosses()
    {
      AdditionalLossesTotal = _waterAdditionalLossesService.CalculateTotalAdditionalLosses(AdditionalLosses);
    }

    #endregion Calculation Methods for Section 1

    #region Calculation Methods for Section 2

    private string _meterLossErrorMessage2;

    public string MeterLossErrorMessage2
    {
      get => _meterLossErrorMessage2;
      private set => SetProperty(ref _meterLossErrorMessage2, value);
    }

    private void CalculateMeterLoss2()
    {
      var (pressureLoss, message) = _waterMeterLossService.CalculateWaterMeterLoss(MeterSize2, FixtureCalculation2);

      if (pressureLoss.HasValue)
      {
        MeterLoss2 = pressureLoss.Value;
        MeterLossErrorMessage2 = null;
      }
      else
      {
        MeterLoss2 = 0;
        MeterLossErrorMessage2 = message;
      }

      OnPropertyChanged(nameof(MeterLoss2));
      OnPropertyChanged(nameof(MeterLossErrorMessage2));
    }

    private void CalculateTotalLoss2()
    {
      TotalLoss2 = _waterTotalLossService.CalculateTotalLoss(MeterLoss2, 0, PressureRequired2, 0, 0, AdditionalLossesTotal2);
    }

    private void CalculateWaterPressureAvailable2()
    {
      PressureAvailable2 = _waterPressureAvailableService.CalculateAvailableWaterPressure(PressureRequiredOrAtUnit, TotalLoss2);
    }

    private void CalculateWaterDevelopedLength2()
    {
      DevelopedLength2 = _waterDevelopedLengthService.CalculateDevelopedLength(SystemLength2, DevelopedLengthPercentage);
    }

    private void CalculateWaterPressureRemainingPer100Feet2()
    {
      AveragePressureDrop2 = _waterRemainingPressurePer100FeetService.CalculateRemainingPressurePer100Feet(PressureAvailable2, DevelopedLength2);
    }

    public void UpdateAdditionalLosses2()
    {
      AdditionalLossesTotal2 = _waterAdditionalLossesService2.CalculateTotalAdditionalLosses(AdditionalLosses2);
    }

    public void AddAdditionalLoss(string title, string amount)
    {
      AdditionalLosses.Add(new AdditionalLoss { Title = title, Amount = amount });
    }

    public void AddAdditionalLoss2(string title, string amount)
    {
      AdditionalLosses2.Add(new AdditionalLoss { Title = title, Amount = amount });
    }

    public void RemoveAdditionalLoss(AdditionalLoss loss)
    {
      AdditionalLosses.Remove(loss);
    }

    public void RemoveAdditionalLoss2(AdditionalLoss loss)
    {
      AdditionalLosses2.Remove(loss);
    }

    #endregion Calculation Methods for Section 2

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
      if (Equals(field, value)) return false;
      field = value;
      OnPropertyChanged(propertyName);
      return true;
    }

    public WaterSystemData GetWaterSystemData()
    {
      return new WaterSystemData
      {
        SectionHeader1 = SectionHeader1,
        StreetLowPressure = StreetLowPressure,
        StreetHighPressure = StreetHighPressure,
        MeterSize = MeterSize,
        FixtureCalculation = FixtureCalculation,
        Elevation = Elevation,
        BackflowPressureLoss = BackflowPressureLoss,
        PRVPressureLoss = PRVPressureLoss,
        PressureRequiredOrAtUnit = PressureRequiredOrAtUnit,
        SystemLength = SystemLength,
        MeterLoss = MeterLoss,
        StaticLoss = StaticLoss,
        TotalLoss = TotalLoss,
        PressureAvailable = PressureAvailable,
        DevelopedLength = DevelopedLength,
        AveragePressureDrop = AveragePressureDrop,
        AdditionalLossesTotal = AdditionalLossesTotal,
        ExistingMeter = ExistingMeter,
        PipeMaterial = PipeMaterial,
        ColdWaterMaxVelocity = ColdWaterMaxVelocity,
        HotWaterMaxVelocity = HotWaterMaxVelocity,
        DevelopedLengthPercentage = DevelopedLengthPercentage,
        SectionHeader2 = SectionHeader2,
        PressureRequired2 = PressureRequired2,
        MeterSize2 = MeterSize2,
        FixtureCalculation2 = FixtureCalculation2,
        SystemLength2 = SystemLength2,
        MeterLoss2 = MeterLoss2,
        TotalLoss2 = TotalLoss2,
        PressureAvailable2 = PressureAvailable2,
        DevelopedLength2 = DevelopedLength2,
        AveragePressureDrop2 = AveragePressureDrop2,
        AdditionalLossesTotal2 = AdditionalLossesTotal2,
        AdditionalLosses = new ObservableCollection<AdditionalLoss>(AdditionalLosses),
        AdditionalLosses2 = new ObservableCollection<AdditionalLoss>(AdditionalLosses2)
      };
    }

    public void UpdatePropertiesFromData(WaterSystemData data)
    {
      if (data == null) return;

      SectionHeader1 = data.SectionHeader1;
      StreetLowPressure = data.StreetLowPressure;
      StreetHighPressure = data.StreetHighPressure;
      MeterSize = data.MeterSize;
      FixtureCalculation = data.FixtureCalculation;
      Elevation = data.Elevation;
      BackflowPressureLoss = data.BackflowPressureLoss;
      PRVPressureLoss = data.PRVPressureLoss;
      PressureRequiredOrAtUnit = data.PressureRequiredOrAtUnit;
      SystemLength = data.SystemLength;
      MeterLoss = data.MeterLoss;
      StaticLoss = data.StaticLoss;
      TotalLoss = data.TotalLoss;
      PressureAvailable = data.PressureAvailable;
      DevelopedLength = data.DevelopedLength;
      AveragePressureDrop = data.AveragePressureDrop;
      AdditionalLossesTotal = data.AdditionalLossesTotal;
      ExistingMeter = data.ExistingMeter;
      PipeMaterial = data.PipeMaterial;
      ColdWaterMaxVelocity = data.ColdWaterMaxVelocity;
      HotWaterMaxVelocity = data.HotWaterMaxVelocity;
      DevelopedLengthPercentage = data.DevelopedLengthPercentage;

      SectionHeader2 = data.SectionHeader2;
      PressureRequired2 = data.PressureRequired2;
      MeterSize2 = data.MeterSize2;
      FixtureCalculation2 = data.FixtureCalculation2;
      SystemLength2 = data.SystemLength2;
      MeterLoss2 = data.MeterLoss2;
      TotalLoss2 = data.TotalLoss2;
      PressureAvailable2 = data.PressureAvailable2;
      DevelopedLength2 = data.DevelopedLength2;
      AveragePressureDrop2 = data.AveragePressureDrop2;
      AdditionalLossesTotal2 = data.AdditionalLossesTotal2;

      AdditionalLosses.Clear();
      foreach (var loss in data.AdditionalLosses)
      {
        AdditionalLosses.Add(loss);
      }

      AdditionalLosses2.Clear();
      foreach (var loss in data.AdditionalLosses2)
      {
        AdditionalLosses2.Add(loss);
      }
    }

    #endregion INotifyPropertyChanged Implementation
  }

  public class WaterSystemData
  {
    // Properties for Section 1
    public string SectionHeader1 { get; set; }

    public double StreetLowPressure { get; set; }
    public double StreetHighPressure { get; set; }
    public double MeterSize { get; set; }
    public double FixtureCalculation { get; set; }
    public double Elevation { get; set; }
    public double BackflowPressureLoss { get; set; }
    public double PRVPressureLoss { get; set; }
    public double PressureRequiredOrAtUnit { get; set; }
    public double SystemLength { get; set; }
    public double MeterLoss { get; set; }
    public double StaticLoss { get; set; }
    public double TotalLoss { get; set; }
    public double PressureAvailable { get; set; }
    public double DevelopedLength { get; set; }
    public double AveragePressureDrop { get; set; }
    public double AdditionalLossesTotal { get; set; }
    public bool ExistingMeter { get; set; }
    public string PipeMaterial { get; set; }
    public int ColdWaterMaxVelocity { get; set; }
    public int HotWaterMaxVelocity { get; set; }
    public int DevelopedLengthPercentage { get; set; }

    // Properties for Section 2
    public string SectionHeader2 { get; set; }

    public double PressureRequired2 { get; set; }
    public double MeterSize2 { get; set; }
    public double FixtureCalculation2 { get; set; }
    public double SystemLength2 { get; set; }
    public double MeterLoss2 { get; set; }
    public double TotalLoss2 { get; set; }
    public double PressureAvailable2 { get; set; }
    public double DevelopedLength2 { get; set; }
    public double AveragePressureDrop2 { get; set; }
    public double AdditionalLossesTotal2 { get; set; }

    // Collections for additional losses
    public ObservableCollection<AdditionalLoss> AdditionalLosses { get; set; }

    public ObservableCollection<AdditionalLoss> AdditionalLosses2 { get; set; }
  }

  public class AdditionalLoss
  {
    public string Title { get; set; }
    public string Amount { get; set; }
  }
}