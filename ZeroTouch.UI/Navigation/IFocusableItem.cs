using System.Windows.Input;

namespace ZeroTouch.UI.Navigation
{
    public interface IFocusableItem
    {
        bool IsSelected { get; set; }
        void Activate();
        
        ICommand Command { get; }
    }
}
