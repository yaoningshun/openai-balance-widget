using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Saldo {
    public sealed class Balance {
        public decimal Available, Used;
        public decimal? Paid;
        public DateTime Updated;
        public decimal? Other { get { return Paid.HasValue ? (decimal?)(Available - Paid.Value) : null; } }
        public static Balance Parse(string json) {
            Dictionary<string, object> data;
            try { data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json); }
            catch { throw new BalanceException("The balance response could not be read."); }
            decimal? available = Number(data, "total_available");
            if (!available.HasValue) throw new BalanceException("The response contains no available balance. Your previous balance is unchanged.");
            return new Balance { Available = available.Value, Paid = Number(data, "total_paid_available"), Used = Number(data, "total_used") ?? 0, Updated = DateTime.Now };
        }
        static decimal? Number(Dictionary<string, object> data, string name) {
            object value;
            if (data == null || !data.TryGetValue(name, out value) || value == null || value is bool) return null;
            decimal number;
            return Decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? (decimal?)number : null;
        }
        public static string Money(decimal value) { return "$" + value.ToString("N2", CultureInfo.InvariantCulture); }
        public static Balance Demo() { return new Balance { Available = 42.80m, Paid = 12.30m, Used = 7.20m, Updated = DateTime.Now }; }
    }
    public class BalanceException : Exception { public BalanceException(string message) : base(message) {} }
    public sealed class BalanceClient : IDisposable {
        readonly HttpClient client;
        public BalanceClient() : this(new HttpClientHandler { AllowAutoRedirect = false }) {}
        public BalanceClient(HttpMessageHandler handler) { client = new HttpClient(handler); client.Timeout = TimeSpan.FromSeconds(18); }
        public async Task<Balance> Fetch(string session, string organization, CancellationToken cancellation) {
            session = (session ?? "").Trim();
            if (!session.StartsWith("sess-", StringComparison.Ordinal) || session.Length < 20)
                throw new BalanceException("Use an OpenAI browser session (sess-), rather than an inference API key.");
            // Fixed HTTPS destination. No custom hosts, redirects, cookies or browser scanning.
            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/dashboard/billing/credit_grants")) {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.UserAgent.ParseAdd("Saldo/0.1");
                if (!String.IsNullOrWhiteSpace(organization)) {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(organization, @"^org-[A-Za-z0-9_-]+$"))
                        throw new BalanceException("Organization IDs start with org-.");
                    request.Headers.Add("OpenAI-Organization", organization);
                }
                try {
                    using (var response = await client.SendAsync(request, cancellation)) {
                        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                            throw new BalanceException("Your session expired or cannot access this organization. Reconnect in Settings.");
                        if ((int)response.StatusCode == 429) throw new BalanceException("Too many requests. Saldo will wait before retrying.");
                        if (!response.IsSuccessStatusCode) throw new BalanceException("OpenAI returned HTTP " + (int)response.StatusCode + ". Your previous balance is unchanged.");
                        return Balance.Parse(await response.Content.ReadAsStringAsync());
                    }
                } catch (BalanceException) { throw; }
                catch (OperationCanceledException) { throw new BalanceException("The request timed out. Your previous balance is unchanged."); }
                catch (HttpRequestException) { throw new BalanceException("Could not reach OpenAI. Check your connection or proxy."); }
            }
        }
        public void Dispose() { client.Dispose(); }
    }
}
