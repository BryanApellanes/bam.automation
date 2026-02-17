
namespace BamBot.Automation
{
    public class PageAssertionEventArgs : EventArgs
    {
        public PageAssertionEventArgs()
        {
            Result = new PageAssertionResult();
        }

        public string PageName { get; set; } = null!;
        public PageAssertion PageAssertion{ get; set; } = null!;

        public PageAssertionResult Result{ get; set; }
        public string Message
        {
            get => Result?.Message!;
            set => Result.Message = value;
        }
    }
}
