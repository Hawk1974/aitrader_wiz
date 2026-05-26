using System.Net;
using System.Net.Http;
using System.Text;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz.UnitTests;

public sealed class ValidationServiceTests
{
    [Fact]
    public async Task ValidateAlpacaPaperAsync_Fails_WhenCredentialsMissing()
    {
        var service = new ValidationService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await service.ValidateAlpacaPaperAsync(new AlpacaPaperConfiguration());

        Assert.Equal(ValidationStatus.FailedBlocking, result.Status);
    }

    [Fact]
    public async Task ValidateLmStudioAsync_Passes_WhenEndpointReturnsSuccess()
    {
        var service = new ValidationService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json") })));

        var result = await service.ValidateLmStudioAsync(new LmStudioConfiguration
        {
            BaseUrl = "http://localhost:1234",
            ModelId = "qwen",
        });

        Assert.Equal(ValidationStatus.Passed, result.Status);
    }

    [Fact]
    public void ValidateConnectivity_Warns_WhenBootstrapNotComplete()
    {
        var service = new ValidationService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var result = service.ValidateConnectivity(new ConnectivityConfiguration
        {
            BackendTargetHostOrIp = "100.100.100.2",
            DesktopTargetHostOrIp = "100.100.100.3",
            RequiresTailscale = true,
            BootstrapComplete = false,
        });

        Assert.Equal(ValidationStatus.PassedWithWarning, result.Status);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
