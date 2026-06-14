using System.Net;
using System.Net.Http.Json;
using Platform.Contracts.Http;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class PublicAlphaTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public PublicAlphaTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Landing_page_is_served_at_root()
    {
        var http = _factory.CreateClient();

        var response = await http.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("SpawnWeaver", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Feedback_can_be_submitted_and_listed()
    {
        var http = _factory.CreateClient();

        var submit = await http.PostAsJsonAsync("/api/feedback",
            new FeedbackRequest("tester@example.com", "Loving the alpha — matchmaking is smooth."));
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var created = await submit.Content.ReadFromJsonAsync<FeedbackResponse>();
        Assert.StartsWith("fb_", created!.Id);

        var list = await http.GetFromJsonAsync<FeedbackListResponse>("/api/admin/feedback");
        Assert.Contains(list!.Items, f => f.Id == created.Id && f.Email == "tester@example.com");
    }

    [Fact]
    public async Task Empty_feedback_is_rejected()
    {
        var http = _factory.CreateClient();

        var response = await http.PostAsJsonAsync("/api/feedback", new FeedbackRequest(null, "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
