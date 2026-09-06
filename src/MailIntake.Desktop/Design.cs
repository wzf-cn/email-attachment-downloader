namespace MailIntake.Desktop;

internal sealed class ActionButton : AntdUI.Button
{
    public ActionButton()
    {
        Height=38; Width=112; Radius=6; BorderWidth=1; WaveSize=0;
        DefaultBorderColor=Color.FromArgb(220,226,235);
        Margin=new Padding(0,0,8,6);
    }
}

internal static class Design
{
    public static readonly Color Background=Color.FromArgb(244,246,250);
    public static readonly Color Ink=Color.FromArgb(30,41,59);
    public static readonly Color Muted=Color.FromArgb(100,116,139);
    public static Label Heading(string text,int size=15)=>new(){Text=text,AutoSize=false,Height=48,Dock=DockStyle.Top,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Microsoft YaHei UI",size,FontStyle.Bold),ForeColor=Ink};
    public static Panel Card(string title,Control content,Control? toolbar=null)
    {
        var card=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(18,6,18,14)};
        card.Controls.Add(content);
        if(toolbar!=null)card.Controls.Add(toolbar);
        card.Controls.Add(Heading(title,11));return card;
    }
}

// A single page is visible at a time; controls stay alive so unsaved edits survive navigation.
internal sealed class PageDeck : Panel
{
    private readonly FlowLayoutPanel navigation;
    private readonly Panel content=new(){Dock=DockStyle.Fill,Padding=new Padding(20),BackColor=Design.Background};
    private readonly List<(Control Page,ActionButton Button)> pages=[];
    public PageDeck(bool sidebar)
    {
        Dock=DockStyle.Fill;
        navigation=new(){Dock=sidebar?DockStyle.Left:DockStyle.Top,Width=176,Height=64,
            FlowDirection=sidebar?FlowDirection.TopDown:FlowDirection.LeftToRight,WrapContents=false,
            BackColor=Color.White,Padding=new Padding(12)};
        Controls.Add(content);Controls.Add(navigation);
    }
    public void AddPage(string title,Control page)
    {
        page.Dock=DockStyle.Fill;page.Visible=false;content.Controls.Add(page);
        var button=new ActionButton{Text=title,Width=navigation.Dock==DockStyle.Left?150:142,Height=40,BorderWidth=0};
        int index=pages.Count;button.Click+=(_,_)=>SelectPage(index);navigation.Controls.Add(button);pages.Add((page,button));
        if(index==0)SelectPage(0);
    }
    public void SelectPage(int index)
    {
        for(int i=0;i<pages.Count;i++)
        {
            pages[i].Page.Visible=i==index;
            pages[i].Button.Type=i==index?AntdUI.TTypeMini.Primary:AntdUI.TTypeMini.Default;
        }
        pages[index].Page.BringToFront();
    }
    public int PageCount=>pages.Count;
}

