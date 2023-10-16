using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BamBot.Automation
{
    public class PageActionSequence
    {
        static PageActionSequence()
        {
            DefaultNavigationOptions = AutomationPage.DefaultNavigationOptions;
        }

        public PageActionSequence() : this(null) { }
        public PageActionSequence(string name): this("default", name)
        {
            Category = GetDefaultCateogry();
        }

        public PageActionSequence(string category, string name) 
        {
            Category = category;
            Name = name ?? $"{nameof(PageActionSequence)}_{GetHashCode()}";
            ScreenShotsDirectory = new DirectoryInfo("./screenshots").FullName;
            Steps = new List<PageAction>();
            NavigationOptions = DefaultNavigationOptions;
        }

        public PageActionSequence(string name, params PageAction[] steps)
        {
            Category = GetDefaultCateogry();
            Name = name;
            ScreenShotsDirectory = new DirectoryInfo("./screenshots").FullName;
            Steps = new List<PageAction>(steps);
            NavigationOptions = DefaultNavigationOptions;
        }

        public static NavigationOptions DefaultNavigationOptions { get; set; }

        public string Name{ get; set; }
        public string ScreenShotsDirectory{ get; set; }
        public List<PageAction> Steps { get; }        
        public NavigationOptions NavigationOptions{ get; set; }

        public bool HasExecuted => ExecutionResult != null;

        public bool Succeeded => (HasExecuted && (!ExecutionResult?.HasFailures).Value);

        public PageActionSequenceExecutionResult ExecutionResult { get; protected set; }

        public virtual PageActionSequence EnableDebug(string screenshotsDirectory = null)
        {
            Debug = true;
            DefaultNavigationOptions = AutomationPage.OriginalTimeoutNavigationOptions;
            NavigationOptions = AutomationPage.OriginalTimeoutNavigationOptions;
            ScreenShotsDirectory = screenshotsDirectory ?? Path.Combine($"{Environment.GetEnvironmentVariable("TMP") ?? "/tmp"}", "DebugScreenshots");
            return this;
        }

        public string GetDefaultCateogry()
        {
            return $"{nameof(PageActionSequence)}_{Name}_Category";
        }

        public bool HasErrors()
        {
            return HasErrors(out _);
        }

        public bool HasErrors(out List<PageActionResult> failures)
        {
            failures = ExecutionResult?.GetFailures();
            return (bool)ExecutionResult?.HasFailures;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to take a screenshot after each step.
        /// </summary>
        public bool Debug{ get; set; }

        /// <summary>
        /// Gets or sets the additional delay between step execution.
        /// </summary>
        public int DelayPadding{ get; set; }

        /// <summary>
        /// Gets or sets the start url.
        /// </summary>
        public string StartUrl{ get; set; }

        /// <summary>
        /// Gets the current page.
        /// </summary>
        public IAutomationPage Page { get; private set; }

        /// <summary>
        /// The event that is raised when an error occurs.  Errors may or may not be fatal and should be handled on a case by case basis.
        /// </summary>
        public event EventHandler Error;
        protected void OnError(object sender, EventArgs eventArgs)
        {
            Error?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// The event that is raised when this sequence fails to complete.
        /// </summary>
        public event EventHandler Failure;
        protected void OnFailure(object sender, EventArgs eventArgs)
        {
            Failure?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// The event that is raised when this sequence completes successfully.
        /// </summary>
        public event EventHandler Success;
        protected void OnSuccess(object sender, EventArgs eventArgs)
        {
            Success?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// The event that is raised when this sequence completes, regardless of success or failure.
        /// </summary>
        public event EventHandler Executed;
        public void OnExecuted(object sender, EventArgs eventArgs) 
        {
            Executed?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// The event that is raised when taking a screenshot fails.
        /// </summary>
        public event EventHandler ScreenShotFailed;

        public List<PageAction> GetTaggedSteps(params Tags[] tags)
        {
            HashSet<string> tagSet = new HashSet<string>(tags.Select(tag => tag.ToString()));
            return GetTaggedSteps(tagSet);
        }

        public List<PageAction> GetTaggedSteps(params string[] tags)
        {         
            return GetTaggedSteps(new HashSet<string>(tags ?? new string[] { }));
        }

        public List<PageAction> GetTaggedSteps(HashSet<string> tagSet)
        {
            return Steps.Where(pageAction =>
            {
                bool include = false;

                foreach (string actionTag in pageAction.Tags)
                {
                    include = tagSet.Contains(actionTag);
                    if (include)
                    {
                        break;
                    }
                }
                return include;
            }).ToList();
        }

        public PageActionSequence AddStep(string name, Func<IAutomationPage, Task<PageActionResult>> action, Tags[] tags)
        {
            return AddStep(name, action, tags.Select(tag => tag.ToString()).ToArray());
        }

        public PageActionSequence AddStep(string name, Func<IAutomationPage, Task<PageActionResult>> action, params string[] tags)
        {
            Steps.Add(new PageAction(name, action, tags));
            return this;
        }
        
        public PageActionSequence AddStep(string name, Action<IAutomationPage> action, params Tags[] tags)
        {
            return AddStep(name, action, tags.Select(tag => tag.ToString()).ToArray());
        }

        public PageActionSequence AddStep(string name, Action<IAutomationPage> action, params string[] tags)
        {
            return AddStep(name, action, false, tags);
        }

        public PageActionSequence AddStep(string name, Action<IAutomationPage> action, bool navigates = false)
        {
            return AddStep(name, action, navigates, new string[] { });
        }
        
        public PageActionSequence AddStep(string name, Action<IAutomationPage> action, bool navigates = false, params Tags[] tags)
        {
            return AddStep(name, action, navigates, tags.Select(tag => tag.ToString()).ToArray());
        }

        public PageActionSequence AddStep(string name, Action<IAutomationPage> action, bool navigates = false, params string[] tags)
        {
            Steps.Add(new PageAction(name, action, tags){ Navigates = navigates });
            return this;
        }

        public PageActionSequence AddStep(string category, string name, Action<IAutomationPage> action, bool navigates = false)
        {
            return AddStep(category, name, action, navigates, new string[] { });
        }

        public PageActionSequence AddStep(string category, string name, Action<IAutomationPage> action, bool navigates = false, params Tags[] tags)
        {
            return AddStep(category, name, action, navigates, tags.Select(tag => tag.ToString()).ToArray());
        }

        public PageActionSequence AddStep(string category, string name, Action<IAutomationPage> action, bool navigates = false, params string[] tags)
        {
            Steps.Add(new PageAction(category, name, action, tags){ Navigates = navigates });
            return this;
        }

        public PageActionSequence AddCategorizedStep(string name, Action<IAutomationPage> action, bool navigates = false)
        {
            return AddCategorizedStep(name, action, navigates, new string[] { });
        }
        
        public PageActionSequence AddCategorizedStep(string name, Action<IAutomationPage> action, bool navigates = false, params Tags[] tags)
        {
            return AddCategorizedStep(name, action, navigates, tags.Select(tag => tag.ToString()).ToArray());
        }

        public PageActionSequence AddCategorizedStep(string name, Action<IAutomationPage> action, bool navigates = false, params string[] tags)
        {
            return this.AddStep(Category, name, action, navigates, tags);
        }

        public PageActionSequence AddCategorizedNavigationStep(string name, Action<IAutomationPage> action)
        {
            return this.AddCategorizedStep(name, action, true);
        }

        public string Category { get; set; }

        /// <summary>
        /// Add a step that causes navigation.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public PageActionSequence AddNavigationStep(string name, Action<IAutomationPage> action)
        {
            return AddStep(name, action, true);
        }

        public PageActionSequence AddStep(PageAction pageAction)
        {
            Steps.Add(pageAction);
            return this;
        }

        public virtual Task<PageActionSequenceExecutionResult> ExecuteAsync(ReExecutionStrategy reExecutionStrategy = ReExecutionStrategy.ForErrors, bool continueOnFailure = false)
        {
            switch (reExecutionStrategy)
            {
                case ReExecutionStrategy.Invalid:
                case ReExecutionStrategy.ForErrors:
                    if(!HasExecuted || HasErrors())
                    {
                        EnsureStartUrlOrDie();
                        return Task.FromResult(ExecuteAsync(StartUrl, continueOnFailure).Result);
                    }

                    return Task.FromResult(ExecutionResult); // has already executed with no errors
                case ReExecutionStrategy.Always:
                default:
                    EnsureStartUrlOrDie();
                    return Task.FromResult(ExecuteAsync(StartUrl, continueOnFailure).Result);
            }
        }

        public async Task<PageActionSequenceExecutionResult> ExecuteAsync(string startUrl, bool continueOnFailure = false)
        {
            StartUrl = startUrl;
            IAutomationPage nextPage = AutomationPage.Open(startUrl);
            return await ExecuteAsync(nextPage, continueOnFailure);
        }

        public async Task<PageActionSequenceExecutionResult> ExecuteCategoryAsync(IAutomationPage nextPage, bool continueOnFailure = false)
        {
            return await ExecuteCategoryAsync(Category, nextPage, continueOnFailure);
        }

        public virtual async Task<PageActionSequenceExecutionResult> ExecuteCategoryAsync(string category, IAutomationPage nextPage, bool continueOnFailure = false)
        {
            return await ExecuteAsync(nextPage, GetCategorySteps(category), continueOnFailure);
        }

        int tryNumber = 0;
        public virtual async Task<PageActionSequenceExecutionResult> ExecuteAsync(IAutomationPage nextPage, bool continueOnFailure = false)
        {
            return await ExecuteAsync(nextPage, Steps, continueOnFailure);
        }

        public virtual async Task<PageActionSequenceExecutionResult> ExecuteAsync(IAutomationPage nextPage, params Tags[] tags)
        {
            return await ExecuteAsync(nextPage, GetTaggedSteps(tags), false);
        }

        public virtual async Task<PageActionSequenceExecutionResult> ExecuteAsync(IAutomationPage nextPage, List<PageAction> steps, bool continueOnFailure = false)
        {
            ++tryNumber;
            nextPage.ScreenShotsDirectory = ScreenShotsDirectory;
            List<PageActionResult> results = new List<PageActionResult>();
            int number = 0;
            foreach (PageAction step in steps)
            {
                ++number;
                Func<IAutomationPage, Task<PageActionResult>> action = step.Action;
                PageActionResult result = new PageActionResult(nextPage, true) { PageAction = step };
                try
                {
                    result = await action(nextPage);
                    result.PageAction = step;
                    if(step.Navigates)
                    {
                        Thread.Sleep(300);
                    }
                    Page = result.AutomationPage;
                }
                catch (Exception ex)
                {
                    result = new PageActionResult(nextPage, ex);
                    if (ex is EvaluationFailedException && step.Navigates)
                    {
                        result.Succeeded = true;
                    }
                    this.OnError(this, new PageActionSequenceEventArgs(this) { PageAction = step, PageActionResult = result, Exception = ex });
                }

                if (Debug)
                {
                    TakeDebugScreenshot(nextPage, number, step, result);
                }

                results.Add(result);
                if (!result.Succeeded && !continueOnFailure)
                {
                    break;
                }

                if (result.AutomationPage != null && result.AutomationPage != nextPage)
                {
                    nextPage = result.AutomationPage;
                    nextPage.ScreenShotsDirectory = ScreenShotsDirectory;
                }
                if(DelayPadding > 0)
                {
                    await Task.Delay(DelayPadding);
                }
            }

            PageActionSequenceEventArgs pageActionSequenceEventArgs = new PageActionSequenceEventArgs(this) { PageAction = results[^1].PageAction, PageActionResult = results[^1], Results = results };
            if (results.Any(r => r.Succeeded == false))
            {
                OnFailure(this, pageActionSequenceEventArgs);
            }
            else
            {
                OnSuccess(this, pageActionSequenceEventArgs);
            }
            
            ExecutionResult = new PageActionSequenceExecutionResult(this, results);
            OnExecuted(this, pageActionSequenceEventArgs);
            return ExecutionResult;
        }

        public List<PageAction> GetCategorySteps(string category = null)
        {
            category = category ?? Category;
            return Steps.Where(pageAction => pageAction.Category.Equals(category)).ToList();
        }

        private void TakeDebugScreenshot(IAutomationPage nextPage, int number, PageAction step, PageActionResult result)
        {
            lock (nextPage)
            {
                try
                {
                    string screenshotPath = Path.Combine(ScreenShotsDirectory, $"{tryNumber}_{number}-{Name.Replace(" ", "_")}-{step.Name.Replace(" ", "_")}.png");
                    if (!Directory.Exists(ScreenShotsDirectory))
                    {
                        Directory.CreateDirectory(ScreenShotsDirectory);
                    }
                    nextPage.ScreenshotAsync(screenshotPath).Wait();
                    result.ScreenShot = screenshotPath;
                }
                catch (Exception ex)
                {
                    ScreenShotFailed?.Invoke(this, new PageActionSequenceEventArgs(this) { Exception = ex });
                }
            }
        }

        private void EnsureStartUrlOrDie()
        {
            if (string.IsNullOrEmpty(StartUrl))
            {
                throw new InvalidOperationException($"StartUrl must be specified to execute PageActionSequence: {Name}");
            }
        }
    }
}
