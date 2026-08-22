using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Data.LLM;
using Xunit;

namespace Nexus.Tests;

public class LlmServiceTests
{
    private IOptions<LLMOptions> GetOptions(string baseUrl = "http://localhost:11434", string model = "llama3.2", int timeout = 2)
    {
        return Options.Create(new LLMOptions
        {
            BaseUrl = baseUrl,
            Model = model,
            TimeoutSeconds = timeout,
            Provider = "Ollama"
        });
    }

    [Fact]
    public async Task LocalLLMService_Fails_Gracefully_When_Server_Is_Offline()
    {
        // Use an invalid port to guarantee offline failure
        var options = GetOptions("http://localhost:59999");
        using var client = new HttpClient();
        var service = new LocalLLMService(client, options, NullLogger<LocalLLMService>.Instance);

        var health = await service.CheckHealthAsync();
        Assert.False(health.IsAvailable);
        Assert.NotNull(health.ErrorMessage);
        Assert.Contains("unreachable", health.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var completion = await service.GenerateCompletionAsync("Hello test");
        Assert.False(completion.IsSuccess);
        Assert.NotNull(completion.ErrorMessage);
        Assert.True(completion.ErrorMessage.Contains("timed out", StringComparison.OrdinalIgnoreCase) || 
                    completion.ErrorMessage.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LocalLLMService_Parses_Mocked_Ollama_Response_Successfully()
    {
        var mockHandler = new MockHttpMessageHandler(@"{ ""response"": ""{\""intent\"": \""onboard_employee\""}"" }", HttpStatusCode.OK);
        using var client = new HttpClient(mockHandler);

        var options = GetOptions();
        var service = new LocalLLMService(client, options, NullLogger<LocalLLMService>.Instance);

        var response = await service.GenerateCompletionAsync("Onboard Ahmed Khan");
        Assert.True(response.IsSuccess);
        Assert.Equal("{\"intent\": \"onboard_employee\"}", response.Content);

        var jsonObject = await service.GenerateJsonAsync<MockIntentPayload>("Onboard Ahmed Khan");
        Assert.NotNull(jsonObject);
        Assert.Equal("onboard_employee", jsonObject!.Intent);
    }

    [Fact]
    public async Task LocalLLMService_Strips_Markdown_Fences_From_Json_Result()
    {
        var jsonWithMarkdown = "```json\n{\n  \"intent\": \"budget_check\"\n}\n```";
        var mockHandler = new MockHttpMessageHandler(@"{ ""response"": """ + jsonWithMarkdown.Replace("\n", "\\n").Replace("\"", "\\\"") + @""" }", HttpStatusCode.OK);
        using var client = new HttpClient(mockHandler);

        var options = GetOptions();
        var service = new LocalLLMService(client, options, NullLogger<LocalLLMService>.Instance);

        var result = await service.GenerateJsonAsync<MockIntentPayload>("Check budget");
        Assert.NotNull(result);
        Assert.Equal("budget_check", result!.Intent);
    }

    private class MockIntentPayload
    {
        public string Intent { get; set; } = string.Empty;
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler(string response, HttpStatusCode statusCode)
        {
            _response = response;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpResponse = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_response, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(httpResponse);
        }
    }
}
