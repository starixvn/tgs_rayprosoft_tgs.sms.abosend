using System.Text.Json.Serialization;

namespace Tgs.Sms.Abosend;
public record SmsSettings(
	[property: JsonPropertyName("orgCode")] string OrgCode,
	[property: JsonPropertyName("apiKey")] string ApiKey);