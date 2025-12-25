using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace ZeroTouch.UI.Navigation
{
    public partial class FocusItemViewModel 
        : ObservableObject, IFocusableItem
    {
        public ICommand Command { get; }
        
        private readonly Action<FocusItemViewModel>? _onActivated;

        [ObservableProperty]
        private bool _isSelected;

        public FocusItemViewModel(ICommand command, Action<FocusItemViewModel>? onActivated = null)
        {
            Command = command;
            _onActivated = onActivated;
        }

        public void Activate()
        {
            _onActivated?.Invoke(this);
            
            if (Command?.CanExecute(null) == true)
                Command.Execute(null);
        }
    }
}
