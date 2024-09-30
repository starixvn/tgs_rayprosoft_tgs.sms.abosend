using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RichardSzalay.MockHttp;
using Tgs.Sms.Abosend;

namespace Abosend.Core.UnitTests;

public class AbosendApiTests
{
    [Fact]
    public async Task 傳送簡訊()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var fakeTimeProvider = Substitute.For<TimeProvider>();

        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        var sut = new AbosendApi(httpClient, fakeTimeProvider, NullLogger<AbosendApi>.Instance);

        _ = fakeTimeProvider.GetUtcNow()
            .Returns(new DateTimeOffset(2024, 9, 30, 6, 10, 27, TimeSpan.Zero));

        var orgCode = "orgCode";
        var phone = "8616535533188";
        var message = "testMessage09301403";
        var apiKey = "apiKey";

        mockHttp.Expect("http://localhost/v2/api/sendSMS")            
            .Respond(
                "application/json",
                """
                    {"code":200,"data":{"sendCode":"20240930143838OFWK1"},"message":"Submitted successfully"}
                    """);

        // Act
        await sut.SendMessageAsync(orgCode, apiKey, phone, message);
    }
}