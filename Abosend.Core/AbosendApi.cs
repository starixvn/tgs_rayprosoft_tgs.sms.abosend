using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Tgs.Sms.Abosend;

internal class AbosendApi(
	HttpClient httpClient,
	TimeProvider timeProvider,
	ILogger<AbosendApi> logger)
{
	public async ValueTask SendMessageAsync(
		string orgCode,
		string apiKey,
		string phone,
		string message,
		CancellationToken cancellationToken = default)
	{
		var rand = timeProvider.GetUtcNow().ToString("HHmmss");

		var data = new
		{
			orgCode,
			mobiles = phone.TrimStart('+'),
			content = message,
			rand,
			sign = AbosendSmsHasher.ComputeHash($"{orgCode}{message}{rand}{apiKey}")
		};
		var requestContent = JsonSerializer.Serialize(data);

		var content = new StringContent(requestContent, Encoding.UTF8, MediaTypeNames.Application.Json);

		var response = await httpClient
			.PostAsync("/v2/api/sendSMS", content, cancellationToken)
			.ConfigureAwait(false);

		var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		var node = JsonNode.Parse(body) ?? throw new Exception("SendMessage response can't read.");

		if (node["code"]!.GetValue<int>() != 200)
			throw new Exception("Send message fail.");

		logger.LogInformation("SendCode:{sendCode}", node["data"]!["sendCode"]!.GetValue<string>());
	}
}