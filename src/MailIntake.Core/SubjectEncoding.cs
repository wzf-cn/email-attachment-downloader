using System.Text;

namespace MailIntake.Core;

public static class SubjectEncoding
{
    public static string Repair(string subject)
    {
        // Only repair the recognizable GB bracket signature, never guess arbitrary foreign text.
        if(!subject.Contains("¡¾")||!subject.Contains("¡¿")||subject.Any(c=>c>255))return subject;
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding=Encoding.GetEncoding(54936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
            byte[] bytes=Encoding.Latin1.GetBytes(subject);
            string decoded=encoding.GetString(bytes);
            if(!decoded.Contains('【')||!decoded.Contains('】')||!decoded.Any(c=>c>='一'&&c<='鿿'))return subject;
            return encoding.GetBytes(decoded).SequenceEqual(bytes)?decoded:subject;
        }
        catch(ArgumentException){return subject;}
    }
}
