using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ZeroTouch.UI.ViewModels;

namespace ZeroTouch.UI.Views
{
    public partial class MainDashboardView : UserControl
    {
        public MainDashboardView()
        {
            InitializeComponent();
        }

        private void MusicSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (DataContext is MainDashboardViewModel vm)
            {
                vm.SeekCommand.Execute((long)e.NewValue);
            }
        }
    }
}
