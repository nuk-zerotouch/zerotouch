using System.Windows.Input;

namespace ZeroTouch.UI.Navigation
{
    public interface IFocusableItem
    {
        bool IsSelected { get; set; }
        void Activate();
        bool IsAnimating { get; }
        bool IsArmed { get; }

        ICommand Command { get; }
    }
}
