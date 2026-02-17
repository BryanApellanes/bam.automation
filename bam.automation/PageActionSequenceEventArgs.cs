namespace BamBot.Automation
{
    public class PageActionSequenceEventArgs : EventArgs
    {
        public PageActionSequenceEventArgs(PageActionSequence pageActionSequence)
        {
            PageActionSequence = pageActionSequence;
        }
        public IAutomationPage Page => PageActionSequence.Page;
        public PageActionSequence PageActionSequence{ get; set; }
        public PageActionResult PageActionResult{ get; set; } = null!;
        public PageAction PageAction{ get; set; } = null!;
        public List<PageActionResult> Results{ get; set; } = null!;
        public Exception Exception { get; set; } = null!;
    }
}
