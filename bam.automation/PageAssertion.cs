using BamBot.Automation;
using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace BamBot.Automation
{
    public class PageAssertion
    {
        protected PageAssertion() { }

        public PageAssertion(string name, Action<IAutomationPage> action)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"{nameof(name)} must be specified");
            }

            this.Name = name;
            this.AssertionFunction = async (page) =>
            {
                try
                {
                    action(page);
                    return new PageAssertionResult(page);
                }
                catch (Exception ex)
                {
                    return new PageAssertionResult(page, ex);
                }
            };
        }

        public PageAssertion(string name, Func<IAutomationPage, Task<PageAssertionResult>> assertionFunction)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"{nameof(name)} must be specified");
            }

            this.Name = name;
            this.AssertionFunction = assertionFunction ?? throw new ArgumentException($"{nameof(assertionFunction)} must be specified");
        }

        public PageAssertion(string name, string selectorThatShouldExist)
            : this(name, async (page) =>
            {
                ElementHandle handle = await page.QuerySelectorAsync(selectorThatShouldExist);
                return new PageAssertionResult
                {
                    PageName = name,
                    Message = handle != null ? string.Empty : $"No element found matching selector ({selectorThatShouldExist})",
                    Passed = handle != null,
                };
            })
        {
        }

        public PageAssertion(string pageName, params string[] selectorsThatShouldExist)
            : this(pageName, async (page) =>
            {
                if (selectorsThatShouldExist == null || selectorsThatShouldExist.Length == 0)
                {
                    throw new ArgumentException("at least one selector must be specified");
                }

                foreach (string selector in selectorsThatShouldExist)
                {
                    ElementHandle handle = await page.QuerySelectorAsync(selector);
                    if (handle == null)
                    {
                        return new PageAssertionResult { PageName = pageName, Message = $"No element matched selector ({selector})", Passed = false };
                    }
                }

                return new PageAssertionResult { PageName = pageName, Passed = true };
            })
        {
        }

        public PageAssertion(string pageName, Uri uriPath)
            : this(pageName, async (page) =>
            {
                Uri pageUri = new Uri(page.Url);
                string pagePath = pageUri.AbsolutePath;
                bool passed = pagePath.Equals(uriPath.AbsolutePath, StringComparison.InvariantCultureIgnoreCase);
                string message = passed ? string.Empty : $"The Uri path did not match expected value ({pageUri?.ToString()})";
                return await Task.FromResult(new PageAssertionResult
                {
                    PageName = pageName,
                    Passed = passed,
                    Message = message,
                });
            })
        {
        }

        public PageAssertion(string pageName, Uri uriShouldBe, params string[] selectorsThatShouldExist)
            : this(pageName, async (page) =>
            {
                bool passed = true;
                string message = string.Empty;
                if (!page.Url.Equals(uriShouldBe.ToString()))
                {
                    passed = false;
                    message = $"The Uri did not match expected value ({uriShouldBe?.ToString()})";
                }

                if (passed)
                {
                    foreach (string selector in selectorsThatShouldExist)
                    {
                        ElementHandle handle = await page.QuerySelectorAsync(selector);
                        if (handle == null)
                        {
                            return new PageAssertionResult { PageName = pageName, Message = $"No element matched selector ({selector})", Passed = false };
                        }
                    }
                }

                return new PageAssertionResult { PageName = pageName, Passed = true };
            })
        {
        }

        /// <summary>
        /// Gets or sets the name of this page assertion.
        /// </summary>
        public string Name { get; private set; }
                
        /// <summary>
        /// The event that is raised when this assertion fails.
        /// </summary>
        public event EventHandler AssertionFailed;

        /// <summary>
        /// The event that is raised when this assertion passes on execution.
        /// </summary>
        public event EventHandler AssertionPassed;

        protected Func<IAutomationPage, Task<PageAssertionResult>> AssertionFunction { get; }

        public async Task<PageAssertionResult> ExecuteAsync(IAutomationPage page)
        {
            return await Task.Run(() => Execute(page));
        }

        public PageAssertionResult Execute(IAutomationPage page)
        {
            PageAssertionResult assertionResult = AssertionFunction(page).Result;
            PageAssertionEventArgs eventArgs = new PageAssertionEventArgs { PageName = this.Name, PageAssertion = this, Result = assertionResult };
            if (!assertionResult.Passed)
            {
                AssertionFailed?.Invoke(this, eventArgs);
            }
            else
            {
                AssertionPassed?.Invoke(this, eventArgs);
            }

            return assertionResult;
        }
    }
}
