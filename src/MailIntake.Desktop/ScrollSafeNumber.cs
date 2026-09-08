namespace MailIntake.Desktop;

internal sealed class ScrollSafeNumber : NumericUpDown
{
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if(e is HandledMouseEventArgs handled)handled.Handled=true;
        // The wheel belongs to the surrounding form, even while the number has focus.
        for(Control? parent=Parent;parent!=null;parent=parent.Parent)
        {
            if(parent is not ScrollableControl { AutoScroll: true } scroll||!scroll.VerticalScroll.Visible)continue;
            int lines=SystemInformation.MouseWheelScrollLines;
            int distance=lines<0?scroll.ClientSize.Height:Math.Max(0,lines)*Font.Height;
            int y=Math.Max(0,-scroll.AutoScrollPosition.Y-e.Delta*distance/120);
            scroll.AutoScrollPosition=new Point(-scroll.AutoScrollPosition.X,y);
            break;
        }
    }
}
