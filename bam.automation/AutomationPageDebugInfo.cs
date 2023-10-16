using System.IO;

namespace BamBot.Automation
{
    public class AutomationPageDebugInfo
    {
        public FileInfo ScreenShot { get; set; }
        public string Message{ get; set; }
        public AutomationPage AutomationPage{ get; set; }
    }
}
