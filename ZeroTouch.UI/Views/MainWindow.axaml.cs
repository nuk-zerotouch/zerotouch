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

            // listen for key events
            this.KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (e.Key == Key.F2)
                {
                    vm.ToggleDebugMode();
                }
            }
        }
    }
}
