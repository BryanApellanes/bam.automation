using BamBot.Automation;
using System;

namespace BamBot.Automation
{
    public class PageAssertionResult
    {
        public PageAssertionResult() { }
        public PageAssertionResult(IAutomationPage page, bool passed = true) 
        {
            AutomationPage = page;
            PageName = page.Name;
            Passed = passed;
        }

        public PageAssertionResult(IAutomationPage page, string message) : this(page, false)
        {
            Message = message;
        }

        public PageAssertionResult(IAutomationPage page, Exception ex) : this(page, ex.Message)
        {
        }

        public string PageName { get; set; }
        public bool Passed { get; set; }
        public string Message { get; set; }
        public IAutomationPage AutomationPage{ get; set; }
        public string ScreenShot{ get; set; }
        public string StepName{ get; set; }

        public override string ToString()
        {
            return $"{PageName}({StepName}): Passed={Passed} {Message}";
        }
    }
}
