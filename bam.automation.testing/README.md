# bam.automation.testing

Reusable, bam.test-integrated components for browser-driven end-to-end journeys that mimic a real user — a visible, persistent browser session, secrets from an env file, polling waits, a journey report with per-step screenshots, a Stripe hosted-Checkout page object and a bam.test adapter — so a product's e2e test is a thin script over framework pieces.

## Overview

`bam.automation` wraps PuppeteerSharp (`AutomationPage`, `PageAction`, `PageActionSequence`, `PageAssertion`) but launches headless only and has no notion of a monitored run. This project (namespace `Bam.Automation.Testing`, net10.0) adds what a journey needs on top of it, without new packages — PuppeteerSharp comes through `bam.automation`, JSON through `bam.base`'s Newtonsoft extensions, test authoring through `bam.test`.

A journey is an ordered list of `JourneyStep`s run against one page of a `BrowserSession`. Each step becomes a `PageAction` on the journey's `PageActionSequence`, so existing sequence events keep firing, and the journey adds per-step timing, screenshots, stop-on-first-failure, secret scrubbing and a `JourneyResult` the test asserts on. `JourneyReport` renders that result to Markdown and JSON and can follow a run, rewriting the report after every step so whoever is monitoring sees it move. Secrets come from a `KEY=VALUE` env file (`EnvFileSecrets`) and never appear in names, messages, reports or `ToString()` output.

Journeys hit real sites, so they are **integration tests** (`--it`); this project's own unit tests (`--ut`) exercise the engine against a fake page and the browser-launching tests against local HTML fixtures.

## Key Classes

