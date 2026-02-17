namespace BamBot.Automation
{
    public class UserSignInEventArgs : EventArgs
    {
        public UserSignInInfo UserSignInInfo{ get; set; } = null!;
        public List<PageActionResult> SequenceResults{ get; set; } = null!;
        public string Message => Exception.Message;
        public Exception Exception{ get; set; } = null!;
    }
}
