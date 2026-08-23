using System.Diagnostics;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Provides the <see cref="ActivitySource"/> for the PoliNorError.Extensions.Http library.
	/// Subscribe to the <see cref="SourceName"/> via OpenTelemetry or <see cref="ActivityListener"/>
	/// to observe resilience pipeline execution.
	/// </summary>
	/// <remarks>
	/// Activity tags emitted by this library:
	/// <list type="bullet">
	///   <item><c>polinorerror.pipeline.result</c>: <c>"success"</c>, <c>"failed"</c>, <c>"canceled"</c>, or <c>"faulted"</c> (unexpected exception escaped the policy)</item>
	///   <item><c>polinorerror.pipeline.is_final_handler</c>: <c>true</c> if this handler is the final (response-classifying) handler</item>
	///   <item><c>polinorerror.pipeline.policy.type</c>: short name of the PoliNorError policy type (e.g. <c>"RetryPolicy"</c>, <c>"FallbackPolicy"</c>)</item>
	///   <item><c>polinorerror.pipeline.policy.name</c>: the name of the PoliNorError policy, emitted only when explicitly set via <c>WithPolicyName</c></item>
	///   <item><c>http.request.method</c>: HTTP request method (e.g. <c>"GET"</c>, <c>"POST"</c>), per OTel semantic conventions</item>
	///   <item><c>url.full</c>: absolute request URL with sensitive query parameters redacted</item>
	///   <item><c>url.path</c>: request path</item>
	///   <item><c>server.address</c>: server domain name or IP</item>
	///   <item><c>http.response.status_code</c>: HTTP response status code as an <c>int</c>; emitted on success and on the final handler when the response status was filtered</item>
	/// </list>
	/// </remarks>
	public static class PipelineTelemetry
	{
		/// <summary>
		/// The well-known name used to identify the <see cref="ActivitySource"/>.
		/// Pass this to <c>TracerProviderBuilder.AddSource()</c> or <c>ActivitySource.AddActivityListener()</c>.
		/// </summary>
		public const string SourceName = "PoliNorError.Extensions.Http";

		/// <summary>
		/// Name of the tag carrying the pipeline execution result:
		/// <c>"success"</c>, <c>"failed"</c>, <c>"canceled"</c>, or <c>"faulted"</c>.
		/// </summary>
		public const string ResultTag = "polinorerror.pipeline.result";

		/// <summary>
		/// Name of the tag carrying the short PoliNorError policy type name
		/// (e.g. <c>"RetryPolicy"</c>, <c>"FallbackPolicy"</c>).
		/// </summary>
		public const string PolicyTypeTag = "polinorerror.pipeline.policy.type";

		/// <summary>
		/// Name of the tag carrying the user-configured PoliNorError policy name
		/// (e.g. <c>"catalog-api-retry"</c>). Emitted only when the policy has a name set
		/// via <c>WithPolicyName</c>.
		/// </summary>
		public const string PolicyNameTag = "polinorerror.pipeline.policy.name";

		/// <summary>
		/// Name of the tag indicating whether this handler is the final (response-classifying) handler.
		/// </summary>
		public const string IsFinalHandlerTag = "polinorerror.pipeline.is_final_handler";

		/// <summary>
		/// The <see cref="ActivitySource"/> used to create activities for pipeline execution.
		/// This is safe to use statically - when no listener is attached, all operations are no-alloc no-ops.
		/// </summary>
		public static readonly ActivitySource Source = new ActivitySource(SourceName, typeof(PipelineTelemetry).Assembly.GetName().Version?.ToString());

		/// <summary>
		/// The operation name for the top-level pipeline Activity.
		/// </summary>
		internal const string PipelineOperationName = "HttpPipeline.Execute";
	}
}
