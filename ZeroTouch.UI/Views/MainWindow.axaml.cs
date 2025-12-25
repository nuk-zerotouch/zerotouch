using System;
using System.Threading.Tasks;
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
        
        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                await vm.OnAppClosingAsync();
            }

            base.OnClosing(e);
        }

        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            _ = HandleKeyAsync(e);
        }
        
        private async Task HandleKeyAsync(KeyEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            switch (e.Key)
            {
                case Key.F2:
                    vm.ToggleDebugMode();
                    break;

                case Key.F3:
                    await vm.SendCommand("set_driver_debug", true);
                    break;
            }
        }
    }
}
