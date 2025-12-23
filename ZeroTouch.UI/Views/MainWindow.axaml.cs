using System;
using Avalonia.Controls;
using Avalonia.Input;
using ZeroTouch.UI.ViewModels;

namespace ZeroTouch.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Listen for key events
            this.KeyDown += OnKeyDown;
        }

        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            try
            {
                if (DataContext is not MainWindowViewModel vm)
                    return;

                switch (e.Key)
                {
                    case Key.F2:
                        vm.ToggleDebugMode();
                        await vm.SendCommand("toggle_gesture_debug");
                        break;

                    case Key.F3:
                        await vm.SendCommand("toggle_driver_debug");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }
    }
}
