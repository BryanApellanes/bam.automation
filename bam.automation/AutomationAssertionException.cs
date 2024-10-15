
using System;

namespace BamBot.Automation
{
    public class AutomationAssertionException : Exception 
    {
        public AutomationAssertionException(IAutomationPage page) : base($"Assertion exception occurred on page {page?.Name ?? "[null page]"}") 
        {
            AutomationPage = page;
        }

        public AutomationAssertionException(IAutomationPage page, string message) : base(message) 
        {
            AutomationPage = page;
        }

        public IAutomationPage AutomationPage{ get; set; }
    }
}
