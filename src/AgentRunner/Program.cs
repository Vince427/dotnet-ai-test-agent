using System;
using System.Threading;
using DesktopAiTestAgent.UIAutomation;

namespace DesktopAiTestAgent.AgentRunner;

internal static class Program
{
    private static int Main(string[] args)
    {
        var targetWindow = args != null && args.Length > 0
            ? args[0]
            : "Sample Login App (.NET 8)";

        Console.WriteLine("Desktop AI Test Agent V1.2 Dual Target");
        Console.WriteLine("Target window: " + targetWindow);

        using (var driver = new FlaUiDesktopDriver())
        {
            Console.WriteLine("Attaching to target window...");
            if (!driver.AttachToWindow(targetWindow, TimeSpan.FromSeconds(20)))
            {
                Console.Error.WriteLine("Could not attach to target window.");
                return 1;
            }

            var snapshot = driver.Capture();
            Console.WriteLine("Window attached: " + snapshot.WindowTitle);

            Console.WriteLine("Entering credentials...");
            driver.EnterText(snapshot.UsernameFieldId, "admin");
            driver.EnterText(snapshot.PasswordFieldId, "password123");

            Console.WriteLine("Clicking login...");
            driver.Click(snapshot.LoginButtonId);

            Console.WriteLine("Waiting for success label...");
            var deadline = DateTime.UtcNow.AddSeconds(5);
            string status = null;
            while (DateTime.UtcNow < deadline)
            {
                status = driver.ReadText(snapshot.StatusLabelId);
                Console.WriteLine("Current status: " + status);
                if (string.Equals(status, "Login successful", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("SUCCESS: automated login flow passed.");
                    return 0;
                }
                Thread.Sleep(300);
            }

            Console.Error.WriteLine("FAILURE: expected 'Login successful' but got '" + status + "'.");
            return 3;
        }
    }
}
