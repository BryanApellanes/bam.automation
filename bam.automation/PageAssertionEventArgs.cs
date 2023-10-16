// <copyright file="PageAssertionEventArgs.cs" company="Okta, Inc">
// Copyright (c) 2020 - present Okta, Inc. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.
// </copyright>

using System;

namespace BamBot.Automation
{
    public class PageAssertionEventArgs : EventArgs
    {
        public PageAssertionEventArgs()
        {
            Result = new PageAssertionResult();
        }

        public string PageName { get; set; }
        public PageAssertion PageAssertion{ get; set; }

        public PageAssertionResult Result{ get; set; }
        public string Message
        {
            get => Result?.Message;
            set => Result.Message = value;
        }
    }
}
