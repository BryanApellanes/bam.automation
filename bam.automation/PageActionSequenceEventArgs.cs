using System;
using System.Collections.Generic;

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
        public PageActionResult PageActionResult{ get; set; }
        public PageAction PageAction{ get; set; }
        public List<PageActionResult> Results{ get; set; }
        public Exception Exception { get; set; }
    }
}
