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

        public string PageName { get; set; } = null!;
        public bool Passed { get; set; }
        public string Message { get; set; } = null!;
        public IAutomationPage AutomationPage{ get; set; } = null!;
        public string ScreenShot{ get; set; } = null!;
        public string StepName{ get; set; } = null!;

        public override string ToString()
        {
            return $"{PageName}({StepName}): Passed={Passed} {Message}";
        }
    }
}
