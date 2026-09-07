namespace MailIntake.Desktop;

// Localize application prompts without altering persisted data or email content.
internal static class MessageBox
{
    public static DialogResult Show(IWin32Window owner,string text,string? caption=null,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None)
        =>System.Windows.Forms.MessageBox.Show(owner,UiLanguage.T(text),UiLanguage.T(caption??"邮件接收管理"),buttons,icon);
    public static DialogResult Show(string text,string? caption=null,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1)
        =>System.Windows.Forms.MessageBox.Show(UiLanguage.T(text),UiLanguage.T(caption??"邮件接收管理"),buttons,icon,defaultButton);
}
