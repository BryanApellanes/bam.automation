using System;
using System.Collections.Generic;
using System.Linq;

namespace BamBot.Automation
{
    public class SignInFailedException : Exception 
    {
        public SignInFailedException(IEnumerable<PageActionResult> pageAssertionResults)
        : base(string.Join("\r\n", pageAssertionResults.Select(par => par.ToString()).ToArray()))
        { }
    }
}