| Class | Description |
|-------|-------------|
| `BrowserSessionOptions` | Launch policy: `Headless` (default **false** — a real user sees the browser), `UserDataDirectory` (persistent profile, default `~/.bam/automation/profiles/default`, so Stripe Link, autofill and cookies survive runs), `WindowWidth`/`WindowHeight`, `SlowMotionMilliseconds`, `DefaultNavigationTimeout`, `ExecutablePath`, `AdditionalArguments`. `FromEnvironment()` flips to headless under `CI` or `BAM_AUTOMATION_HEADLESS=1`; `BAM_AUTOMATION_HEADED=1` wins; `BAM_AUTOMATION_PROFILE` names the profile. `ToLaunchOptions()` maps to PuppeteerSharp. |
| `BrowserSession` | One browser process launched from the options via `AutomationPage.LaunchBrowserAsync`; `OpenAsync(url)` creates `AutomationPage`s that do **not** own the browser (disposing a page closes a tab, disposing the session closes the browser). `GetShared(options)` hands out one process-wide session per option set — the bam.test runner constructs a container per test method, so the session cannot be owned per instance. |
| `EnvFileSecrets` | Reads `KEY=VALUE` files (the `~/.claude/secrets/<system>.<env>.env` hand-off standard): ignores comments, blanks and `export`, strips quotes. `Get`/`TryGet`/`GetOptional`, `IsBlank`, `Require(keys…)` (throws `MissingSecretException` naming the keys, never values), `Masked(key)` → `len=16 prefix=4242` for reports, `Scrub(text)` → replaces any value with `[REDACTED:KEY]`. |
| `Journey` | Named, ordered steps over one page. Fluent builders mirror `JourneyStep` (`Navigate`, `Click`, `Type`, `TypeSecret`, `WaitForSelector`, `WaitForUrl`, `WaitUntil`, `Assert`, `Screenshot`, `Do`), plus `ContinueOnFailure`, `DefaultScreenshotPolicy`, `ArtifactsDirectory`, `Note`/`NoteMasked`, `RegisterSecrets`, `StepCompleted`/`Completed` events. `RunAsync(BrowserSession)` opens the page at `StartUrl`; `RunAsync(IAutomationPage)` runs against any page (fakes included). |
| `JourneyStep` | One action with optional `PageAssertion`s (`Expect`, `ExpectSelector`), a `ScreenshotPolicy` and `Navigates`. Static builders: `Navigate(url)`, `Click(selector)`, `Type(selector, text)`, `TypeSecret(selector, secrets, key)` (name carries the key, never the value), `WaitForSelector`, `WaitForUrl(fragment)` / `WaitForUrl(description, predicate)` (follows cross-origin redirects such as a hosted checkout), `WaitUntil(description, condition)` (polls anything — an order status, an inbox), `Assert(assertion)`, `Screenshot(name)`, `Custom(name, action)`. |
| `JourneyResult` / `JourneyStepResult` | The run outcome: per-step `Status` (`Passed`/`Failed`/`Skipped`), `Duration`, `Url`, `ScreenshotPath`, scrubbed `Message` and `ExceptionType`; overall `Succeeded`, `FailureSummary`, counts, `ArtifactsDirectory`, `ReportPath`, report-safe `Notes`. |
| `JourneyReport` | `ToMarkdown()` (status, timings, a step table with urls and screenshot links, the failure, the notes), `ToJson()`, `Write(result, directory)` → `report.md` + `result.json`, and `Follow(journey)` which rewrites `report.md` after every step for live monitoring. |
| `ScreenshotPolicy` | `Inherit`, `Never`, `OnFailure`, `Always` (the journey default — the report shows what the user saw at every step). **Screenshots are not scrubbed**: a PNG shows whatever the page showed, so `TypeSecret` and the Stripe fill / dismiss-Link / submit steps are `Never` regardless of the journey default. Give any custom step that types or reveals a secret the same policy. |
| `StripeCheckoutPage` | Page object for Stripe hosted Checkout (`checkout.stripe.com`): `WaitForFormAsync`, `FillCardAsync(secrets)` (email if asked, number, expiry, CVC, name, postal code and country when shown; refuses with `MissingSecretException` before typing anything if a required value is blank), `DismissLinkIfShownAsync`, `SubmitAsync`, `WaitForRedirectAsync(hostOrPath, timeout)`. Fields are found on the page or in its frames. `PaymentSteps(secrets, redirectContains)` returns the five journey steps for a payment. |
| `StripeCheckoutSelectors` / `StripeCheckoutSecretKeys` | The selectors (`#cardNumber`, `#cardExpiry`, `#cardCvc`, `#billingName`, `#billingPostalCode`, `#billingCountry`, `#email`, submit, Link dismiss candidates) centralized so a Stripe UI change is one fix, and the env-file key names (`CARD_NUMBER`, `CARD_EXPIRY`, `CARD_CVC`, `CARD_NAME`, `CARD_POSTAL_CODE`, `CARD_COUNTRY`, `CUSTOMER_EMAIL`). |
| `HttpJsonProbe` | Out-of-band verification against a product API: `GetJsonAsync<T>`, `PostJsonAsync`, `WaitForAsync<T>(url, predicate, timeout)` polling, `WithHeader`/`WithBearer` (values never in `ToString()` or exceptions). Non-2xx → `HttpJsonProbeException` with status and body. |
| `Poll` | `UntilAsync(condition, timeout, interval)` / `UntilOrThrowAsync(description, …)` — the one polling wait every other component is built on. |
| `JourneyTestMenuContainer` | bam.test adapter: derive, mark `[IntegrationTestMenu]` / `[IntegrationTest]`, call `Run(journey)` (shared session, live report, final report under `~/.bam/tests/journeys/<slug>/<timestamp>/`) and `Expect(because, result)` in `ShouldPass<JourneyResult>`. |
| `AutomationPageExtensions` | `GetCurrentUrl()` — the live browser url (`AutomationPage.Url` returns the url the page was opened with). |

### Change to `bam.automation`

`AutomationPage` gained an additive launch path: a constructor and `Open(IBrowser, url)` factory that bind a page to a caller-owned browser (`OwnsBrowser == false`, so `Dispose` closes only the page) and `LaunchBrowserAsync(LaunchOptions)` which downloads the browser build PuppeteerSharp is pinned to and resolves `ExecutablePath` to it. The static headless singleton (`AutomationPage.Open(url)`) is unchanged for existing callers and now shares the same pinned download — `BrowserTag.Latest` fetched a Chrome newer than PuppeteerSharp 20.0.2 understands and every navigation failed with `Protocol error (Page.navigate): Invalid referrerPolicy`.

