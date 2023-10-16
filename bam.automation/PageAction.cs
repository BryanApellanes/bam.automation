using System;
using System.Threading.Tasks;

namespace BamBot.Automation
{
    public class PageAction
    {
        public static implicit operator Func<IAutomationPage, Task<PageActionResult>>(PageAction pageAction)
        {
            return pageAction.Action;
        }

        public static implicit operator PageAction(Func<IAutomationPage, Task<PageActionResult>> action)
        {
            return new PageAction(action);
        }

        public PageAction(string name, Func<IAutomationPage, Task<PageActionResult>> action, params string[] tags)
        {
            Category = GetDefaultCategory();
            Name = name;
            Action = action;
            Tags = tags ?? new string[] { };
        }

        public PageAction(string name, Action<IAutomationPage> action, params string[] tags)
        {
            Category = GetDefaultCategory();
            Name = name;
            Action = async (page) =>
            {
                action(page);
                return new PageActionResult(page);
            };
            Tags = tags ?? new string[] { };
        }

        public PageAction(string category, string name, Action<IAutomationPage> action, params string[] tags) : this(name, action, tags)
        {
            Category = category;
        }

        public PageAction(Func<IAutomationPage, Task<PageActionResult>> action)
        {
            Name = $"{nameof(PageAction)}_{GetHashCode()}";
            Action = action;
        }

        public string GetDefaultCategory()
        {
            return $"{nameof(PageAction)}_{Name}_Category";
        }

        public string Category{ get; set; }
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets an array of tags for this action.  May be used to logically group actions.
        /// </summary>
        public string[] Tags { get; set; }

        /// <summary>
        /// Gets or sets the action.
        /// </summary>
        public Func<IAutomationPage, Task<PageActionResult>> Action { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this action causes navigation.
        /// </summary>
        public bool Navigates{ get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
