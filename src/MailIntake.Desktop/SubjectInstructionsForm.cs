using MailIntake.Core;

namespace MailIntake.Desktop;

internal sealed class SubjectInstructionsForm : Form
{
    protected override void OnShown(EventArgs e){base.OnShown(e);UiLanguage.Apply(this);}
    public SubjectInstructionsForm(string topic,MailRule rule)
    {
        Text="主题填写说明";Size=new Size(760,470);MinimumSize=new Size(600,380);StartPosition=FormStartPosition.CenterParent;
        Font=new Font("Microsoft YaHei UI",10);Padding=new Padding(22);BackColor=Color.White;
        var text=new TextBox{Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Text=SubjectInstructions.Generate(topic,rule,UiLanguage.English)};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=56,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,12,0,0)};
        var copy=new ActionButton{Text="复制说明",Width=150};
        copy.Click+=(_,_)=>{try{Clipboard.SetText(text.Text);copy.Text=UiLanguage.T("已复制");}catch{MessageBox.Show(this,UiLanguage.T("复制失败，请选中文字后按 Ctrl+C。"));}};
        var close=new ActionButton{Text="关闭",DialogResult=DialogResult.Cancel};actions.Controls.Add(close);actions.Controls.Add(copy);
        var note=new Label{Text="可修改下面的说明，再复制发给提交邮件的人。",Dock=DockStyle.Top,Height=42,ForeColor=Design.Muted};
        Controls.Add(text);Controls.Add(actions);Controls.Add(note);CancelButton=close;
    }
}
