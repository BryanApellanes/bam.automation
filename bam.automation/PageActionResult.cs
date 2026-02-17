using BamBot.Automation;

namespace BamBot
{
    public class PageActionResult //: Serializable
    {
        public PageActionResult() { }
        public PageActionResult(IAutomationPage page, bool passed = true)
        {
            AutomationPage = page;
            PageName = page.Name;
            Succeeded = passed;
        }

        public PageActionResult(IAutomationPage page, string message) : this(page, false)
        {
            Message = message;
        }

        public PageActionResult(IAutomationPage page, Exception ex) : this(page, ex.Message)
        {
        }

        public string PageName { get; set; } = null!;
        public bool Succeeded { get; set; }
        public string Message { get; set; } = null!;

        [Newtonsoft.Json.JsonIgnore]
        public IAutomationPage AutomationPage { get; set; } = null!;

        public string ScreenShot { get; set; } = null!;

        public string StepName => PageAction?.Name!;

        [Newtonsoft.Json.JsonIgnore]
        public PageAction PageAction { get; set; } = null!;

        public override string ToString()
        {
            return $"{PageName}({StepName}): Passed={Succeeded} {Message}";
        }
    }
}
