using System;
using System.Threading.Tasks;

namespace BamBot.Automation
{
    public class PagePathAssertion: PageAssertion
    {
        public PagePathAssertion(string path) : this("PathAssertion", path) 
        { 
        }

        public PagePathAssertion(string assertionName, string path)
            : base(assertionName, async (page) =>
            {
                Uri pageUri = new Uri(page.Page.Url);
                string pagePath = pageUri.AbsolutePath;
                if(pagePath.EndsWith("/") && !path.EndsWith("/"))
                {
                    path += "/";
                }
                bool passed = pagePath.Equals(path, StringComparison.InvariantCultureIgnoreCase);
                string message = passed ? string.Empty : $"The Uri path did not match expected value ({pageUri?.ToString()})";
                return await Task.FromResult(new PageAssertionResult
                {
                    PageName = assertionName,
                    Passed = passed,
                    Message = message,
                });
            })
        {
        }

        public string ActualPath{ get; private set; }
    }
}
