using System.Collections.Generic;
using System.Linq;

namespace ZeroTouch.UI.Navigation
{
    public class FocusGroup
    {
        public IList<IFocusableItem> Items { get; }
        public int SelectedIndex { get; private set; }

        public FocusGroup(IEnumerable<IFocusableItem> items)
        {
            Items = items.ToList();

            if (Items.Count > 0)
                SetSelected(0);
        }

        public void Move(int delta)
        {
            if (Items.Count == 0) return;

            Items[SelectedIndex].IsSelected = false;

            SelectedIndex = (SelectedIndex + delta + Items.Count) % Items.Count;

            Items[SelectedIndex].IsSelected = true;
        }

        public void Activate()
        {
            if (Items.Count == 0) return;
            Items[SelectedIndex].Activate();
        }

        private void SetSelected(int index)
        {
            SelectedIndex = index;
            Items[index].IsSelected = true;
        }

        public void SelectItem(IFocusableItem item)
        {
            var index = Items.IndexOf(item);
            if (index >= 0 && index != SelectedIndex)
            {
                Items[SelectedIndex].IsSelected = false;
                SelectedIndex = index;
                Items[SelectedIndex].IsSelected = true;
            }
        }
    }
}
