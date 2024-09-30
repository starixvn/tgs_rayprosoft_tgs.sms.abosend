using FluentdForward.OpenTelemetry.MessagePack;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Tgs.Sms.Abosend;
using Tgs.Sms.Abosend.MessageQueue;

var builder = WebApplication.CreateBuilder(args);
{
	builder.Logging
		.AddOpenTelemetry(options =>
		{
			options.IncludeFormattedMessage = true;
			options.ParseStateValues = true;
			_ = options.AddFluentdForwardExporter(fluentOptions =>
			{
				var nodeIp = builder.Configuration.GetValue<string>("NODE_IP")!;
				fluentOptions.Host = nodeIp;
				fluentOptions.Tag = builder.Configuration.GetValue<string>("OTEL_SERVICE_NAME")!;
				fluentOptions.UseMessagePack(LogRecordFormatterResolver.GetResolverInstanceWithExtendInfo(
					"MachineInfo",
					() => new
					{
						Environment.MachineName
					}));
			});
		});

	builder.Services
		.AddSingleton(TimeProvider.System)
		.AddHttpClientLoggingHandler()
		.AddBrandServiceClient((options, _) => options.BrandCertificateBaseUrl =
			builder.Configuration.GetValue<Uri>("Abosend:BrandCertificateBaseUrl")!)
				.AddAbosendSms(httpBuilder => httpBuilder
			.ConfigureHttpClient(httpClient => httpClient.BaseAddress =
				builder.Configuration.GetValue<Uri>("Abosend:ApiBaseUrl")!)
			.AddLoggingHandler<SmsSettings>());

	builder.Services
		.AddMessageQueue()
		.AddRabbitMessageQueue(config => config
			.ConfigQueueOptions((options, _) =>
			{
				builder.Configuration.GetSection("Abosend:RabbitMQ").Bind(options);
				options.RabbitMQUrl = new Uri(builder.Configuration.GetConnectionString("RabbitMQ")!);

				options.OnSetupQueueAndExchange = channel =>
				{
					channel.QueueDeclare(
						"tgs.sms.abosend",
						true,
						false,
						false,
						null);

					channel.QueueBind(
						"tgs.sms.abosend",
						"amq.direct",
						"tgs.sms.abosend",
						null);
				};
			})
			.AddHandler(ProcessorTypes.AbosendReceiveHandlerType, "tgs.sms.abosend", false))
		.AddRabbitGlobPatternExchange("*", "amq.direct");

	builder.Services
		.AddHealthChecks();

	var excludeExtensions = new[] { "html", "ico", "png", "jpg" };
	builder.Services
		.AddOpenTelemetry()
		.WithTracing(tracingBuilder => tracingBuilder
			.SetResourceBuilder(ResourceBuilder.CreateDefault()
				.AddEnvironmentVariableDetector())
			.AddSource("Tgs.*")
			.AddSource("TGS.*")
			.AddSource("Valhalla.*")
			.AddAspNetCoreInstrumentation(options => options.Filter =
			httpContext => !httpContext.Request.Path.HasValue
				|| !httpContext.Request.Path.StartsWithSegments("/swagger")
				&& !httpContext.Request.Path.StartsWithSegments("/healthz")
				&& !httpContext.Request.Path.StartsWithSegments("/metrics")
				&& !httpContext.Request.Path.StartsWithSegments("/profiler/")
				&& !excludeExtensions.Any(
					ext => httpContext.Request.Path.Value.EndsWith($".{ext}")))
			.AddProfileViewExporter()
			.AddOtlpExporter())
		.WithMetrics(meterBuiler => meterBuiler
			.SetResourceBuilder(ResourceBuilder.CreateDefault()
				.AddEnvironmentVariableDetector())
				.AddRuntimeInstrumentation()
				.AddAspNetCoreInstrumentation()
				.AddOtlpExporter())
		.Services
		.AddProfileViewer();
}

var app = builder.Build();
{
	app.MapHealthChecks("/healthz");
	app.MapProfileViewer();
}

app.Run();
