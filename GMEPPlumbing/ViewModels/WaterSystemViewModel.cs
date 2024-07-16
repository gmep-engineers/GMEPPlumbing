using GMEPPlumbing.Services;
using System.ComponentModel;

namespace GMEPPlumbing.ViewModels
{
  public class WaterSystemViewModel : INotifyPropertyChanged
  {
    private readonly WaterCalculationService _calculationService;

    public WaterSystemViewModel(WaterCalculationService calculationService)
    {
      _calculationService = calculationService;
    }

    private double _lowPressure;

    public double LowPressure
    {
      get => _lowPressure;
      set
      {
        _lowPressure = value;
        OnPropertyChanged(nameof(LowPressure));
        CalculateAveragePressureDrop();
      }
    }

    private double _highPressure;

    public double HighPressure
    {
      get => _highPressure;
      set
      {
        _highPressure = value;
        OnPropertyChanged(nameof(HighPressure));
        CalculateAveragePressureDrop();
      }
    }

    private double _averagePressureDrop;

    public double AveragePressureDrop
    {
      get => _averagePressureDrop;
      set
      {
        _averagePressureDrop = value;
        OnPropertyChanged(nameof(AveragePressureDrop));
      }
    }

    private void CalculateAveragePressureDrop()
    {
      AveragePressureDrop = _calculationService.CalculateAveragePressureDrop(LowPressure, HighPressure);
    }

    // Implement INotifyPropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}