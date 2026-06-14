using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Platform.Contracts.Http;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class DashboardTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public DashboardTests(SpawnWeaverApiFactory factory) => _factory = factory;

    private HttpClient NoRedirectClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Signed_in_home_renders_and_lists_the_workspace_projects()
    {
        var http = await _factory.CreateAuthenticatedClientAsync();
        await http.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Dashboard Visible Project"));

        var response = await http.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("SpawnWeaver Dashboard", html, StringComparison.Ordinal);
        Assert.Contains("Dashboard Visible Project", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Protected_dashboard_page_redirects_to_getting_started_when_logged_out()
    {
        var http = NoRedirectClient();

        foreach (var path in new[] { "/dashboard", "/dashboard/projects", "/dashboard/account", "/dashboard/realtime" })
        {
            var response = await http.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/dashboard/getting-started", response.Headers.Location?.ToString());
        }
    }

    [Fact]
    public async Task Getting_started_is_public_and_explains_sign_in_is_needed()
    {
        var http = _factory.CreateClient();

        var html = await http.GetStringAsync("/dashboard/getting-started");

        Assert.Contains("Getting started", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sign", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Realtime_page_renders_when_signed_in()
    {
        var http = await _factory.CreateAuthenticatedClientAsync();

        var html = await http.GetStringAsync("/dashboard/realtime");

        Assert.Contains("Connections", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projects_page_has_create_flow_when_signed_in()
    {
        var http = await _factory.CreateAuthenticatedClientAsync();

        var html = await http.GetStringAsync("/dashboard/projects");

        Assert.Contains("swCreateProject()", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dashboard_stylesheet_is_served_publicly()
    {
        var http = _factory.CreateClient();

        var response = await http.GetAsync("/spawnweaver.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/css", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task Sign_in_and_sign_up_pages_are_public()
    {
        var http = _factory.CreateClient();

        var signin = await http.GetStringAsync("/dashboard/signin");
        Assert.Contains("swSignIn()", signin, StringComparison.Ordinal);

        var signup = await http.GetStringAsync("/dashboard/signup");
        Assert.Contains("swSignUp()", signup, StringComparison.Ordinal);
        Assert.Contains("Create your account", signup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Onboarding_create_form_renders_when_signed_in()
    {
        var http = await _factory.CreateAuthenticatedClientAsync();

        var html = await http.GetStringAsync("/dashboard/onboarding");

        Assert.Contains("swCreateProjectOnboarding()", html, StringComparison.Ordinal);
        Assert.Contains("Project name", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Docs_are_public_and_render()
    {
        var http = _factory.CreateClient(); // anonymous

        var index = await http.GetAsync("/dashboard/docs");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Contains("SpawnWeaver docs", await index.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var tutorial = await http.GetStringAsync("/dashboard/docs/tutorial-state-sync");
        Assert.Contains("Simple state sync", tutorial, StringComparison.Ordinal);

        var reference = await http.GetStringAsync("/dashboard/docs/sdk-reference");
        Assert.Contains("state_forbidden", reference, StringComparison.Ordinal);
        Assert.Contains("rate_limited", reference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Debugger_pages_render_when_signed_in()
    {
        var http = await _factory.CreateAuthenticatedClientAsync();

        var hub = await http.GetStringAsync("/dashboard/debugger");
        Assert.Contains("Debugger", hub, StringComparison.Ordinal);

        var errors = await http.GetStringAsync("/dashboard/errors");
        Assert.Contains("Error Explorer", errors, StringComparison.Ordinal);

        var debug = await http.GetStringAsync("/dashboard/debug");
        Assert.Contains("Debug Bundle Viewer", debug, StringComparison.Ordinal);
        Assert.Contains("swViewDebugBundle()", debug, StringComparison.Ordinal);
    }
}
