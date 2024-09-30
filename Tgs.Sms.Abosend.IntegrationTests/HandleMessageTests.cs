using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RichardSzalay.MockHttp;
using Tgs.BrandCertificate.Clients;
using Tgs.Sms.Abosend.MessageQueue;
using Tgs.Sms.Protos;
using Valhalla.MessageQueue;
using Xunit.Abstractions;

namespace Tgs.Sms.Abosend.IntegrationTests;
public class HandleMessageTests(ITestOutputHelper testOutputHelper)
{
    [Fact(Timeout = 3000)]
    public async Task 處理傳送簡訊要求()
    {
        // Arrange
        var fakeBrandServiceClient = Substitute.For<IBrandServiceClient>();
        var fakeTimeProvider = Substitute.For<TimeProvider>();
        var mockHttp = new MockHttpMessageHandler();

        _ = fakeTimeProvider.GetUtcNow()
            .Returns(new DateTimeOffset(2024, 9, 30, 6, 10, 27, TimeSpan.Zero));

        await using var web = AbosendTestHost.Instance
            .WithWebHostBuilder(hostBuilder => hostBuilder
                .ConfigureLogging(logging => logging.AddXUnit(testOutputHelper))
                .ConfigureServices(services => services
                    .AddSingleton(fakeTimeProvider)
                    .AddSingleton(fakeBrandServiceClient)
                    .AddMessageQueue()
                    .AddDirectMessageQueue(config => config
                        .AddHandler(ProcessorTypes.AbosendReceiveHandlerType, "tgs.sms.abosend"))
                    .ClearAllExchanges()
                    .AddDirectGlobPatternExchange("*")
                    .Services
                    .AddHttpClient<AbosendApi>()
                    .ConfigurePrimaryHttpMessageHandler(_ => mockHttp)));

        var brandId = Guid.NewGuid();
        var request = new ShortMessage
        {
            BrandId = brandId,
            Content = "testMessage09301403",
            Destination = "+8616535533188",
            AreaCode = "+86",
            Phone = "16535533188"
        };

        var messageSender = web.Services.GetRequiredService<IMessageSender>();

        _ = fakeBrandServiceClient.GetBrandInfoAsync(Arg.Is(brandId), Arg.Any<CancellationToken>())
            .Returns(new BrandInfo
            {
                Id = brandId,
                SmsEnabled = true
            });

        var vendorInfo = new SmsVendorInfo<SmsSettings>(
            "Test",
            new SmsSettings("orgCode", "apiKey"));
        _ = fakeBrandServiceClient.GetBrandSmsSettingsAsync<SmsSettings>(
            Arg.Is(brandId),
            Arg.Any<JsonSerializerOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(vendorInfo);

        var tcs = new TaskCompletionSource();
        mockHttp.Expect("http://localhost/v2/api/sendSMS")
            .Respond(
                HttpStatusCode.OK,
                _ =>
                {
                    tcs.SetResult();

                    return JsonContent.Create(
                    """
                    {"code":200,"data":{"sendCode":"20240930143838OFWK1"},"message":"Submitted successfully"}
                    """);
                });

        // Act
        await messageSender.PublishAsync<ReadOnlyMemory<byte>>("tgs.sms.abosend", request.ToByteArray());
        await tcs.Task;
    }
}
