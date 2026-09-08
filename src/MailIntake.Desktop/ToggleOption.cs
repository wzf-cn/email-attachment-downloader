using System.Drawing.Drawing2D;

namespace MailIntake.Desktop;

internal sealed class ToggleOption : CheckBox
{
    public ToggleOption(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AutoSize=true;Cursor=Cursors.Hand;}
    public override Size GetPreferredSize(Size proposedSize)
    {
        var text=TextRenderer.MeasureText(Text,Font);
        return new Size(text.Width+(int)(54*DeviceDpi/96f),Math.Max(text.Height+8,(int)(30*DeviceDpi/96f)));
    }
    internal static void DrawSwitch(Graphics graphics,Rectangle bounds,bool on,bool enabled)
    {
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        int d=bounds.Height;using var shape=new GraphicsPath();
        shape.AddArc(bounds.X,bounds.Y,d,d,90,180);shape.AddArc(bounds.Right-d,bounds.Y,d,d,270,180);shape.CloseFigure();
        using var rail=new SolidBrush(!enabled?Color.LightGray:on?Color.FromArgb(22,119,255):Color.FromArgb(135,145,158));graphics.FillPath(rail,shape);
        int inset=Math.Max(2,d/8);using var knob=new SolidBrush(Color.White);
        graphics.FillEllipse(knob,on?bounds.Right-d+inset:bounds.X+inset,bounds.Y+inset,d-inset*2,d-inset*2);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        float scale=DeviceDpi/96f;int width=(int)(38*scale),height=(int)(22*scale);
        DrawSwitch(e.Graphics,new Rectangle(2,(Height-height)/2,width,height),Checked,Enabled);
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(width+10,0,Math.Max(0,Width-width-10),Height),Enabled?ForeColor:SystemColors.GrayText,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
    }
}
