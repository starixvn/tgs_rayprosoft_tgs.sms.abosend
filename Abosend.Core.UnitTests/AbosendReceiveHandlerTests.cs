using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RichardSzalay.MockHttp;
using Tgs.BrandCertificate.Clients;
using Tgs.Sms.Abosend;
using Tgs.Sms.Abosend.MessageQueue;
using Tgs.Sms.Protos;

namespace Abosend.Core.UnitTests;

public class AbosendReceiveHandlerTests
{
    [Fact]
    public async Task 接收到發送簡訊要求()
    {
        // Arrange
        var fakeBrandServiceClient = Substitute.For<IBrandServiceClient>();
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        var fakeProvider = Substitute.For<TimeProvider>();

        var sut = new AbosendReceiveHandler(
            fakeBrandServiceClient,
            new AbosendApi(httpClient, fakeProvider, NullLogger<AbosendApi>.Instance),
            NullLogger<AbosendReceiveHandler>.Instance);

        var brandId = Guid.NewGuid();
        var request = new ShortMessage
        {
            BrandId = brandId,
            Content = "testMessage09301403",
            Destination = "+8616535533188",
            AreaCode = "+86",
            Phone = "16535533188"
        };

        _ = fakeBrandServiceClient.GetBrandInfoAsync(Arg.Is(brandId))
            .Returns(new BrandInfo
            {
                Id = brandId,
                SmsEnabled = true
            });

        var vendorInfo = new SmsVendorInfo<SmsSettings>(
            "Test",
            new SmsSettings("orgCode", "apiKey"));
        _ = fakeBrandServiceClient.GetBrandSmsSettingsAsync<SmsSettings>(Arg.Is(brandId))
            .Returns(vendorInfo);

        mockHttp.Expect("http://localhost/v2/api/sendSMS")
            .Respond(
                "application/json",
                """
                    {"code":200,"data":{"sendCode":"20240930143838OFWK1"},"message":"Submitted successfully"}
                    """);

        // Act
        await sut.HandleAsync("test", request.ToByteArray(), null);
    }
}