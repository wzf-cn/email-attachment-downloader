using System.Diagnostics;
using System.Text;

namespace MailIntake.Desktop;

internal sealed class FeedbackForm : EditorForm
{
    public FeedbackForm():base("意见反馈")
    {
        var kind=Choice("反馈类型","使用问题","使用问题","功能建议","界面体验","其他");
        var title=TextField("标题","");
        var detail=TextField("具体说明","",false,7);
        Field("填写提示",new Label{AutoSize=true,Text="遇到问题时，请说明操作步骤、预期结果和实际表现。请勿填写授权码、秘钥或他人的个人信息。软件不会自动附带邮箱配置、邮件或日志。"});
        Field("提交方式",new Label{AutoSize=true,Text="可先保存反馈文件。点击“复制并打开 Gitee”后，在网页中新建 Issue、粘贴内容并确认提交；可能需要登录 Gitee。"});
        string Read()
        {
            if(string.IsNullOrWhiteSpace(title.Text)||string.IsNullOrWhiteSpace(detail.Text))throw new ArgumentException("请填写标题和具体说明。");
            return $"标题：{title.Text.Trim()}\n类型：{kind.Text}\n软件版本：{Application.ProductVersion.Split('+')[0]}\n时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\n\n{detail.Text.Trim()}\n";
        }
        void AddAction(string label,Action action)
        {
            var button=new ActionButton{Text=label,Width=TextRenderer.MeasureText(label,Font).Width+32,Height=38};
            button.Click+=(_,_)=>{try{action();}catch(Exception e){MessageBox.Show(this,e.Message,"反馈未完成");}};
            Footer.Controls.Add(button);
        }
        AddAction("保存到本地",()=>
        {
            string text=Read(),folder=Path.Combine(LocalSettings.Root,"Feedback");Directory.CreateDirectory(folder);
            using var picker=new SaveFileDialog{Title="保存意见反馈",Filter="反馈文件|*.txt",FileName="意见反馈-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt",InitialDirectory=folder,OverwritePrompt=true};
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            File.WriteAllText(picker.FileName,text,new UTF8Encoding(true));MessageBox.Show(this,"反馈已保存到本地，尚未提交。","意见反馈");
        });
        AddAction("复制并打开 Gitee",()=>
        {
            Clipboard.SetText(Read());
            Process.Start(new ProcessStartInfo("https://gitee.com/wzFeel/email-attachment-downloader/issues"){UseShellExecute=true});
            MessageBox.Show(this,"反馈内容已复制。请在 Gitee 网页中新建 Issue，粘贴内容并提交。","尚需在网页提交");
        });
    }
}
