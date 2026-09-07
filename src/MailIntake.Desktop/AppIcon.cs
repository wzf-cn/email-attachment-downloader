namespace MailIntake.Desktop;

internal static class AppIcon
{
    public static Icon Value { get; } = Load();
    private static Icon Load()
    {
        using var stream=typeof(AppIcon).Assembly.GetManifestResourceStream("MailIntake.AppIcon.ico")
            ?? throw new InvalidOperationException("Application icon resource is missing.");
        using var icon=new Icon(stream,32,32);
        return (Icon)icon.Clone();
    }
}
