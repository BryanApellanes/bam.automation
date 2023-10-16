using BamBot.Automation;
using System;

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

        public string PageName { get; set; }
        public bool Succeeded { get; set; }
        public string Message { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public IAutomationPage AutomationPage { get; set; }

        public string ScreenShot { get; set; }

        public string StepName => PageAction?.Name;

        [Newtonsoft.Json.JsonIgnore]
        public PageAction PageAction { get; set; }

        public override string ToString()
        {
            return $"{PageName}({StepName}): Passed={Succeeded} {Message}";
        }
    }
}
