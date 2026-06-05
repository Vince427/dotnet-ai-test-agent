using System;
using System.IO;
using System.Linq;
using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner.Tests;

/// <summary>
/// Gated, interactive desktop E2E for the live UIA event source (V9.5 inc.2b): launches the WinForms
/// sample, wires a real <see cref="UiaSessionRecorder"/> → pure <see cref="SessionRecorder"/> with a
/// real <see cref="SecretRedactor"/>, drives the form via the real driver, and asserts the recorder
/// captured the interactions AND that the typed password was redacted AT CAPTURE (never on disk).
///
/// Env-bound: UIA needs a logged-in desktop session and the sample exe must be built. Gated by
/// <see cref="InteractiveUiFactAttribute"/> (RUN_E2E_UI=1) — Skipped otherwise, never a false green.
/// </summary>
[Collection(InteractiveUiCollection.Name)]
public sealed class UiaSessionRecorderE2ETests
{
    [InteractiveUiFact]
    public void LiveCapture_RecordsActions_AndRedactsPasswordAtCapture()
    {
        var target = DesktopE2E.WinForms;
        const string secret = "Sup3rSecretPwd!";

        using var app = DesktopE2E.LaunchSample(target);
        try
        {
            using var driver = new FlaUiDesktopDriver();
            DesktopE2E.WaitForControlReady(driver, target.WindowTitle, "txtUsername", TimeSpan.FromSeconds(20));

            var redactor = new SecretRedactor();
            var sink = new SessionRecorder { Window = target.WindowTitle, Title = target.WindowTitle };

            using var recorder = new UiaSessionRecorder(sink.Observe, redactor.RedactValue);
            Assert.True(recorder.Attach(target.WindowTitle, TimeSpan.FromSeconds(10)));

            // Drive the form: a non-secret field, the password field, then the login button. UIA raises
            // ValueChanged on the edits and Invoked on the button; the recorder maps + smooths them.
            driver.EnterText("txtUsername", "admin");
            driver.EnterText("txtPassword", secret);
            driver.Click("btnLogin");

            // Give UIA a beat to deliver the events (they are delivered on a background thread).
            System.Threading.Thread.Sleep(1500);

            var session = sink.ToSession();
            var json = System.Text.Json.JsonSerializer.Serialize(session);

            // The secret-safety invariant: the typed password must never appear in the session/JSON.
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);

            // The password field's captured value (if any) is the redaction placeholder, not the secret.
            var pwd = session.Actions.FirstOrDefault(a =>
                string.Equals(a.Target, "txtPassword", StringComparison.OrdinalIgnoreCase));
            if (pwd != null && pwd.Value != null)
                Assert.Equal(SecretRedactor.RedactedValue, pwd.Value);

            // We captured at least one interaction (the exact event set is framework-dependent).
            Assert.True(session.Actions.Count > 0, "Expected at least one captured action.");
        }
        finally
        {
            try { if (!app.HasExited) app.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }
    }
}