## Dependencies

### Project References
- `bam.automation` -- `AutomationPage`, `PageAction`, `PageActionSequence`, `PageAssertion`, PuppeteerSharp
- `bam.base` -- `BamProfile` paths, JSON extensions
- `bam.test` -- `IntegrationTestMenuContainer`, `Because`

### Package References
None (PuppeteerSharp 20.0.2 is transitive through `bam.automation`).

## Usage Examples

### A journey test (the consumer's thin script)
```csharp
using Bam.Automation.Testing;
using Bam.Test;
using Bam.Test.Integration;

[IntegrationTestMenu("AirroamerPurchase")]
public class AirroamerPurchaseJourney : JourneyTestMenuContainer
{
    [IntegrationTest]
    public void BuyTheCheapestSpainPlan()
    {
        EnvFileSecrets secrets = EnvFileSecrets.Load("~/.claude/secrets/airroamer.e2e.env");
        secrets.Require(StripeCheckoutSecretKeys.Required); // stops before the browser opens if the card file is blank

        Journey journey = new Journey("airroamer purchase")
            .NoteMasked(secrets, StripeCheckoutSecretKeys.CardNumber)
            .Navigate("https://airroamer.co/en/destinations")
            .Click("a[href*='/destinations/spain']")
            .Click("[data-plan] button")
            .TypeSecret("#email", secrets, StripeCheckoutSecretKeys.CustomerEmail)
            .Click("button[type=submit]")
            .AddRange(StripeCheckoutPage.PaymentSteps(secrets, redirectContains: "airroamer.co/order/"))
            .WaitUntil("the order page shows Delivered", async () => await OrderIsDeliveredAsync(), TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(10));

        When.A<Journey>("completes a live purchase", journey, (j) => Run(j))
            .TheTest
            .ShouldPass<JourneyResult>((because, result) => Expect(because, result))
            .SoBeHappy()
            .UnlessItFailed();
    }
}
```

### Out-of-band verification while the browser runs
```csharp
using HttpJsonProbe api = new HttpJsonProbe().WithHeader("X-Internal-Api-Key", secrets, "INTERNAL_API_KEY");
OrderDto order = await api.WaitForAsync<OrderDto>($"https://airroamer.co/api/orders/by-session/{sessionId}", o => o.Status == "Delivered", TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(10));
journey.Note("order id", order.Id);
```

### Running a journey without the adapter
```csharp
using BrowserSession session = new BrowserSession(BrowserSessionOptions.FromEnvironment());
Journey journey = new Journey("sample").Navigate(url).Type("#name", "Bam").Click("#go").WaitForSelector("#greeting").Screenshot("done");
using (JourneyReport.Follow(journey))          // report.md is rewritten after every step
{
    JourneyResult result = await journey.RunAsync(session);
    JourneyReport.Write(result);                // report.md + result.json in result.ArtifactsDirectory
}
```

### Running the tests
```bash
dotnet run --project bam.automation.testing.tests -- --ut   # engine, secrets, report, probe, selectors (no browser)
dotnet run --project bam.automation.testing.tests -- --it   # launches a browser against local fixtures; headed on a dev box
BAM_AUTOMATION_HEADLESS=1 dotnet run --project bam.automation.testing.tests -- --it   # headless (CI)
```

## Known Gaps / Not Yet Implemented

- **`BamBot.Automation` namespace** -- `bam.automation` still lives under the legacy `BamBot.*` root; moving it to `Bam.Automation` is tracked separately (bam.automation#2) and is why this project's `using`s mix `Bam.Automation.Testing` and `BamBot.Automation`.
- **`AutomationPage.Url` does not follow navigation** -- it returns the url the page was opened with. Use `GetCurrentUrl()` from `AutomationPageExtensions`; a fix in `bam.automation` would make `PageAssertion(pageName, Uri)` and `PagePathAssertion` agree.
- **Stripe interstitials** -- Link and 3-D Secure are handled by waiting for the redirect in a headed run so the owner can intervene; there is no automated 3DS challenge handling.
- **Email assertions** -- reading an inbox (IMAP) is left to the consumer; `WaitUntil` is the hook.
