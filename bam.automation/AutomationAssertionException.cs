// <copyright file="AutomationAssertionException.cs" company="Okta, Inc">
// Copyright (c) 2020 - present Okta, Inc. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.
// </copyright>

using System;

namespace BamBot.Automation
{
    public class AutomationAssertionException : Exception 
    {
        public AutomationAssertionException(IAutomationPage page) : base($"Assertion exception occurred on page {page?.Name ?? "[null page]"}") 
        {
            AutomationPage = page;
        }

        public AutomationAssertionException(IAutomationPage page, string message) : base(message) 
        {
            AutomationPage = page;
        }

        public IAutomationPage AutomationPage{ get; set; }
    }
}
