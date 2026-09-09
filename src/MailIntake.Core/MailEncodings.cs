using System.Runtime.CompilerServices;
using System.Text;
namespace MailIntake.Core;
internal static class MailEncodings
{
    [ModuleInitializer]
    internal static void Initialize()=>Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
}
