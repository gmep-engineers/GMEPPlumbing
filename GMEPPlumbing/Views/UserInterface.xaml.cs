using GMEPPlumbing.Services;
using GMEPPlumbing.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace GMEPPlumbing.Views
{
  public partial class UserInterface : UserControl
  {
    private WaterSystemViewModel _viewModel;

    public UserInterface(WaterSystemViewModel viewModel)
    {
      InitializeComponent();

      _viewModel = viewModel;

      DataContext = _viewModel;

      DynamicListView.ItemsSource = _viewModel.AdditionalLosses;
      DynamicListView2.ItemsSource = _viewModel.AdditionalLosses2;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
      if (!string.IsNullOrWhiteSpace(TitleTextBox.Text) && !string.IsNullOrWhiteSpace(ValueTextBox.Text))
      {
        _viewModel.AddAdditionalLoss(TitleTextBox.Text, ValueTextBox.Text);
        TitleTextBox.Clear();
        ValueTextBox.Clear();
      }
    }

    private void AddButton2_Click(object sender, RoutedEventArgs e)
    {
      if (!string.IsNullOrWhiteSpace(TitleTextBox2.Text) && !string.IsNullOrWhiteSpace(ValueTextBox2.Text))
      {
        _viewModel.AddAdditionalLoss2(TitleTextBox2.Text, ValueTextBox2.Text);
        TitleTextBox2.Clear();
        ValueTextBox2.Clear();
      }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
      var button = (Button)sender;
      var itemToRemove = (AdditionalLoss)button.DataContext;
      _viewModel.RemoveAdditionalLoss(itemToRemove);
    }

    private void RemoveButton2_Click(object sender, RoutedEventArgs e)
    {
      var button = (Button)sender;
      var itemToRemove = (AdditionalLoss)button.DataContext;
      _viewModel.RemoveAdditionalLoss2(itemToRemove);
    }

    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
      if (sender is TextBox textBox)
      {
        textBox.Dispatcher.BeginInvoke(new Action(() =>
        {
          textBox.SelectAll();
        }), DispatcherPriority.Input);
      }
    }

    private void ComboBox_GotFocus(object sender, RoutedEventArgs e)
    {
      ComboBox comboBox = sender as ComboBox;
      if (comboBox != null)
      {
        comboBox.IsDropDownOpen = true;
      }
    }

    private void CreateBasicResidentialWaterTable_Click(object sender, RoutedEventArgs e)
    {
      _viewModel.BuildBasicResidentialWaterTable();
    }

    private void CreateBasicCommercialWaterTable_Click(object sender, RoutedEventArgs e)
    {
      _viewModel.BuildBasicCommercialWaterTable();
    }

    private void PART_EditableTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
      var textBox = sender as TextBox;
      if (textBox == null) return;

      var comboBox = textBox.TemplatedParent as ComboBox;
      if (comboBox == null) return;

      // Calculate the visible width of the TextBox
      double visibleWidth = comboBox.ActualWidth - textBox.Padding.Left - textBox.Padding.Right - 23; // 23 is for the dropdown button

      // Get the position of the caret
      int caretIndex = textBox.CaretIndex;

      // Calculate the width of the text up to the caret
      var formattedText = new FormattedText(
          textBox.Text.Substring(0, caretIndex),
          System.Globalization.CultureInfo.CurrentCulture,
          FlowDirection.LeftToRight,
          new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
          textBox.FontSize,
          Brushes.Black,
          VisualTreeHelper.GetDpi(textBox).PixelsPerDip);

      double caretPosition = formattedText.Width;

      // Calculate the current visible range
      double scrollPosition = textBox.HorizontalOffset;
      double visibleStart = scrollPosition;
      double visibleEnd = scrollPosition + visibleWidth;

      // If the caret is beyond the visible area, scroll the text
      if (caretPosition > visibleEnd)
      {
        double offsetNeeded = caretPosition - visibleWidth;
        textBox.ScrollToHorizontalOffset(offsetNeeded);
      }
      else if (caretPosition < visibleStart)
      {
        // If the caret is before the visible area, scroll back
        textBox.ScrollToHorizontalOffset(caretPosition);
      }
      // If the caret is within the visible area, don't scroll
    }
  }
}