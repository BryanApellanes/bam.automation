namespace BamBot.Automation
{
    public class UserSignInInfo
    {
        public UserSignInInfo()
        //this(Deserialize.FromEnvironmentVariables<UserSignInCredentials>())
        {
        }

        public UserSignInInfo(UserSignInCredentials userSignInCredentials)
        {
            UserSignInCredentials = userSignInCredentials;
        }

        public UserSignInCredentials UserSignInCredentials{ get; set; } = null!;
        public string UserNameInputSelector{ get; set; } = null!;
        public string PasswordInputSelector{ get; set; } = null!;
        public string SubmitSelector{ get; set; } = null!;
    }
}
