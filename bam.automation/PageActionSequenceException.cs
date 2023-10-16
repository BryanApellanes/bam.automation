using System;

namespace BamBot.Automation
{
    public class PageActionSequenceException: Exception
    {
        public PageActionSequenceException(PageActionSequence pageActionSequence, string message) : base(message)
        {
            PageActionSequence = pageActionSequence;
        }

        public PageActionSequence PageActionSequence { get; set; }
    }
}
