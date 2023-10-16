using System;
using System.Collections.Generic;

namespace BamBot.Automation
{
    public class UserSignInEventArgs : EventArgs
    {
        public UserSignInInfo UserSignInInfo{ get; set; }
        public List<PageActionResult> SequenceResults{ get; set; }
        public string Message => Exception.Message;
        public Exception Exception{ get; set; }
    }
}
