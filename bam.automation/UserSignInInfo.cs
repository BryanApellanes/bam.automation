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

        public UserSignInCredentials UserSignInCredentials{ get; set; }
        public string UserNameInputSelector{ get; set; }
        public string PasswordInputSelector{ get; set; }
        public string SubmitSelector{ get; set; }
    }
}
