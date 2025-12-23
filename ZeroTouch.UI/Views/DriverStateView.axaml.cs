using System;
using Avalonia.Controls;
using Avalonia.Input;
using ZeroTouch.UI.ViewModels;

namespace ZeroTouch.UI.Views
{
    public partial class DriverStateView : UserControl
    {
        private bool _dragging;
        private double _startX;

        public DriverStateView()
        {
            InitializeComponent();
        }

        private void OnSliderPressed(object? sender, PointerPressedEventArgs e)
        {
            _dragging = true;
            _startX = e.GetPosition(this).X;
        }

        private void OnSliderMoved(object? sender, PointerEventArgs e)
        {
            if (!_dragging || DataContext is not DriverStateViewModel vm)
                return;

            var currentX = e.GetPosition(this).X;
            var delta = currentX - _startX;

            vm.SlideProgress = Math.Clamp(delta / 300.0, 0.0, 1.0);
        }

        private void OnSliderReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (DataContext is not DriverStateViewModel vm)
                return;

            _dragging = false;

            if (vm.SlideProgress >= 0.9)
            {
                vm.AcknowledgeFatigue();
            }
            else
            {
                vm.SlideProgress = 0.0;
            }
        }
    }
}
