# bam.automation

Browser automation framework built on PuppeteerSharp for scripting web page interactions, action sequences, and assertions.

## Overview

The `bam.automation` project is a .NET 10 class library (namespace `BamBot.Automation`) that provides a high-level abstraction over PuppeteerSharp for automating browser-based workflows. It manages a headless Chromium browser lifecycle, wraps page interactions (clicking, typing, navigation, element queries), and supports building multi-step action sequences with assertions.

The core workflow involves creating `PageAction` steps, composing them into a `PageActionSequence`, and executing the sequence against a target URL. Each step produces a `PageActionResult` indicating success or failure. Assertions (`PageAssertion`, `PagePathAssertion`) verify page state such as element presence or URL path matching. The framework supports tagging steps for selective execution, debug screenshots between steps, configurable navigation timeouts, and re-execution strategies for error recovery.

Sign-in automation is supported through `UserSignInInfo` and `UserSignInCredentials`, which capture login form selectors and credentials. The `CompositePageActionSequence` extends the basic sequence with pre-execution dependency sequences, though it is marked as a TODO for simplification.

## Key Classes

| Class | Description |
|-------|-------------|
| `AutomationPage` | Core page wrapper around PuppeteerSharp's `IPage`. Manages browser lifecycle (launch, download, dispose), provides element queries, keyboard input, navigation, screenshot capture, and assertion helpers. |
| `IAutomationPage` | Interface for page interactions: query selectors, element text/value retrieval, click, keyboard input, navigation, assertion methods, and screenshot directory configuration. |
| `IPage` | Minimal page interface: `Name`, `Url`, `GoToAsync`, `ScreenshotAsync`. |
| `PageAction` | Represents a single named step in an automation sequence. Wraps a `Func<IAutomationPage, Task<PageActionResult>>` with optional tags and a navigation flag. Supports implicit conversions. |
| `PageActionSequence` | Ordered collection of `PageAction` steps with execution, debug screenshot, error/success events, tagged step filtering, and re-execution strategies. The main orchestrator for automation workflows. |
| `CompositePageActionSequence` | Extends `PageActionSequence` with pre-execution dependency sequences that must succeed before the main sequence runs. Marked with a TODO for simplification. |
| `PageActionResult` | Result of a single action step: success/failure, message, screenshot path, and reference to the automation page. |
| `PageActionSequenceExecutionResult` | Aggregate result of an entire sequence execution: collects all `PageActionResult` items and provides failure queries. |
| `PageAssertion` | Assertion against page state. Supports element-exists checks, URL matching, and custom assertion functions. Raises `AssertionPassed`/`AssertionFailed` events. |
| `PagePathAssertion` | Specialized assertion verifying the current URL path matches an expected path. |
| `PageAssertionResult` | Result of an assertion execution: passed/failed, message, page name. |
| `UserSignInInfo` | Captures sign-in form selectors (`UserNameInputSelector`, `PasswordInputSelector`, `SubmitSelector`) and associated `UserSignInCredentials`. |
| `UserSignInCredentials` | Simple data class holding `SignInUrl`, `UserName`, and `Password`. |
| `Tags` | Enum for categorizing page actions: `Validation`, `Throws`, `Action`, `Click`, `Keyboard`, `Submit`, `Read`. |
| `ReExecutionStrategy` | Enum controlling re-execution behavior: `Invalid`, `ForErrors` (default), `Always`. |
| `AutomationAssertionException` | Exception thrown when a page assertion fails. |
| `SignInFailedException` | Exception thrown when a sign-in sequence fails, aggregating all action results. |
| `AutomationPageDebugInfo` | Debug data container: screenshot file, message, and associated page reference. |
| `PageActionSequenceException` | Exception associated with a `PageActionSequence` failure. |
| `PageActionSequenceEventArgs` | Event args carrying the sequence, current action, result, all results, and any exception. |
| `PageAssertionEventArgs` | Event args for assertion passed/failed events. |

## Dependencies

### Project References
- `bam.base` -- core framework primitives (`BamProfile` for screenshot paths)

### Package References
- `PuppeteerSharp` 20.0.2 -- headless Chromium browser automation

## Usage Examples

### Basic page automation
```csharp
using BamBot.Automation;

// Open a page and check for an element
using var page = AutomationPage.Open("https://example.com");
bool hasHeading = await page.IsPresentAsync("h1");
string text = await page.GetElementTextAsync("h1");
```

### Building and executing an action sequence
```csharp
using BamBot.Automation;

var sequence = new PageActionSequence("Login Flow");
sequence.StartUrl = "https://myapp.com/login";

sequence.AddStep("Enter username", (page) =>
{
    page.KeysAsync("#username", "admin").Wait();
});

sequence.AddStep("Enter password", (page) =>
{
    page.KeysAsync("#password", "secret").Wait();
});

sequence.AddNavigationStep("Click submit", (page) =>
{
    page.ClickAsync("#submit").Wait();
});

var result = await sequence.ExecuteAsync();
if (result.Success)
{
    Console.WriteLine("Login succeeded");
}
else
{
    foreach (var failure in result.GetFailures())
    {
        Console.WriteLine($"Failed at: {failure.StepName} - {failure.Message}");
    }
}
```

### Assertions
```csharp
using BamBot.Automation;

var page = AutomationPage.Open("https://example.com/dashboard");

// Assert element is present
await page.AssertElementIsPresentAsync("#main-content");

// Assert at expected path
await page.AssertIsAtPathAsync("/dashboard");

// Programmatic assertion
var pathAssertion = new PagePathAssertion("/dashboard");
var result = await pathAssertion.ExecuteAsync(page);
Console.WriteLine($"At dashboard: {result.Passed}");
```

## Known Gaps / Not Yet Implemented

- **`CompositePageActionSequence`** -- Marked with `TODO: revisit this concept; unnecessarily complex for now`. The pre-execution dependency chain works but is flagged for redesign.
- **`AllExecuted` and `AnyFailed` in `CompositePageActionSequence`** -- These methods accept a nullable `pageActionSequences` parameter but can be called with `null` from certain code paths (the default parameter is not applied when called from `ExecuteAsync`), which may cause a `NullReferenceException`.
- **Browser cleanup** -- `TryCloseBrowser` uses broad exception swallowing. Browser resource management could be more robust.
