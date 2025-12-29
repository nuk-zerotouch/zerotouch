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

            if (DataContext is not MainWindowViewModel vm)
                return;

            var dashboardVm = vm.CurrentView as MainDashboardViewModel;
            
            bool isMapPage = dashboardVm?.CurrentPageIndex == 3;
            
            switch (e.Key)
            {
                case Key.Up:
                    if (vm.ActiveFocusGroup == dashboardVm?.RouteFocusGroup)
                    {
                        dashboardVm.RouteFocusGroup.Move(-1);
                    }
                    else
                    {
                        vm.ActiveFocusGroup = vm.DockFocusGroup;
                        vm.DockFocusGroup.Move(-1);
                    }
                    break;

                case Key.Down:
                    if (vm.ActiveFocusGroup == dashboardVm?.RouteFocusGroup)
                    {
                        dashboardVm.RouteFocusGroup.Move(+1);
                    }
                    else
                    {
                        vm.ActiveFocusGroup = vm.DockFocusGroup;
                        vm.DockFocusGroup.Move(+1);
                    }
                    break;

                case Key.Left:
                    if (isMapPage)
                    {
                        if (vm.ActiveFocusGroup == dashboardVm?.RouteFocusGroup)
                        {
                            vm.ActiveFocusGroup = vm.DockFocusGroup;
                        }
                    }
                    else
                    {
                        vm.ActiveFocusGroup = vm.MusicFocusGroup;
                        vm.MusicFocusGroup.Move(-1);
                    }
                    break;

                case Key.Right:
                    if (isMapPage)
                    {
                        if (vm.ActiveFocusGroup == vm.DockFocusGroup)
                        {
                            vm.ActiveFocusGroup = dashboardVm?.RouteFocusGroup;
                        }
                    }
                    else
                    {
                        vm.ActiveFocusGroup = vm.MusicFocusGroup;
                        vm.MusicFocusGroup.Move(+1);
                    }
                    break;

                case Key.Enter:
                case Key.Space:
                    vm.ActiveFocusGroup?.Activate();
                    break;
            }
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
