using System.Diagnostics;

namespace MailIntake.Desktop;

internal sealed class SupportForm : Form
{
    protected override void OnShown(EventArgs e){base.OnShown(e);UiLanguage.Apply(this);}
    public SupportForm(bool sponsor)
    {
        Text=sponsor?"赞助支持":"点个 Star";
        ClientSize=sponsor?new Size(790,560):new Size(520,300);MinimumSize=Size;
        StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);
        BackColor=Color.White;Padding=new Padding(24);ShowInTaskbar=false;
        var content=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
        Controls.Add(content);
        void Note(string text,int height)=>content.Controls.Add(new Label{Text=text,Width=sponsor?720:450,Height=height,ForeColor=Design.Muted,Margin=new Padding(0,8,0,12)});
        var heading=Design.Heading(sponsor?"请作者喝杯咖啡":"觉得好用？点个 Star 支持一下",14);heading.Width=450;
        var headingRow=new FlowLayoutPanel{Width=sponsor?720:450,Height=52,WrapContents=false,Margin=Padding.Empty};headingRow.Controls.Add(heading);content.Controls.Add(headingRow);
        if(!sponsor)
        {
            Note("你的 Star 能让更多人发现这个项目。选择一个平台，登录后点击仓库页面上的 Star 即可。",54);
            var links=new FlowLayoutPanel{Width=450,Height=48};content.Controls.Add(links);
            AddLink(links,"GitHub 点 Star","https://github.com/wzf-cn/email-attachment-downloader");
            AddLink(links,"Gitee 点 Star","https://gitee.com/wzFeel/email-attachment-downloader");
        }
        else
        {
            Note("感谢你支持开发与维护。打赏完全自愿，不影响任何软件功能。",50);
            string folder=Path.Combine(AppContext.BaseDirectory,"support");
            string addressFile=Path.Combine(folder,"sponsor-url.txt");
            bool available=false;
            try
            {
                if(File.Exists(addressFile)&&Uri.TryCreate(File.ReadAllText(addressFile).Trim(),UriKind.Absolute,out var address)&&address.Scheme=="https"&&string.IsNullOrEmpty(address.UserInfo))
                {AddLink(headingRow,address.Host.Equals("paypal.me",StringComparison.OrdinalIgnoreCase)?"PayPal 赞助":"打开打赏页面",address.AbsoluteUri);available=true;}
                var codes=new FlowLayoutPanel{Width=720,Height=346,WrapContents=false,Margin=Padding.Empty};
                foreach(var (file,label) in new[]{("wechat.png","微信支付"),("alipay.jpg","支付宝")})
                {
                    string imagePath=Path.Combine(folder,file);
                    if(!File.Exists(imagePath))continue;
                    bool wechat=file=="wechat.png";
                    var card=new Panel{Width=350,Height=340,Margin=new Padding(4,0,8,0),BackColor=wechat?Color.FromArgb(7,193,96):Color.FromArgb(22,119,255)};
                    // Display only source regions. Never regenerate or alter the payment QR pixels.
                    var qr=new ImageRegion(imagePath,wechat?new Rectangle(425,477,602,602):new Rectangle(250,582,552,552))
                    {Location=new Point(40,16),Size=new Size(270,270),AccessibleName=label+"收款码",Cursor=Cursors.Hand};
                    qr.Click+=(_,_)=>OpenLink(imagePath);card.Controls.Add(qr);
                    var badge=new FlowLayoutPanel{Location=new Point(40,286),Size=new Size(270,38),Padding=new Padding(48,2,0,0),BackColor=Color.White,WrapContents=false};
                    badge.Controls.Add(new ImageRegion(imagePath,wechat?new Rectangle(290,1445,220,210):new Rectangle(305,85,145,145))
                    {Size=new Size(30,30),Margin=new Padding(0,0,8,0)});
                    badge.Controls.Add(new Label{Text=wechat?"微信支付":"支付宝支付",AutoSize=true,ForeColor=Design.Ink,Margin=new Padding(0,3,0,0)});
                    card.Controls.Add(badge);codes.Controls.Add(card);available=true;
                }
                if(codes.Controls.Count>0)content.Controls.Add(codes);else codes.Dispose();
            }
            catch { Note("打赏信息暂时无法显示，请稍后再试。",40); }
            if(!available)Note("作者尚未提供打赏方式。你也可以通过点 Star 或反馈建议支持项目，谢谢！",65);
        }
        var close=new ActionButton{Text="关闭",DialogResult=DialogResult.Cancel};
        content.Controls.Add(close);CancelButton=close;
    }

    private void AddLink(Control parent,string text,string address)
    {
        var button=new ActionButton{Text=text,Width=174,Height=40};
        button.Click+=(_,_)=>
        {
            OpenLink(address);
        };
        parent.Controls.Add(button);
    }
    private void OpenLink(string address)
    {
        try{Process.Start(new ProcessStartInfo(address){UseShellExecute=true});}
        catch{MessageBox.Show(this,"无法打开，请稍后再试。\n"+address,"打开页面失败");}
    }
    private sealed class ImageRegion : Control
    {
        private readonly Image source;
        private readonly Rectangle region;
        public ImageRegion(string path,Rectangle region)
        {
            source=Image.FromFile(path);this.region=region;DoubleBuffered=true;BackColor=Color.White;
            if(!new Rectangle(0,0,source.Width,source.Height).Contains(region)){source.Dispose();throw new ArgumentException("Unsupported payment image dimensions");}
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            e.Graphics.DrawImage(source,ClientRectangle,region,GraphicsUnit.Pixel);
        }
        protected override void Dispose(bool disposing){if(disposing)source.Dispose();base.Dispose(disposing);}
    }
}
