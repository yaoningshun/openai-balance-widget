using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Saldo;

public static class BalanceTests {
    static int count;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    sealed class FakeHandler : HttpMessageHandler {
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
            Check(request.RequestUri.AbsoluteUri == "https://api.openai.com/v1/dashboard/billing/credit_grants", "fixed HTTPS destination");
            Check(request.Headers.Authorization.Scheme == "Bearer", "bearer authentication");
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent("{\"total_available\":42.80,\"total_paid_available\":12.30}") });
        }
    }
    public static int Main() {
        try {
            Balance b = Balance.Parse("{\"total_available\":63.72457891,\"total_paid_available\":8.43214567,\"total_granted\":85,\"total_used\":21.27542109}");
            Check(b.Available == 63.72457891m && b.Paid == 8.43214567m, "total and paid remain distinct");
            Check(b.Other == 55.29243324m, "other credits are explicitly derived");
            Check(Balance.Money(b.Available) == "$63.72", "invariant currency formatting");
            Check(Balance.Parse("{\"total_available\":-0.25}").Available == -0.25m, "negative balance is not clamped");
            Check(!Balance.Parse("{\"total_available\":0}").Paid.HasValue, "missing paid balance stays unknown");
            foreach (string data in new [] { "{}", "{\"total_available\":null}", "{\"total_available\":true}", "{\"total_available\":\"NaN\"}", "garbage" }) {
                bool threw = false; try { Balance.Parse(data); } catch (BalanceException) { threw = true; }
                Check(threw, "malformed data cannot become a zero balance");
            }
            var fake = new FakeHandler();
            using (var client = new BalanceClient(fake)) {
                Check(client.Fetch("sess-synthetic-test-only-0000", "", CancellationToken.None).GetAwaiter().GetResult().Available == 42.80m, "HTTP response parser");
                fake.Status = HttpStatusCode.Unauthorized;
                bool expired = false;
                try { client.Fetch("sess-synthetic-test-only-0000", "", CancellationToken.None).GetAwaiter().GetResult(); } catch (BalanceException e) { expired = e.Message.Contains("Reconnect") && !e.Message.Contains("sess-"); }
                Check(expired, "expired-session error excludes credentials");
                bool rejected = false;
                try { client.Fetch("sk-synthetic-inference-key", "", CancellationToken.None).GetAwaiter().GetResult(); } catch (BalanceException) { rejected = true; }
                Check(rejected, "inference API keys rejected before sending");
            }
            Console.WriteLine(count + " checks passed. All HTTP uses an in-memory handler."); return 0;
        } catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }
}
